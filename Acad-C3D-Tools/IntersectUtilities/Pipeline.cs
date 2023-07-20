using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities
{
    internal class Pipeline
    {
        public Alignment Alignment { get; set; }
        public Entity[] Entities { get; set; }
        public Pipeline(Alignment alignment, IEnumerable<Entity> entities)
        {
            Alignment = alignment;
            if (entities == null || entities.Count() == 0)
                throw new Exception("No entities supplied!");
            Entities = entities.OrderBy(x => GetStation(x)).ToArray();
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
        public bool IsConnected(Pipeline other, double tolerance)
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
        }
        private bool IsOn(Alignment al, Point3d point, double tolerance)
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
            if (Math.Abs(p.DistanceTo(point)) <= tolerance) return true;

            // Otherwise, the point is not on the alignment
            return false;
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
    }
}
