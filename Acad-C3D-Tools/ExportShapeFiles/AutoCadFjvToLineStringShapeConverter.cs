using Autodesk.AutoCAD.DatabaseServices;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Features;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.DynamicBlocks;
using IntersectUtilities;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace ExportShapeFiles
{
    public static class PolylineFjvToShapeLineStringConverter
    {
        public static Feature Convert(Entity entity)
        {
            if (!(entity is Polyline pline))
                throw new ArgumentException($"Entity {entity.Handle} is not a polyline!");

            string color = AutocadColors.GetHexColor(GetLayerColor(entity));

            var props = new AttributesTable
                {
                    { "System", GetPipeSystem(entity).ToString() },
                    { "Type", GetPipeType(entity).ToString() },
                    { "Serie", GetPipeSeriesV2(entity).ToString() },
                    { "NominalDia", GetPipeDN(entity) },
                    { "OD", GetPipeOd(entity) },
                    { "ID", GetPipeId(entity) },
                    { "KappeOD", GetPipeKOd(entity) },
                    { "color", color }
                };

            if (pline.Closed) throw new System.NotSupportedException(
            $"Polyline {pline.Handle} is closed! Closed polylines are not supported yet!");
            if (pline.Length < 0.01) throw new System.NotSupportedException(
                $"Polyline {pline.Handle} is too short! Polylines shorter than 0.01m are not allowed!");

            List<Point2d> points = new List<Point2d>();
            int numOfVert = pline.NumberOfVertices - 1;
            if (pline.Closed) numOfVert++;
            for (int i = 0; i < numOfVert; i++)
            {
                switch (pline.GetSegmentType(i))
                {
                    case SegmentType.Line:
                        LineSegment2d ls = pline.GetLineSegment2dAt(i);
                        if (i == 0)
                        {//First iteration
                            points.Add(ls.StartPoint);
                        }
                        points.Add(ls.EndPoint);
                        break;
                    case SegmentType.Arc:
                        CircularArc2d arc = pline.GetArcSegment2dAt(i);
                        double sPar = arc.GetParameterOf(arc.StartPoint);
                        double ePar = arc.GetParameterOf(arc.EndPoint);
                        double length = arc.GetLength(sPar, ePar);
                        double radians = length / arc.Radius;
                        int nrOfSamples = (int)(radians / 0.04);
                        if (nrOfSamples < 3)
                        {
                            if (i == 0) points.Add(arc.StartPoint);
                            points.Add(arc.EndPoint);
                        }
                        else
                        {
                            Point2d[] samples = arc.GetSamplePoints(nrOfSamples);
                            if (i != 0) samples = samples.Skip(1).ToArray();
                            foreach (Point2d p2d in samples) points.Add(p2d);
                        }
                        break;
                    case SegmentType.Coincident:
                    case SegmentType.Point:
                    case SegmentType.Empty:
                    default:
                        throw new Exception($"Unsupported segment type {pline.GetSegmentType(i)}!\n" +
                            $"Run \"CLEANPLINES\"");
                }
            }

            var geom = new NetTopologySuite.Geometries.LineString(
                points.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray());

            return new Feature(geom, props);
        }
    }
    internal static class BlockRefFjvToShapePointConverter
    {
        public static Feature Convert(Entity entity)
        {
            if (!(entity is BlockReference br))
                throw new ArgumentException($"Entity {entity.Handle} is not a block reference!");

            var dt = CsvData.FK;

            var props = new AttributesTable
            {
                { "BlockName", br.RealName() },
                { "BlockType", br.ReadDynamicCsvProperty(DynamicProperty.Type) },
                { "Rotation", ComponentSchedule.ReadBlockRotation(br, dt) },
                { "System", br.ReadDynamicCsvProperty(DynamicProperty.SysNavn) },
                { "Type", br.ReadDynamicCsvProperty(DynamicProperty.System) },
                { "DN1", br.ReadDynamicCsvProperty(DynamicProperty.DN1) },
                { "DN2", br.ReadDynamicCsvProperty(DynamicProperty.DN2) },
                { "Serie", br.ReadDynamicCsvProperty(DynamicProperty.Serie) },
                { "Angle", br.ReadDynamicCsvProperty(DynamicProperty.Vinkel) }
            };

            var geom = new NetTopologySuite.Geometries.Point(
                new NetTopologySuite.Geometries.Coordinate(br.Position.X, br.Position.Y));

            return new Feature(geom, props);
        }
    }    
}