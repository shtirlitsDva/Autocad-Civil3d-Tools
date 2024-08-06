using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegment 
    {
        public int UnprocessedPolylines { get; private set; }
        private int[] _polyIndici;
        private int _currentPolyIndex = -1;

        public PipelineSegment() { }

    }

    internal static class PipelineSegmentFactory
    {
        /// <summary>
        /// Entities are expected to be ordered in the correct order.
        /// </summary>
        public static List<PipelineSegment> CreateSegments(IEnumerable<Entity> entities)
        {
            List<PipelineSegment> _list = new List<PipelineSegment>();

            return _list;
        }
    }
}
