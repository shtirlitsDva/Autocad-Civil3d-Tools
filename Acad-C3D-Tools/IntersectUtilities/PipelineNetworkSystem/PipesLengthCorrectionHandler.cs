using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    Polyline pline = ps.ProcessNextPolyline();

                    double pipeStdLength = _pipeSettings.GetSettingsLength(pline);
                    double pipeLength = pline.Length;
                    double division = pipeLength / pipeStdLength;
                    int nrOfSections = (int)division;
                    double remainder = pipeLength - nrOfSections * pipeStdLength;
                    double missingLength = pipeStdLength - remainder;

                    //Skip if pipe is missing less than 1 mm
                    if (remainder < 1e-3 || pipeStdLength - remainder < 1e-3)
                    {
                        prdDbg($"Pipe {pline.Handle} is OK. {remainder} {pipeStdLength - remainder}");
                        continue;
                    }
                    prdDbg($"{pline.Handle} ML:{missingLength} R:{remainder} RI:{pipeStdLength - remainder}");
                    var nextEnt = ps.PeekNextEntity();
                    BlockReference br = nextEnt as BlockReference;
                    #region Validating next entity
                    //Validating next entity
                    if (nextEnt == null)
                    {
                        prdDbg($"No next entity after {pline.Handle}. Length correction stopped.");
                        break;
                    }
                    if (nextEnt is Polyline pl)
                    {
                        throw new Exception($"Next entity after polyline {pline.Handle} is a polyline {pl.Handle}! " +
                            $"This is not expected.");
                    }
                    if (br == null)
                        throw new Exception($"Next entity after polyline {pline.Handle} is not a block reference! " +
                            $"This is not expected.");
                    if (br.GetPipelineType() != PipelineElementType.Reduktion)
                    {
                        prdDbg($"Next entity {br.Handle} after {pline.Handle} is not a reducer. Length correction stopped.");
                        break;
                    }
                    #endregion

                    Polyline nextPline = ps.PeekNextPolyline();
                    #region Validate next polyline
                    if (nextPline == null)
                    {
                        prdDbg($"No next polyline after {pline.Handle}. Length correction stopped.");
                        break;
                    }
                    #endregion

                    //Now to correcting lengths
                    double transitionLength = Utils.GetTransitionLength(tx, br);

                    //Test to see if the next polylines' length is less than the missing length
                    //If so, the correction tries to check next polyline again and so on
                    //So we 'push' the missing length down the line until we find space
                    //Or segment terminates
                    //This must be done before all else


                    //Test to see if nexts polyline new start point is going to land in an arc
                    //If so, the correction stops as I don't know how to handle that
                    //The resulting point must be marked and user warned that the correction
                    //needs to be done manually. Then the correction must be run again.
                    SegmentType landingSegmentType = nextPline.GetSegmentType(
                        (int)nextPline.GetParameterAtDistance(missingLength));
                    if (landingSegmentType == SegmentType.Arc)
                    {
                        result.Status = ResultStatus.SoftError;
                        result.ErrorMsg =
                            $"***WARNING***\n" +
                            $"For polyline {nextPline.Handle} length correction points ends in an Arc segment.\n" +
                            $"Automatic cut length correction is not possible. If you wish, correct lengths manually.\n" +
                            $"In that case run CORRECTCUTLENGTHSV2 afterwards again.";
                        continue;
                    }

                    #region Case 1. Missing length <= transitionLength
                    
                    #endregion
                }
            }

            return result;
        }
        /// <summary>
        /// If next polyline is shorter than missing length, push the missing length down the line
        /// using recursion.
        /// </summary>
        /// <param name="nextPline">The nextPline from where the need to do this arose.</param>
        /// <returns>
        /// The result of operation. 
        /// OK - ok to proceed, SoftError - pushing down the line cannot be done.
        /// </returns>
        private Result PushMissingLengthDownTheLine(
            Transaction tx, Polyline nextPline, PipelineSegment ps, double missingLength, int depth = 1)
        {
            depth++;
            //If unprocessed polylines is 1, we are at the end of the segment
            //and in current implementation we do not move last segment.
            if (ps.UnprocessedPolylines == 1)
            { 
                throw new Exception(
                    $"PushMissingLengthDownTheLine encountered ps.UnprocessedPolylines == 1.\n" +
                    $"This shouldn't be possible as we fail already at 2."); 
            }
            //If nprocessedPolylines is 2, we are at the second to last polyline
            //and this means that nextPline is the last polyline in the segment
            //and we do not modify last segment, so the processing aborts.
            //This also means that we failed to push the missing length down the line.
            //And thus return SoftError.
            if (ps.UnprocessedPolylines == 2)
                return new Result(
                    ResultStatus.SoftError,
                    "Pushing MissingLength reached the end of segment without finding space for missing length.");


        }

        private void Case1(Polyline firstPoly, Polyline secondPoly, BlockReference transition)
        {
            Vector3d v = firstPoly.GetFirstDerivative(firstPoly.EndParam);
        }

    }
}