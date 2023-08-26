using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LER2
{
    public class MyPl3d
    {
        private LineSegment3d[] _segments;
        public Handle Handle { get => _sourceHandle; }
        private Handle _sourceHandle;
        public string Layer { get; }
        public Extents3d GeometricExtents { get; }
        public Point3d[] Vertices { get => getVertices(); }
        public Vector3d StartVector { get => _segments[0].Direction; }
        public Vector3d EndVector { get => _segments[_segments.Length - 1].Direction; }
        private Point3dCollection p3DCol { get => new Point3dCollection(Vertices); }
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

            for (int i = 0; i < verts.Length - 1; i++)
                _segments[i] = new LineSegment3d(verts[i].Position, verts[i + 1].Position);
            ;
        }
        public OverlapType GetOverlapType(MyPl3d other)
        {
            var otherVerts = other.Vertices;
            if (otherVerts.All(
                x => _segments.Any(
                    y => y.IsOn(x, _tolerance))))
                return OverlapType.Full;

            else if (otherVerts.Any(
                x => _segments.Any(
                    y => y.IsOn(x, _tolerance))))
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
        /// WARNING: This method works only if all polylines in the collection overlap.
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
                prdDbg(overlapType);
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
                            this._segments = other._segments;
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

                                    _segments = newSegs.ToArray();
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
                                    _segments = newSegs.ToArray();
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
        private Point3d[] getVertices()
        {
            Point3d[] vertices = new Point3d[_segments.Length + 1];
            for (int i = 0; i < _segments.Length; i++)
                vertices[i] = _segments[i].StartPoint;
            vertices[vertices.Length - 1] = _segments[vertices.Length - 2].EndPoint;
            return vertices;
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
}
