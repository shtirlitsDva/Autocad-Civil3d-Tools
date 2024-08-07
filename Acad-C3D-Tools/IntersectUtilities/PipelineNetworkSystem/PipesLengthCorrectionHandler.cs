using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        internal void CorrectLengths(Database localDb)
        {
            Database db = localDb;
            Transaction tx = db.TransactionManager.TopTransaction;

            foreach (PipelineSegment ps in _segments)
            {
                while (ps.UnprocessedPolylines > 0)
                {
                    Polyline pline = ps.ProcessNextPolyline();


                }
            }
        }
    }
}
