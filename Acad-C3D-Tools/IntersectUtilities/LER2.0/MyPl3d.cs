using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using static IntersectUtilities.LER2.MyPl3d;
using Autodesk.AutoCAD.MacroRecorder;

namespace IntersectUtilities.LER2
{
    public class MyPl3d
    {
        public Vertex3d this[int index] { get => Vertices[index]; }
        public LineSegment3d[] Segments { get => _segments; }
        private LineSegment3d[] _segments;
        public Handle Handle { get => _sourceHandle; }
        private Handle _sourceHandle;
        public string Layer { get; }
        public Extents3d GeometricExtents { get; }
        public Vertex3d[] Vertices { get; private set; }
        public Vertex3d[] VerticesWithoutStartAndEndpoints { get => Vertices.Skip(1).Take(Vertices.Count() - 2).ToArray(); }
        public Vector3d StartVector { get => _segments[0].Direction; }
        public Vector3d EndVector { get => _segments[_segments.Length - 1].Direction; }
        private void overWriteSegments(MyPl3d other)
        {
            this._segments = other._segments;
            this.Vertices = other.Vertices;
        }
        private void overWriteSegments(LineSegment3d[] otherSegments)
        {
            this._segments = otherSegments;

            //Re-Init vertices array
            Vertices = new Vertex3d[_segments.Length + 1];
            for (int i = 0; i < _segments.Length; i++)
            {
                Vector3d vecBefore = default(Vector3d);
                Vector3d vecAfter = _segments[i].Direction;
                if (i != 0) vecBefore = _segments[i - 1].Direction;
                Vertices[i] = new Vertex3d
                    (i, _segments[i].StartPoint, vecAfter, vecBefore);
            }

            Vertices[Vertices.Length - 1] =
                new Vertex3d(
                    Vertices.Length - 1,
                    _segments[Vertices.Length - 2].EndPoint,
                    default(Vector3d),
                    _segments[_segments.Length - 1].Direction);
        }
        public double Length { get => _segments.Sum(x => x.Length); }
        private Point3dCollection p3DCol { get => new Point3dCollection(Vertices.Select(x => x.Position).ToArray()); }
        private Tolerance _tolerance;
        public MyPl3d(Polyline3d source, Tolerance tolerance)
        {
            _sourceHandle = source.Handle;
            _tolerance = tolerance;
            Layer = source.Layer;
            GeometricExtents = source.GeometricExtents;

            Transaction tx = source.Database.TransactionManager.TopTransaction;
            var verts = source.GetVertices(tx);

            _segments = new LineSegment3d[verts.Length - 1];

            //Init segments array
            for (int i = 0; i < verts.Length - 1; i++)
                _segments[i] = new LineSegment3d(verts[i].Position, verts[i + 1].Position);

            //Init vertices array
            Vertices = new Vertex3d[_segments.Length + 1];
            for (int i = 0; i < _segments.Length; i++)
            {
                Vector3d vecBefore = default(Vector3d);
                Vector3d vecAfter = _segments[i].Direction;
                if (i != 0) vecBefore = _segments[i - 1].Direction;
                Vertices[i] = new Vertex3d
                    (i, _segments[i].StartPoint, vecAfter, vecBefore);
            }

            Vertices[Vertices.Length - 1] =
                new Vertex3d(
                    Vertices.Length - 1,
                    _segments[Vertices.Length - 2].EndPoint,
                    default(Vector3d),
                    _segments[_segments.Length - 1].Direction);
        }
        public OverlapType GetOverlapType(MyPl3d other)
        {
            var otherVerts = other.Vertices;
            if (otherVerts.All(
                x => _segments.Any(
                    y => y.IsOn(x.Position, _tolerance))))
                return OverlapType.Full;

            else if (otherVerts.Any(
                x => _segments.Any(
                    y => y.IsOn(x.Position, _tolerance))))
                return OverlapType.Partial;

            else return OverlapType.None;
        }
        public Point3d StartPoint { get => _segments[0].StartPoint; }
        public Point3d EndPoint { get => _segments[_segments.Length - 1].EndPoint; }
        public void Reverse()
        {
            Array.Reverse(_segments);
            for (int i = 0; i < _segments.Length; i++)
            {
                var curSeg = _segments[i];
                _segments[i] = new LineSegment3d(
                    curSeg.EndPoint, curSeg.StartPoint);
            }
        }
        public bool IsPointOn(Vertex3d vertex) => IsPointOn(vertex.Position);
        public bool IsPointOn(Point3d point)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                var curSeg = _segments[i];
                bool isOn = curSeg.IsOn(point, _tolerance);
                if (isOn) return isOn;
            }
            return false;
        }
        /// <summary>
        /// Merges a collection of overlapping polylines into a single polyline.
        /// WARNING: This method works only if all polylines in the collection OVERLAP.
        /// </summary>
        public Point3dCollection Merge(IEnumerable<MyPl3d> others)
        {
            Queue<MyPl3d> queue = new Queue<MyPl3d>(others);

            //prdDbg(this._sourceHandle);

            int safetyCounter = 0;
            while (queue.Count > 0)
            {
                var other = queue.Dequeue();
                var overlapType = GetOverlapType(other);
                //prdDbg(overlapType);
                switch (overlapType)
                {
                    case OverlapType.Full:
                        //Other is a subset of or equal to this
                        continue;
                    case OverlapType.Partial:
                        //0. Detect if this is a subset of the other
                        //   Which means that the other is longer and covers both ends of this
                        if (other.IsPointOn(StartPoint) && other.IsPointOn(EndPoint))
                        {
                            //prdDbg("This is a subset of the other");
                            //If this is a subset, then it safe to assume the supersets' segments
                            this.overWriteSegments(other);
                            continue; //Skip to the next iteration
                        }
                        //   The cases for when only one end is covered by the other
                        //1. Determine what end of the other is overlapping
                        bool otherStart = IsPointOn(other.StartPoint);
                        bool otherEnd = IsPointOn(other.EndPoint);
                        //2. Determine what end of this is overlapping
                        bool thisStart = other.IsPointOn(StartPoint);
                        bool thisEnd = other.IsPointOn(EndPoint);
                        //3. Determine the direction of the overlap
                        OverlapDirection direction = OverlapDirection.None;
                        if (otherStart && thisEnd) direction = OverlapDirection.OtherStartThisEnd;
                        else if (otherEnd && thisEnd) direction = OverlapDirection.OtherEndThisEnd;
                        else if (otherStart && thisStart) direction = OverlapDirection.OtherStartThisStart;
                        else if (otherEnd && thisStart) direction = OverlapDirection.OtherEndThisStart;
                        else throw new Exception("Overlap direction not found! " +
                            $"{_sourceHandle}, {string.Join(", ", others.Select(x => x._sourceHandle))}");

                        //prdDbg(direction);
                        switch (direction)
                        {
                            case OverlapDirection.None:
                                break;
                            case OverlapDirection.OtherEndThisEnd:
                                other.Reverse();
                                direction = OverlapDirection.OtherStartThisEnd;
                                goto case OverlapDirection.OtherStartThisEnd;
                            case OverlapDirection.OtherStartThisEnd: //co-directional
                                {
                                    List<LineSegment3d> newSegs = new List<LineSegment3d>();
                                    //Start by adding this segments to the new collection
                                    newSegs.AddRange(_segments);

                                    //Add the segments of the other to the end of this
                                    bool foundUnMerged = false;
                                    for (int i = 0; i < other._segments.Length; i++)
                                    {
                                        var otherSeg = other._segments[i];

                                        //Cases:
                                        //1. The other segment is completely free of this
                                        //2. The other segment is partially free of this
                                        //3. The other segment is completely covered by this

                                        if (foundUnMerged)
                                        {
                                            newSegs.Add(otherSeg);
                                            continue;
                                        }

                                        //Testing for Other start point on this is meaningless
                                        //It must be outside of this, else it's an error
                                        bool isOtherSegEndOnThis = IsPointOn(otherSeg.EndPoint);
                                        if (isOtherSegEndOnThis) continue; //Case 3
                                        else
                                        {
                                            newSegs.Add(new LineSegment3d(EndPoint, otherSeg.EndPoint)); //Case 2.
                                            foundUnMerged = true;
                                        }
                                    }

                                    overWriteSegments(newSegs.ToArray());
                                }
                                break;
                            case OverlapDirection.OtherStartThisStart:
                                other.Reverse();
                                direction = OverlapDirection.OtherEndThisStart;
                                goto case OverlapDirection.OtherEndThisStart;
                            case OverlapDirection.OtherEndThisStart: //co-directional
                                //Add the segments of the other to the start of this
                                {
                                    List<LineSegment3d> newSegs = new List<LineSegment3d>();
                                    bool mergingDone = false;
                                    for (int i = 0; i < other._segments.Length; i++)
                                    {
                                        var otherSeg = other._segments[i];

                                        //Cases:
                                        //1. The other segment is completely free of this
                                        //2. The other segment is partially free of this
                                        //3. The other segment is completely covered by this, but this shouldn't happen

                                        //Testing for Other start point on this is meaningless
                                        //It must be outside of this, else it's an error
                                        bool isOtherSegEndOnThis = IsPointOn(otherSeg.EndPoint);
                                        if (!isOtherSegEndOnThis) newSegs.Add(otherSeg); //Case 1.
                                        else
                                        {
                                            newSegs.Add(new LineSegment3d(otherSeg.StartPoint, StartPoint)); //Case 2.
                                            mergingDone = true;
                                        }

                                        if (mergingDone) break;
                                    }

                                    //Now add this segments to the new collection
                                    newSegs.AddRange(_segments);
                                    overWriteSegments(newSegs.ToArray());
                                }
                                break;
                        }
                        break;
                    case OverlapType.None:
                        queue.Enqueue(other);
                        break;
                }

                safetyCounter++;
                if (safetyCounter > 1000)
                    throw new Exception("Safety counter exceeded! " +
                        $"{_sourceHandle}, {string.Join(", ", others.Select(x => x._sourceHandle))}");
            }

            //Return the resulting points
            return p3DCol;
        }
        private enum OverlapDirection
        {
            None,
            OtherStartThisEnd, //Start of other overlaps end of this -> co-directional
            OtherEndThisEnd, //End of other overlaps end of this -> counter-directional
            OtherStartThisStart, //Start of other overlaps start of this -> counter-directional
            OtherEndThisStart //End of other overlaps start of this -> co-directional
        }
        public enum OverlapType
        {
            None,
            Partial,
            Full
        }
        public static bool operator ==(MyPl3d pl3d1, MyPl3d pl3d2)
        {
            if (ReferenceEquals(pl3d1, pl3d2))
            {
                return true;
            }

            if (ReferenceEquals(pl3d1, null) || ReferenceEquals(pl3d2, null))
            {
                return false;
            }

            return pl3d1.Handle == pl3d2.Handle;
        }
        public static bool operator !=(MyPl3d pl3d1, MyPl3d pl3d2)
        {
            return !(pl3d1 == pl3d2);
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            MyPl3d other = obj as MyPl3d;
            if (other == null)
            {
                return false;
            }

            return this.Handle == other.Handle;
        }
        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
    }
    public struct Vertex3d
    {
        public int Index { get; }
        public Point3d Position { get; }
        public double X { get => Position.X; }
        public double Y { get => Position.Y; }
        public double Z { get => Position.Z; }
        public Vector3d DirectionAfter { get; }
        public Vector3d DirectionBefore { get; }
        public Vertex3d(int index, Point3d position, Vector3d after, Vector3d before)
        {
            Index = index;
            Position = position;
            DirectionAfter = after;
            DirectionBefore = before;
        }
        public bool IsEqualTo(Vertex3d other, Tolerance tolerance) =>
            this.Position.IsEqualTo(other.Position, tolerance);
    }
    public class MyPl3dHandleComparer : IEqualityComparer<MyPl3d>
    {
        public bool Equals(MyPl3d x, MyPl3d y)
        {
            if (x == null || y == null)
                return false;

            return x.Handle == y.Handle;
        }

        public int GetHashCode(MyPl3d obj)
        {
            if (obj == null)
                return 0;

            return obj.Handle.GetHashCode();
        }
    }
    public static class Overlapvalidator
    {
        public static HashSet<HashSet<SerializablePolyline3d>> ValidateOverlaps(
            HashSet<HashSet<SerializablePolyline3d>> groups, Tolerance tolerance)
        {
            HashSet<HashSet<SerializablePolyline3d>> result = new HashSet<HashSet<SerializablePolyline3d>>();

            int groupCount = 0;
            foreach (var group in groups)
            {
                var mypl3ds = group.Select(x => new MyPl3d(x.GetPolyline3d(), tolerance)).ToList();

                // List to hold the connected components (i.e., groups of overlapping polylines)
                List<HashSet<MyPl3d>> connectedComponents = new List<HashSet<MyPl3d>>();

                foreach (var pl3d1 in mypl3ds)
                {
                    // Find which connected components pl3d1 belongs to (it could be more than one due to partial overlaps)
                    List<HashSet<MyPl3d>> belongingComponents = new List<HashSet<MyPl3d>>();
                    foreach (var component in connectedComponents)
                    {
                        if (component.Any(pl3d2 => pl3d1.GetOverlapType(pl3d2) != OverlapType.None))
                        {
                            belongingComponents.Add(component);
                        }
                    }

                    // Merge all belonging components into a single one and add pl3d1 to it
                    if (belongingComponents.Count > 0)
                    {
                        var merged = new HashSet<MyPl3d>();
                        foreach (var component in belongingComponents)
                        {
                            merged.UnionWith(component);
                            connectedComponents.Remove(component);
                        }
                        merged.Add(pl3d1);
                        connectedComponents.Add(merged);
                    }
                    else
                    {
                        // If pl3d1 doesn't belong to any existing component, start a new one
                        var newComponent = new HashSet<MyPl3d> { pl3d1 };
                        connectedComponents.Add(newComponent);
                    }
                }

                // Add the connected components to the result
                foreach (var component in connectedComponents)
                {
                    if (component.Count > 1)
                    {
                        groupCount--;
                        result.Add(new HashSet<SerializablePolyline3d>(component.Select(
                            x => new SerializablePolyline3d(x.Handle, groupCount))));
                    }
                }
            }

            return result;
        }
    }
}
