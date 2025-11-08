using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace NTRExport.Spines
{
    internal sealed class SpinePath
    {
        private readonly List<SpineSegment> _segments = new();

        public SpinePath(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public IReadOnlyList<SpineSegment> Segments => _segments;

        public void Add(SpineSegment segment)
        {
            _segments.Add(segment);
        }

        public double TotalLength
        {
            get
            {
                double sum = 0.0;
                foreach (var s in _segments) sum += s.Length;
                return sum;
            }
        }

        public Point3d? StartPoint => _segments.Count == 0 ? null : _segments[0].A;
        public Point3d? EndPoint => _segments.Count == 0 ? null : _segments[^1].B;
    }
}


