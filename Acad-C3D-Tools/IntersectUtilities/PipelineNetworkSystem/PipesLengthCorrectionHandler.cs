using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipesLengthCorrectionHandler
    {
        private List<PipelineSegment> _segments;
        private PipeSettingsCollection _pipeSettings;
        public PipesLengthCorrectionHandler(
            IEnumerable<Entity> entities, bool againstStationsOrder, PipeSettingsCollection psc)
        {
            _segments = PipelineSegmentFactory.CreateSegments(
                againstStationsOrder ? entities.Reverse() : entities);
            _pipeSettings = psc;
        }
        internal Result CorrectLengths(Database localDb)
        {
            Database db = localDb;
            Transaction tx = db.TransactionManager.TopTransaction;
            Result result = new Result();

            foreach (PipelineSegment ps in _segments)
            {
                //We don't want to process last polyline
                //As its end is usually set correctly
                //To process last polyline set > 0
                while (ps.PolylinesLeft > 1)
                {
                    Polyline pline1 = ps.ProcessNextPolyline();

                    double pipeStdLength = _pipeSettings.GetSettingsLength(pline1);
                    //We don't want to correct 100 m pipes
                    if (pipeStdLength > 99) continue;
                    double pipeLength = pline1.Length;
                    double division = pipeLength / pipeStdLength;
                    int nrOfSections = (int)division;
                    double remainder = pipeLength - nrOfSections * pipeStdLength;

                    bool shorten = false;
                    if (remainder < 2 && nrOfSections > 0) shorten = true;
                    double missingLength = shorten ? remainder : pipeStdLength - remainder;

                    //Skip if pipe is missing less than 1 mm
                    if (remainder < 1e-3 || pipeStdLength - remainder < 1e-3)
                    {
                        prdDbg($"Pipe {pline1.Handle} is OK. R:{remainder}  L:{pipeStdLength - remainder}");
                        continue;
                    }
                    prdDbg($"{pline1.Handle} ML:{missingLength} R:{remainder} RI:{pipeStdLength - remainder}");

                    Polyline pline2 = ps.PeekNextPolyline();
                    #region Validate next polyline
                    if (pline2 == null)
                    {
                        prdDbg($"No next polyline after {pline1.Handle}. Length correction stopped.");
                        break;
                    }
                    #endregion
                    var nextEnt = ps.PeekNextEntityByPolyline(pline1);
                    BlockReference br = nextEnt as BlockReference;
                    #region Validating next entity
                    //Validating next entity
                    if (nextEnt == null)
                    {
                        prdDbg($"No next entity after {pline1.Handle}. Length correction stopped.");
                        return new Result(
                            ResultStatus.SoftError,
                            $"No next entity after {pline1.Handle}. Length correction stopped.");
                    }
                    if (nextEnt is Polyline pl)
                    {
                        throw new Exception($"Next entity after polyline {pline1.Handle} is a polyline {pl.Handle}! " +
                            $"This is not expected -- no consecutive polylines allowed in projects.");
                    }
                    if (br == null)
                        throw new Exception($"Next entity after polyline {pline1.Handle} is not a block reference! " +
                            $"This is not expected. Offending element: {nextEnt.Handle}.");
                    if (br.GetPipelineType() != PipelineElementType.Reduktion)
                    {
                        prdDbg($"Next entity {br.Handle} after {pline1.Handle} is not a reducer. " +
                            $"Length correction stopped.");
                        return new Result(ResultStatus.SoftError,
                            $"Next entity {br.Handle} after {pline1.Handle} is not a reducer. " +
                            $"Length correction stopped.");
                    }
                    #endregion

                    //(Old) Case 4: Missing length is relatively small (now 2 meters) -> move transition back
                    //if (missingLength >= pipeStdLength - 2 && nrOfSections != 0)
                    if (shorten)
                    {
                        result.Combine(CorrectPlinesReverse(db, pline1, br, pline2, missingLength, ps));                        
                        continue;
                    }

                    //Test to see if the next polylines' length is less than the missing length
                    //If so, the correction tries to check next polyline again and so on
                    //So we 'push' the missing length down the line until we find space
                    //Or segment terminates
                    //This must be done before all else
                    if (missingLength > pline2.Length)
                    {
                        using (Transaction tooShortTx = db.TransactionManager.StartTransaction())
                        {
                            Result pushResult = PushMissingLengthDownTheLine(db, pline2, ps, missingLength);
                            result.Combine(pushResult);
                            if (pushResult.Status != ResultStatus.OK)
                            {
                                //If pushing the missing length down the line failed
                                //we must continue trying to correct the rest of polylines
                                tooShortTx.Abort();
                                continue;
                            }
                            tooShortTx.Commit();
                        }
                    }

                    //Test to see if nexts polyline new start point is going to land in an arc
                    //If so, the correction stops as I don't know how to handle that
                    //The resulting point must be marked and user warned that the correction
                    //needs to be done manually. Then the correction must be run again.
                    SegmentType landingSegmentType = pline2.GetSegmentType(
                        (int)pline2.GetParameterAtDistance(missingLength));
                    if (landingSegmentType == SegmentType.Arc)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg =
                            $"***WARNING***\n" +
                            $"For polyline {pline2.Handle} length correction points ends in an Arc segment.\n" +
                            $"Automatic cut length correction is not possible. If you wish, correct lengths manually.\n" +
                            $"In that case run CORRECTCUTLENGTHSV2 afterwards again.";
                        DebugHelper.CreateDebugLine(
                            pline2.GetPointAtDist(missingLength), ColorByName("red"));
                        continue;
                    }

                    //Now to correcting lengths
                    var correctResult = CorrectPlines(db, pline1, br, pline2, missingLength, ps);
                    result.Combine(correctResult);
                    //Continue by falling through
                }
            }

            return result;
        }
        /// <summary>
        /// If next polyline is shorter than missing length, push the missing length down the line
        /// using recursion.
        /// </summary>
        /// <param name="pline1">
        /// The nextPline from where the need to do this arose.
        /// This polyline is the next from the currently processed as noted by PipelineSegment.
        /// </param>
        /// <param name="depth">
        /// Index to keep tabs on how far we have dived. Starts at 1
        /// because we already start at next polyline after the currently processing one.
        /// </param>
        /// <returns>
        /// The result of operation. 
        /// OK - ok to proceed, SoftError - pushing down the line cannot be done.
        /// </returns>
        private Result PushMissingLengthDownTheLine(
            Database db, Polyline pline1, PipelineSegment ps, double missingLength, int depth = 1)
        {
            depth++;
            Result result = new Result();
            //If unprocessed polylines is 1, we are at the end of the segment
            //and in current implementation we do not move last segment.

            //!!!!Remember: ps.UnprocessedPolylines refers to currently processed polyline
            //And we are working here with the NEXT polyline after it ON FIRST ITERATION

            if (ps.PolylinesLeft == 1)
            {
                throw new Exception(
                    $"PushMissingLengthDownTheLine encountered ps.UnprocessedPolylines == 1.\n" +
                    $"This is not the intention of the calling code, which assummes that we do not modify last polyline.");
            }
            //If nprocessedPolylines is 2, we are at the second to last polyline
            //and this means that nextPline is the last polyline in the segment
            //and we do not modify last segment, so the processing aborts.
            //This also means that we failed to push the missing length down the line.
            //And thus return SoftError.
            if (ps.PolylinesLeft == 2)
            {
                return new Result(
                    ResultStatus.SoftError,
                    "Pushing MissingLength reached the end of segment without finding space for missing length.");
            }

            //Remember, we are looking for CurrentProccessingPolyline + depth which starts at 2 in the first iteration
            Polyline pline2 = ps.PeekPolylineByRelativeIndex(depth);
            if (pline2 == null)
            {
                return new Result(
                    ResultStatus.SoftError,
                    "Segment does not have further polyline down the line. Pushing aborted.");
            }

            //Test if the moved point lands on arc
            if (missingLength > pline2.Length)
            {
                Result intermediateResult = PushMissingLengthDownTheLine(db, pline2, ps, missingLength, depth);
                if (intermediateResult.Status == ResultStatus.OK) //The pushing succeded down the line, push here then
                {
                    //Test again to see if modified endpoint lands on an arc
                    //Testing for where the endpoint lands
                    SegmentType landingSegmentType = pline2.GetSegmentType(
                                (int)pline2.GetParameterAtDistance(missingLength));
                    if (landingSegmentType == SegmentType.Arc)
                    {
                        DebugHelper.CreateDebugLine(
                            pline2.GetPointAtDist(missingLength), ColorByName("red"));
                        return new Result(
                            ResultStatus.SoftError,
                            "Moved end landed on arc segment after modifying later polylines. Pushing aborted.");
                    }
                    else
                    {
                        result.Combine(intermediateResult);
                        //Fall through to after the ifs for actual pushing
                    }

                    //Fall through to after the ifs for actual pushing
                }
                else //The pushing failed thus return result signaling failure
                {
                    result.Combine(intermediateResult);
                    return result;
                }
            }
            else //Polyline is not too short for operation
            {
                //Testing for where the endpoint lands
                //Change: this check is done in ModifyCurrentPlines

                //Fall through to after the ifs for actual pushing
            }
            var nextEnt = ps.PeekNextEntityByPolyline(pline1);
            BlockReference br = nextEnt as BlockReference;
            #region Validating next entity
            //Validating next entity
            if (nextEnt == null)
            {
                prdDbg($"No next entity after {pline1.Handle}. Length correction stopped.");
                return new Result(
                    ResultStatus.SoftError,
                    $"No next entity after {pline1.Handle}. Length correction stopped.");
            }
            if (nextEnt is Polyline pl)
            {
                throw new Exception($"Next entity after polyline {pline1.Handle} is a polyline {pl.Handle}! " +
                    $"This is not expected -- no consecutive polylines allowed in projects.");
            }
            if (br == null)
                throw new Exception($"Next entity after polyline {pline1.Handle} is not a block reference! " +
                    $"This is not expected. Offending element: {nextEnt.Handle}.");
            if (br.GetPipelineType() != PipelineElementType.Reduktion)
            {
                prdDbg($"Next entity {br.Handle} after {pline1.Handle} is not a reducer. " +
                    $"Length correction stopped.");
                return new Result(ResultStatus.SoftError,
                    $"Next entity {br.Handle} after {pline1.Handle} is not a reducer. " +
                    $"Length correction stopped.");
            }
            #endregion
            Result modifyResult = CorrectPlines(db, pline1, br, pline2, missingLength, ps);
            return modifyResult;
        }
        private Result CorrectPlines(
            Database db, Polyline pline1, BlockReference reducer, Polyline pline2, double missingLength,
            PipelineSegment ps, bool reverseResult = false)
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    //Now to correcting lengths
                    double transitionLength = Utils.GetTransitionLength(tx, reducer);
                    Point3d cachedTransitionLocation = reducer.Position;
                    double cachedPline1ConstantWidth = pline1.ConstantWidthSafe();
                    double cachedPline2ConstantWidth = pline2.ConstantWidthSafe();

                    SegmentType landingSegmentType = pline2.GetSegmentType(
                                (int)pline2.GetParameterAtDistance(missingLength));
                    if (landingSegmentType == SegmentType.Arc)
                    {
                        prdDbg(
                        $"***WARNING***\n" +
                                $"For polyline {pline2.Handle} length correction by {missingLength}m ends in an Arc segment.\n" +
                                $"Automatic cut length correction is not possible. If you wish, correct lengths manually.\n" +
                                $"In that case run CORRECTCUTLENGTHSV2 afterwards again.");
                        DebugHelper.CreateDebugLine(
                            pline2.GetPointAtDist(missingLength), ColorByName("red"));
                        tx.Commit();
                        return new Result(
                            ResultStatus.SoftError,
                            "Moved end landed on arc segment. Modify length aborted.");
                    }

                    //TODO: Maybe add a check if polylines really are connected to the reducer
                    //Pline1 with end point and pline2 with start point

                    #region Case 1. Missing length <= transitionLength
                    //Case 1: Missing length is less than or equal to transition length
                    if (missingLength <= transitionLength)
                    {
                        prdDbg($"R: {reducer.Handle}, Case 1: Missing length is less than or equal to transition length");
                        pline1.UpgradeOpen();
                        pline2.UpgradeOpen();
                        reducer.UpgradeOpen();

                        //Modify pline1
                        Vector3d moveVector = pline1.GetFirstDerivative(pline1.EndParam).GetNormal() * missingLength;
                        Point3d newEndPoint = pline1.GetPointAtParameter(pline1.EndParam) + moveVector;
                        pline1.AddVertexAt(pline1.NumberOfVertices, newEndPoint.To2d(), 0, 0, 0);
                        pline1.ConstantWidth = cachedPline1ConstantWidth;

                        //Modify pline2
                        //Use splitting to avoid checking what segments are in the way
                        //Though the probability of multiple segments on such short distance is low
                        //But one can never be sure
                        List<double> splitPars = new() { pline2.GetParameterAtDistance(missingLength) };
                        try
                        {
                            var objs = pline2.GetSplitCurves(splitPars.ToDoubleCollection());
                            if (objs.Count != 2)
                            {
                                prdDbg($"Splitting of polyline {pline2.Handle} at parameter " +
                                    $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                                throw new Exception("Splitting failed.");
                            }
                            Polyline newPline2 = objs[1] as Polyline;
                            if (newPline2 == null)
                            {
                                prdDbg($"Splitting of polyline {pline2.Handle} at parameter " +
                                    $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                                throw new Exception("Splitting failed.");
                            }
                            newPline2.AddEntityToDbModelSpace(db);

                            ps.ExchangeEntity(pline2, newPline2);
                            PropertySetManager.CopyAllProperties(pline2, newPline2);
                            newPline2.ConstantWidth = pline2.ConstantWidthSafe();
                            if (reverseResult) newPline2.ReverseCurve();
                            //Already opened
                            pline2.Erase(true);
                            pline2 = newPline2;
                        }
                        catch (Exception ex)
                        {
                            prdDbg($"Splitting of polyline {pline2.Handle} at parameter " +
                                $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                            prdDbg(ex);
                            throw;
                        }

                        //Move transition, it is already opened
                        reducer.TransformBy(Matrix3d.Displacement(moveVector));

#if DEBUG
                        DebugHelper.CreateDebugLine(cachedTransitionLocation, ColorByName("yellow"));
                        DebugHelper.CreateDebugLine(reducer.Position, ColorByName("green"));
#endif

                        //Finalize transaction
                        tx.Commit();
                        return new Result();
                    }
                    #endregion
                    #region Case 2: Missing length > transition length
                    //It is assumed
                    else
                    {
                        prdDbg($"R: {reducer.Handle}, Case 2: Missing length is greater than transition length");
                        pline1.UpgradeOpen();
                        pline2.UpgradeOpen();
                        reducer.UpgradeOpen();

                        Point3d cachedPline1EndPoint = pline1.EndPoint;

                        //First split the second pline to get the parts that pline1 is going to inherit
                        try
                        {
                            //Splitting in 3 parts
                            //as we remember the gap for the moved transition
                            List<double> splitPars = new()
                            {
                                pline2.GetParameterAtDistance(missingLength - transitionLength),
                                pline2.GetParameterAtDistance(missingLength),
                            };
                            var objs = pline2.GetSplitCurves(splitPars.ToDoubleCollection());
                            if (objs.Count != 3)
                            {
                                prdDbg($"Splitting of polyline {pline2.Handle} at parameters " +
                                    $"{pline2.GetParameterAtDistance(missingLength - transitionLength)}, " +
                                    $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                                throw new Exception("Splitting failed.");
                            }

                            //First modify pline1 by adding the part of pline2
                            //that is going to be inherited
                            Polyline inheritance = objs[0] as Polyline;
                            if (inheritance == null)
                            {
                                prdDbg($"Splitting of polyline {pline2.Handle} at parameter " +
                                    $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                                throw new Exception("Splitting failed.");
                            }
                            for (int i = 0; i < inheritance.NumberOfVertices; i++)
                            {
                                pline1.AddVertexAt(pline1.NumberOfVertices, inheritance.GetPoint2dAt(i), 0, 0, 0);
                            }
                            pline1.ConstantWidth = cachedPline1ConstantWidth;

                            //We ignore second part of the split as it is the part where the transition is placed

                            //Now consider thirt part of the split
                            //Add it to the db
                            //Copy ps properties
                            //Exchange the old pline2 with the new one in the segment
                            //Erase the old pline2
                            Polyline newPline2 = objs[2] as Polyline;
                            if (newPline2 == null)
                            {
                                prdDbg($"Splitting of polyline {pline2.Handle} at parameter " +
                                    $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                                throw new Exception("Splitting failed.");
                            }
                            newPline2.AddEntityToDbModelSpace(db);

                            ps.ExchangeEntity(pline2, newPline2);
                            PropertySetManager.CopyAllProperties(pline2, newPline2);
                            newPline2.ConstantWidth = pline2.ConstantWidthSafe();
                            if (reverseResult) newPline2.ReverseCurve();
                            //Already opened
                            pline2.Erase(true);
                            pline2 = newPline2;

                            //Now move the transition into place
                            Vector3d moveVector = cachedPline1EndPoint.GetVectorTo(pline1.EndPoint);
                            reducer.TransformBy(Matrix3d.Displacement(moveVector));

                            //Now rotate the transition to match the new direction
                            //This is done by rotating the transition around the new endpoint
                            var v1 = pline1.EndPoint.GetVectorTo(pline1.StartPoint);
                            var v2 = Utils.GetDirectionVectorForReducerFromPoint(tx, reducer, pline2.EndPoint);
                            double angle = v2.GetAngleTo(v1);
                            //reducer.TransformBy(Matrix3d.Rotation(angle, Vector3d.ZAxis, pline1.EndPoint));
                            tx.Commit();

#if DEBUG
                            DebugHelper.CreateDebugLine(cachedTransitionLocation, ColorByName("yellow"));
                            DebugHelper.CreateDebugLine(reducer.Position, ColorByName("green"));
#endif

                            return new Result();
                        }
                        catch (Exception ex)
                        {
                            prdDbg($"Splitting of polyline {pline2.Handle} at parameters " +
                                $"{pline2.GetParameterAtDistance(missingLength - transitionLength)}, " +
                                $"{pline2.GetParameterAtDistance(missingLength)} failed.");
                            prdDbg(ex);
                            throw;
                        }
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    prdDbg($"Pline1: {pline1.Handle}, Pline2: {pline2.Handle}, Br: {reducer.Handle}" +
                        $"ML: {missingLength}");
                    prdDbg(ex);
#if DEBUG
                    DebugHelper.CreateDebugLine(reducer.Position, ColorByName("red"));
#endif
                    tx.Abort();
                    return new Result(
                        ResultStatus.FatalError,
                        "Exception thrown.\n" +
                        ex.ToString());
                }
            }
        }
        private Result CorrectPlinesReverse(
            Database db, Polyline pline1, BlockReference reducer, Polyline pline2, double missingLength,
            PipelineSegment ps)
        {
            pline1.UpgradeOpen();
            pline2.UpgradeOpen();
            pline1.ReverseCurve();
            pline2.ReverseCurve();
            pline1.DowngradeOpen();
            pline2.DowngradeOpen();

            var result = CorrectPlines(db, pline2, reducer, pline1, missingLength, ps, true);

            //Pline1 should have been split in the method and thus no longer be relevant
            //No, wait, actually they need to be reversed if the operation was aborted.
            pline1.UpgradeOpen();
            pline2.UpgradeOpen();
            pline1.ReverseCurve();
            pline2.ReverseCurve();
            pline1.DowngradeOpen();
            pline2.DowngradeOpen();

            return result;
        }
    }
}