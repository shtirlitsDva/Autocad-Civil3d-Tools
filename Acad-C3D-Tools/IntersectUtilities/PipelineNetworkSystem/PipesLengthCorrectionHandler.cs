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
                    //prdDbg($"{pline.Handle} ML:{missingLength} R:{remainder} RI:{pipeStdLength - remainder}");
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

                    //Test to see if nexts polyline new start point is going to land in an arc
                    //If so, the correction stops as I don't know how to handle that
                    //The resulting point must be marked and user warned that the correction
                    //needs to be done manually. Then the correction must be run again.

                }
            }
        }

        private class 
    }
}