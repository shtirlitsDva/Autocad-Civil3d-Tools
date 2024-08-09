using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using System;
using System.Collections.Generic;
using System.Linq;
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
                while (ps.UnprocessedPolylines > 1)
                {
                    Polyline pline1 = ps.ProcessNextPolyline();

                    double pipeStdLength = _pipeSettings.GetSettingsLength(pline1);
                    //We don't want to correct 100 m pipes
                    if (pipeStdLength > 99) continue;
                    double pipeLength = pline1.Length;
                    double division = pipeLength / pipeStdLength;
                    int nrOfSections = (int)division;
                    double remainder = pipeLength - nrOfSections * pipeStdLength;
                    double missingLength = pipeStdLength - remainder;

                    //Skip if pipe is missing less than 1 mm
                    if (remainder < 1e-3 || pipeStdLength - remainder < 1e-3)
                    {
                        prdDbg($"Pipe {pline1.Handle} is OK. {remainder} {pipeStdLength - remainder}");
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
                    if (missingLength >= pipeStdLength - 2 && nrOfSections != 0)
                    {

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
                        continue;
                    }
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

            if (ps.UnprocessedPolylines == 1)
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
            if (ps.UnprocessedPolylines == 2)
            {
                result.Combine(new Result(
                    ResultStatus.SoftError,
                    "Pushing MissingLength reached the end of segment without finding space for missing length."));
                return result;
            }

            //Remember, we are looking for CurrentProccessingPolyline + depth which starts at 2 in the first iteration
            Polyline pline2 = ps.PeekPolylineByRelativeIndex(depth);
            if (pline2 == null)
            {
                result.Combine(new Result(
                    ResultStatus.SoftError,
                    "Segment does not have further polyline down the line. Pushing aborted."));
                return result;
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
                        result.Combine(new Result(
                            ResultStatus.SoftError,
                            "Moved end landed on arc segment after modifying later polylines. Pushing aborted."));
                        Debug.CreateDebugLine(
                            pline2.GetPointAtDist(missingLength), ColorByName("red"));
                        return result;
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
            Result modifyResult = CorrectPlines(db, pline1, br, pline2, missingLength);
            return modifyResult;
        }

        private Result CorrectPlines(
            Database db, Polyline pline1, BlockReference reducer, Polyline pline2, double missingLength)
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    //Now to correcting lengths
                    double transitionLength = Utils.GetTransitionLength(tx, reducer);

                    SegmentType landingSegmentType = pline2.GetSegmentType(
                                (int)pline2.GetParameterAtDistance(missingLength));
                    if (landingSegmentType == SegmentType.Arc)
                    {
                        prdDbg(
                        $"***WARNING***\n" +
                                $"For polyline {pline2.Handle} length correction points ends in an Arc segment.\n" +
                                $"Automatic cut length correction is not possible. If you wish, correct lengths manually.\n" +
                                $"In that case run CORRECTCUTLENGTHSV2 afterwards again.");
                        Debug.CreateDebugLine(
                            pline2.GetPointAtDist(missingLength), ColorByName("red"));
                        return new Result(
                            ResultStatus.SoftError,
                            "Moved end landed on arc segment. Modify length aborted.");
                    }

                    #region Case 1. Missing length <= transitionLength



                    #endregion
                }
                catch (Exception ex)
                {
                    prdDbg($"Pline1: {pline1.Handle}, Pline2: {pline2.Handle}, Br: {reducer.Handle}" +
                        $"ML: {missingLength}");
                    prdDbg(ex);
                    tx.Abort();
                    return new Result(
                        ResultStatus.FatalError,
                        "Exception thrown.");
                }
                tx.Commit();
                return new Result();
            }
        }

        private void Case1(Polyline firstPoly, Polyline secondPoly, BlockReference transition)
        {
            Vector3d v = firstPoly.GetFirstDerivative(firstPoly.EndParam);
        }

    }
}