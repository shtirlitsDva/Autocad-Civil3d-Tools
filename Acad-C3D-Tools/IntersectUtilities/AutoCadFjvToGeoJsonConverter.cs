using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;
using FolderSelect;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities.DynamicBlocks;

using Autodesk.Aec.DatabaseServices;

namespace IntersectUtilities
{
    public interface IAutoCadFjvToGeoJsonConverter
    {
        GeoJsonFeature Convert(Entity entity);
    }

    public class PolylineFjvToGeoJsonPolygonConverter : IAutoCadFjvToGeoJsonConverter
    {
        public GeoJsonFeature Convert(Entity entity)
        {
            if (!(entity is Polyline pl))
                throw new ArgumentException($"Entity {entity.Handle} is not a polyline!");

            var feature = new GeoJsonFeature
            {
                Properties = new Dictionary<string, object>
                {
                    { "DN", GetPipeDN(pl) },
                    { "System", GetPipeType(pl) },
                    { "Serie", GetPipeSeriesV2(pl) },
                    { "Type", GetPipeSystem(pl) },
                    { "KOd", GetPipeKOd(pl) },
                },

                Geometry = new GeoJsonPolygon() { },
            };

            switch (GetPipeType(pl))
            {
                case PipeTypeEnum.Twin:
                    feature.Properties.Add("Color", "#FF00FF");
                    break;
                case PipeTypeEnum.Frem:
                    feature.Properties.Add("Color", "#FF0000");
                    break;
                case PipeTypeEnum.Retur:
                    feature.Properties.Add("Color", "#0000FF");
                    break;
                case PipeTypeEnum.Enkelt:
                default:
                    throw new System.Exception(
                        $"{GetPipeType(pl)} of {pl.Handle} is not supported.");
            }

            if (pl.Closed) throw new System.NotSupportedException(
                $"Polyline {pl.Handle} is closed! Closed polylines are not supported yet!");
            if (pl.Length < 0.1) throw new System.NotSupportedException(
                $"Polyline {pl.Handle} is too short! Polylines shorter than 0.1m are not allowed!");

            var samplePoints = pl.GetSamplePoints();

            List<Point2d> fsPoints = new List<Point2d>();
            List<Point2d> ssPoints = new List<Point2d>();

            double halfKOd = GetPipeKOd(pl, true) / 1000.0 / 2;

            for (int i = 0; i < samplePoints.Count; i++)
            {
                Point3d samplePoint = samplePoints[i].To3D();
                var v = pl.GetFirstDerivative(samplePoint);

                var v1 = v.GetPerpendicularVector().GetNormal();
                var v2 = v1 * -1;

                fsPoints.Add((samplePoint + v1 * halfKOd).To2D());
                fsPoints.Add((samplePoint + v2 * halfKOd).To2D());
            }

            List<Point2d> points = new List<Point2d>();
            points.AddRange(fsPoints);
            ssPoints.Reverse();
            points.AddRange(ssPoints);
            points.Add(fsPoints[0]);
            points = points.SortAndEnsureCounterclockwiseOrder();

            List<double[][]> coordinatesGatherer = new List<double[][]>();
            double[][] coordinates = new double[points.Count][];
            for (int i = 0; i < points.Count; i++)
                coordinates[i] = new double[] { points[i].X, points[i].Y };
            coordinatesGatherer.Add(coordinates);
            (feature.Geometry as GeoJsonPolygon).Coordinates = coordinatesGatherer.ToArray();

            return feature;
        }
    }

    public static class FjvToGeoJsonConverterFactory
    {
        public static IAutoCadFjvToGeoJsonConverter CreateConverter(Entity entity)
        {
            switch (entity)
            {
                //case Hatch _:
                //    return new HatchToGeoJsonPolygonConverter();
                case Polyline _:
                    return new PolylineFjvToGeoJsonPolygonConverter();
                default:
                    return null;
                    //throw new NotSupportedException("Unsupported AutoCAD entity type.");
            }
        }
    }
}
