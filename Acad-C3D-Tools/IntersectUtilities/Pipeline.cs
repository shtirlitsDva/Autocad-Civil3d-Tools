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
        public Pipeline(Alignment alignment, IEnumerable<Entity> entities, DataTable table)
        {
            Alignment = alignment;
            Table = table;
            if (entities == null || entities.Count() == 0)
                throw new Exception("No entities supplied!");
            Entities = entities.OrderBy(x => GetStation(x)).ToArray();

            var curves = entities.OfType<Curve>().ToHashSet();
            var brs = entities.OfType<BlockReference>().ToHashSet();

            if (brs.Count > 0)
                Sizes = new PipelineSizeArray(alignment, curves, brs);
            else Sizes = new PipelineSizeArray(alignment, curves);
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
        public string GetLabel()
        {
            StringBuilder sb = new StringBuilder();
            List<List<string>> labels = new List<List<string>>();

            // First, generate all labels and find the maximum length of each part
            foreach (Entity entity in Entities)
            {
                List<string> parts = new List<string>();
                switch (entity)
                {
                    case Polyline pline:
                        int dn = PipeSchedule.GetPipeDN(pline);
                        string system = GetPipeType(pline).ToString();
                        parts = new List<string> { pline.Handle.ToString(), $"Rør L{pline.Length.ToString("0.##")}", $"{system} DN{dn}" };
                        break;
                    case BlockReference br:
                        string dn1 = ReadComponentDN1Str(br, Table);
                        string dn2 = ReadComponentDN2Str(br, Table);
                        string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                        system = ComponentSchedule.ReadComponentSystem(br, Table);
                        string type = ComponentSchedule.ReadComponentType(br, Table);
                        parts = new List<string> { br.Handle.ToString(), type, $"{system} DN{dnStr}" };
                        break;
                    default:
                        continue;
                }
                labels.Add(parts);
            }

            // Find the maximum length of each part
            List<int> maxLengths = new List<int>();
            for (int i = 0; i < labels[0].Count; i++)
            {
                int maxLength = labels.Max(label => label[i].Length);
                maxLengths.Add(maxLength);
            }

            // Now generate the output with right padding for each part
            foreach (List<string> parts in labels)
            {
                List<string> paddedParts = new List<string>();
                for (int i = 0; i < parts.Count; i++)
                {
                    // Calculate the amount of padding needed
                    int paddingNeeded = maxLengths[i] - parts[i].Length;

                    // Generate the padded part
                    string paddedPart = parts[i] + new string(' ', paddingNeeded);

                    // Replace spaces with escaped spaces
                    paddedPart = paddedPart.Replace(" ", "\\ ");

                    paddedParts.Add(paddedPart);
                }

                // Combine the parts into a single label and append to the output
                sb.AppendLine($"|{{{string.Join("|", paddedParts)}}}");
            }

            return sb.ToString();
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
    }
}
