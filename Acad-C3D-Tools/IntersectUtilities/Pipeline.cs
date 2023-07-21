using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using static IntersectUtilities.PipeSchedule;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DataTable = System.Data.DataTable;

namespace IntersectUtilities
{
    internal class Pipeline : IEquatable<Pipeline>
    {
        public Alignment Alignment { get; set; }
        public Entity[] Entities { get; set; }
        public PipelineSizeArray Sizes { get; set; }
        public DataTable Table { get; }
        public int MaxDn { get => Sizes.MaxDn; }
        public int MinDn { get => Sizes.MinDn; }
        private Graph.Con[] Cons;
        private int _portCount = 0;
        public int PipelineNumber = 0;
        public Pipeline(Alignment alignment, IEnumerable<Entity> entities, DataTable table, int pipelineNumber)
        {
            Alignment = alignment;
            Table = table;
            PipelineNumber = pipelineNumber;
            if (entities == null || entities.Count() == 0)
                throw new Exception("No entities supplied!");
            Entities = entities.OrderBy(x => GetStation(x)).ToArray();

            var curves = entities.OfType<Curve>().ToHashSet();
            var brs = entities.OfType<BlockReference>().ToHashSet();

            if (brs.Count > 0)
                Sizes = new PipelineSizeArray(alignment, curves, brs);
            else Sizes = new PipelineSizeArray(alignment, curves);

            //Reverse entities if sizes SmallToLargeAscending
            if (Sizes.Direction == PipelineSizeArray.PipelineSizesDirection.SmallToLargeAscending)
                Entities = Entities.Reverse().ToArray();

            _labels = BuildLabel();

            //Parse and build connections
            PropertySetManager psmGraph = new PropertySetManager(Entities[0].Database,
                        PSetDefs.DefinedSets.DriGraph);
            PSetDefs.DriGraph driGraphDef = new PSetDefs.DriGraph();

            List<Graph.Con> cons = new List<Graph.Con>();

            foreach (var ent in Entities)
            {
                string conString = psmGraph.ReadPropertyString(ent, driGraphDef.ConnectedEntities);
                var list = Graph.GraphEntity.parseConString(conString);
                //Cache reference to entities own handle to be able to create connections
                foreach (var item in list) item.OwnHandle = ent.Handle;
                cons.AddRange(list);
            }

            //Clean cons from all own entities
            Cons = cons.Where(x => !Entities.Any(y => x.ConHandle == y.Handle)).ToArray();
        }
        private double GetStation(Entity entity)
        {
            double station = 0;
            double offset = 0;

            switch (entity)
            {
                case Polyline pline:
                    double l = pline.Length;
                    Point3d p = pline.GetPointAtDist(l / 2);
                    Alignment.StationOffset(p.X, p.Y, 5.0, ref station, ref offset);
                    break;
                case BlockReference block:
                    Alignment.StationOffset(block.Position.X, block.Position.Y, 5.0, ref station, ref offset);
                    break;
                default:
                    throw new Exception("Invalid entity type");
            }
            return station;
        }
        public bool IsConnectedTo(Pipeline other, double tolerance)
        {
            // Get the start and end points of the alignments
            Point3d thisStart = this.Alignment.StartPoint;
            Point3d thisEnd = this.Alignment.EndPoint;
            Point3d otherStart = other.Alignment.StartPoint;
            Point3d otherEnd = other.Alignment.EndPoint;

            // Check if any of the endpoints of this alignment are on the other alignment
            if (IsOn(other.Alignment, thisStart, tolerance) || IsOn(other.Alignment, thisEnd, tolerance))
                return true;

            // Check if any of the endpoints of the other alignment are on this alignment
            if (IsOn(this.Alignment, otherStart, tolerance) || IsOn(this.Alignment, otherEnd, tolerance))
                return true;

            // If none of the checks passed, the alignments are not connected
            return false;

            bool IsOn(Alignment al, Point3d point, double tol)
            {
                //double station = 0;
                //double offset = 0;

                //try
                //{
                //    alignment.StationOffset(point.X, point.Y, tolerance, ref station, ref offset);
                //}
                //catch (Exception) { return false; }

                Polyline pline = al.GetPolyline().Go<Polyline>(
                    al.Database.TransactionManager.TopTransaction, OpenMode.ForWrite);

                Point3d p = pline.GetClosestPointTo(point, false);
                pline.Erase(true);
                //prdDbg($"{offset}, {Math.Abs(offset)} < {tolerance}, {Math.Abs(offset) <= tolerance}, {station}");

                // If the offset is within the tolerance, the point is on the alignment
                if (Math.Abs(p.DistanceTo(point)) <= tol) return true;

                // Otherwise, the point is not on the alignment
                return false;
            }
        }
        private List<Label> _labels = new List<Label>();
        private List<string> _edges = new List<string>();
        private List<Label> BuildLabel()
        {
            List<Label> labels = new List<Label>();
            foreach (Entity entity in Entities)
            {
                Label label = new Label();
                switch (entity)
                {
                    case Polyline pline:
                        int dn = PipeSchedule.GetPipeDN(pline);
                        string system = GetPipeType(pline).ToString();
                        label.Handle = pline.Handle;
                        label.Description = $"Rør L{pline.Length.ToString("0.##")}";
                        label.SystemAndDN = $"{system} DN{dn}";
                        break;
                    case BlockReference br:
                        string dn1 = ReadComponentDN1Str(br, Table);
                        string dn2 = ReadComponentDN2Str(br, Table);
                        string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                        system = ComponentSchedule.ReadComponentSystem(br, Table);
                        string type = ComponentSchedule.ReadComponentType(br, Table);
                        label.Handle = br.Handle;
                        label.Description = type;
                        label.SystemAndDN = $"{system} DN{dnStr}";
                        break;
                    default:
                        continue;
                }
                labels.Add(label);
            }
            return labels;
        }
        public string GetLabel()
        {
            int maxHandleLength = _labels.Max(label => label.Handle.ToString().Length);
            int maxDescLength = _labels.Max(label => label.Description.Length);
            int maxSysAndDNLength = _labels.Max(label => label.SystemAndDN.Length);

            StringBuilder sb = new StringBuilder();

            foreach (Label label in _labels)
            {
                string handle = label.Handle.ToString().PadRight(maxHandleLength).Replace(" ", "\\ ");
                if (label.PortNumber != 0) handle = $"<p{label.PortNumber.ToString("D2")}> " + handle;
                string description = label.Description.PadRight(maxDescLength).Replace(" ", "\\ ");
                string sysAndDn = label.SystemAndDN.PadRight(maxSysAndDNLength).Replace(" ", "\\ ");

                sb.AppendLine($"|{{{handle}|{description}|{sysAndDn}}}");
            }

            return sb.ToString();
        }
        public string GetEdges()
        {
            return string.Join("\n", _edges);
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            int pipelineNameLength = $"| Pipeline: {Alignment.Name}".Length;
            int spacesNeeded = 48 - pipelineNameLength; // 48 is the total length of the line
            string spaces = new string(' ', spacesNeeded);

            // Print table header
            sb.AppendLine("-------------------------------------------------");
            sb.AppendLine($"| Pipeline: {Alignment.Name}{spaces}|");
            sb.AppendLine("-------------------------------------------------");
            sb.AppendLine("| Station            | Entity                   |");
            sb.AppendLine("-------------------------------------------------");

            // Print each entity and its station
            foreach (Entity entity in Entities)
            {
                double station = GetStation(entity);

                // Print the station and entity type in table format
                sb.AppendLine($"| {station,-18} | {entity.GetType().Name,-24} |");
            }

            // Print table footer
            sb.AppendLine("-------------------------------------------------");

            return sb.ToString();
        }
        public bool Equals(Pipeline other)
        {
            if (other == null) return false;
            return this.Alignment.Name.Equals(other.Alignment.Name);
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as Pipeline);
        }
        public override int GetHashCode()
        {
            return this.Alignment.Name.GetHashCode();
        }
        internal void EstablishConnections(List<GraphNodeV2> children)
        {
            foreach (var childNode in children)
            {
                Pipeline child = childNode.Node;

                foreach (Graph.Con con in Cons)
                {
                    if (child.Entities.Any(x => x.Handle == con.ConHandle))
                    {
                        var p1 = AddPort(con.OwnHandle);
                        var p2 = child.AddPort(con.ConHandle);

                        if (p1 != null && p2 != null)
                        {
                            _edges.Add($"node{PipelineNumber}:{p1} -> node{child.PipelineNumber}:{p2};");
                        }
                    }
                }
            }
        }
        public string AddPort(Handle handle)
        {
            var label = _labels.FirstOrDefault(x => x.Handle == handle);
            if (label == null) return null;
            if (label.PortNumber != 0) return $"p{label.PortNumber.ToString("D2")}";
            _portCount++;
            label.PortNumber = _portCount;
            return $"p{_portCount.ToString("D2")}";
        }
        private class Label
        {
            public Handle Handle { get; set; }
            public string Description { get; set; }
            public string SystemAndDN { get; set; }
            public int PortNumber { get; set; } = 0;
        }
    }
}