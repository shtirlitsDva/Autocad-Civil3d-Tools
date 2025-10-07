using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Topology
{
    internal class Segment
    {
        private PipeSystemEnum _pipeSystem;
        private PipeTypeEnum _pipeType;
        private PipeSeriesEnum _pipeSeries;
        private int _dn;
        private HashSet<Entity> _entities;
        private Polyline _topology;

        public Segment(
            PipeSystemEnum pipeSystem,
            PipeTypeEnum pipeType,
            PipeSeriesEnum pipeSeries,
            int dn,
            IEnumerable<Entity> entities,
            Polyline topology)
        {
            _pipeSystem = pipeSystem;
            _pipeType = pipeType;
            _pipeSeries = pipeSeries;
            _dn = dn;
            _entities = [.. entities];
            _topology = topology;
        }
    }
}