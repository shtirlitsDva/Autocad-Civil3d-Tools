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
            IEnumerable<Entity> entities, bool againstStationsOrder, 
            PipeSettingsCollection psc, Point3d connectionLocation)
        {
            _segments = PipelineSegmentFactory.CreateSegments(entities);
            _pipeSettings = psc;
        }

        internal void CorrectLengths(Database localDb)
        {
            Database db = localDb;
            Transaction tx = db.TransactionManager.TopTransaction;

            //while (_ents.UnprocessedPolylines > 0)
            //{
            //    Polyline pl = _ents.ProcessNextPolyline();


            //}
        }
    }
}
