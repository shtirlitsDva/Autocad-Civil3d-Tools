using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegment
    {
        private readonly Entity[] _ents;
        public int UnprocessedPolylines { get; private set; }
        private int[] _polyIndici;
        private int _currentPolyIndex = -1;

        public PipelineSegment(List<Entity> segmentMembers)
        {
            _ents = segmentMembers.ToArray();
            UnprocessedPolylines = _ents.Count(x => x is Polyline);
            _polyIndici = _ents
                .Select((x, i) => new { x, i })
                .Where(x => x.x is Polyline)
                .Select(x => x.i)
                .ToArray();
        }
        public Polyline ProcessNextPolyline()
        {
            _currentPolyIndex++;
            if (_currentPolyIndex >= _polyIndici.Length) return null;
            UnprocessedPolylines--;
            return _ents[_polyIndici[_currentPolyIndex]] as Polyline;
        }

        internal Entity PeekNextEntity()
        {
            int idx = _polyIndici[_currentPolyIndex] + 1;
            if (idx >= _ents.Length) return null;
            return _ents[idx];
        }

        internal Polyline PeekNextPolyline()
        {
            int idx = _currentPolyIndex + 1;
            if (idx >= _polyIndici.Length) return null;
            return _ents[_polyIndici[idx]] as Polyline;
        }
        /// <summary>
        /// Takes an index relative to currently processed polyline,
        /// gets the specified polyline and peeks at the next element
        /// after it.
        /// </summary>
        internal Entity PeekNextEntityByRelativeIndex(int relativeIndex)
        {   
            int newPolyIdx = _currentPolyIndex + relativeIndex;
            if (newPolyIdx >= _polyIndici.Length) return null;
            int newEntIdx = _polyIndici[newPolyIdx] + 1;
            if (newEntIdx >= _ents.Length) return null;
            return _ents[newEntIdx];
        }
        internal Polyline PeekPolylineByRelativeIndex(int relativeIndex)
        {
            int newPolyIdx = _currentPolyIndex + relativeIndex;
            if (newPolyIdx >= _polyIndici.Length) return null;
            return _ents[newPolyIdx] as Polyline;
        }
    }
    internal static class PipelineSegmentFactory
    {
        /// <summary>
        /// Traverses the entities by geometric end connections
        /// and establishes segments of entities that are delimited
        /// by non-reducer entities.
        /// Entities are expected to be ordered in the correct order.
        /// It is assumed that the polylines are correctly oriented.
        /// Which should be handled by autoreversev2.
        /// The segments contain stretches of polylines that are
        /// routed over reduceres. If polyline starts and terminates
        /// at a non-reducer, it is discarded.
        /// </summary>
        public static List<PipelineSegment> CreateSegments(IEnumerable<Entity> entities)
        {
            List<PipelineSegment> psList = new List<PipelineSegment>();

            var originalList = entities.ToList();

            while (originalList.Any(x => x is Polyline) && originalList.Count(x => x is Polyline) > 1)
            {
                List<Entity> segmentMembers = new List<Entity>();

                var seedPoly = originalList.First(x => x is Polyline);

                Stack<Entity> stack = new Stack<Entity>();
                stack.Push(seedPoly);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    segmentMembers.Add(current);

                    if (current is BlockReference br)
                    {
                        var type = br.GetPipelineType();
                        //if type is not reduktion, break as segment ends here
                        if (type != PipelineElementType.Reduktion) break;
                    }

                    //Get next element
                    var next = GetNextElement(current, originalList, segmentMembers);
                    if (next != null) stack.Push(next);
                }

                originalList.RemoveAll(x => segmentMembers.Contains(x));
                if (segmentMembers.Count(
                    x => x is Polyline) > 1)
                    psList.Add(new PipelineSegment(segmentMembers));
            }
            return psList;
        }

        /// <summary>
        /// Assumes that the polylines are correctly oriented.
        /// For polylines only looking for end nodes.
        /// </summary>
        private static Entity GetNextElement(
            Entity current, List<Entity> originalList, List<Entity> alreadyFound)
        {
            switch (current)
            {
                case Polyline pl:
                    {
                        Point3d p = pl.EndPoint;
                        //Assuming that the polylines are never connected to other polylines
                        var query = originalList
                            .Where(x => x is BlockReference)
                            .Cast<BlockReference>()
                            .Select(x => new
                            {
                                Ends = x.GetAllEndPoints(),
                                Br = x
                            })
                            .Where(x => x.Ends.Contains(p, new Point3dHorizontalComparer(100)));
                        return query.FirstOrDefault()?.Br;
                    }
                case BlockReference br:
                    {
                        var ps = br.GetAllEndPoints();
                        var query = originalList
                            .Where(x => !alreadyFound.Contains(x))
                            .Select(x =>
                            {
                                switch (x)
                                {
                                    case Polyline pl:
                                        return new
                                        {
                                            Ends = new HashSet<Point3d>() { pl.StartPoint, pl.EndPoint },
                                            Ent = x
                                        };
                                    case BlockReference b:
                                        return new
                                        {
                                            Ends = b.GetAllEndPoints(),
                                            Ent = x
                                        };
                                }
                                return null;
                            })
                            .Where(x => ps.Any(y => x.Ends.Contains(y, new Point3dHorizontalComparer(100))));
                        return query.FirstOrDefault()?.Ent;
                    }
            }
            return null;
        }
    }
}
