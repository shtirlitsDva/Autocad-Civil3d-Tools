using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using static IntersectUtilities.UtilsCommon.Utils;

namespace PipeScheduleV2Tests
{
    public partial class PipeScheduleV2TestsClass
    {
        private const string ScheduleDirectory = @"X:\\AutoCAD DRI - 01 Civil 3D\\PipeSchedule\\Schedule\\";
        private const string RadiiDirectory = @"X:\\AutoCAD DRI - 01 Civil 3D\\PipeSchedule\\Radier\\";

        private record ScheduleRow(
            int Dn,
            PipeTypeEnum PipeType,
            PipeSeriesEnum PipeSeries,
            double PipeOuterDiameter,
            double PipeThickness,
            double Kod,
            double TrenchWidth,
            double MinElasticRadius,
            double VerticalFactor,
            short Color,
            double DefaultLength,
            double OffsetUnder7_5);

        private record RadiiRow(
            int Dn,
            PipeTypeEnum PipeType,
            int PipeLength,
            double Brpmin,
            double Erpmin);

        private static bool EnsureScheduleAvailable()
        {
            return Directory.Exists(ScheduleDirectory) && Directory.Exists(RadiiDirectory);
        }

        private static List<ScheduleRow> LoadScheduleRows(string key)
        {
            if (!EnsureScheduleAvailable())
                throw new Ps2SkipException("Pipe schedule CSV directories not available.");

            string path = Path.Combine(ScheduleDirectory, key + ".csv");
            if (!File.Exists(path)) throw new Ps2SkipException($"Schedule file missing: {path}");

            DataTable table = CsvReader.ReadCsvToDataTable(path, key);
            return table.Rows.Cast<DataRow>().Select(r => new ScheduleRow(
                int.Parse(Convert.ToString(r["DN"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                Enum.Parse<PipeTypeEnum>(Convert.ToString(r["PipeType"], CultureInfo.InvariantCulture)!, true),
                Enum.Parse<PipeSeriesEnum>(Convert.ToString(r["PipeSeries"], CultureInfo.InvariantCulture)!, true),
                double.Parse(Convert.ToString(r["pOd"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["pThk"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["kOd"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["tWdth"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["minElasticRadii"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["VertFactor"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                short.Parse(Convert.ToString(r["color"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["DefaultL"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["OffsetUnder7_5"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture)
            )).ToList();
        }

        private static List<RadiiRow> LoadRadiiRows(string company)
        {
            if (!EnsureScheduleAvailable())
                throw new Ps2SkipException("Pipe schedule CSV directories not available.");

            string path = Path.Combine(RadiiDirectory, company + ".csv");
            if (!File.Exists(path)) throw new Ps2SkipException($"Radii file missing: {path}");

            DataTable table = CsvReader.ReadCsvToDataTable(path, company);
            return table.Rows.Cast<DataRow>().Select(r => new RadiiRow(
                int.Parse(Convert.ToString(r["DN"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                Enum.Parse<PipeTypeEnum>(Convert.ToString(r["PipeType"], CultureInfo.InvariantCulture)!, true),
                int.Parse(Convert.ToString(r["PipeLength"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["BRpmin"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
                double.Parse(Convert.ToString(r["ERpmin"], CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture)
            )).ToList();
        }

        private static string BuildLayerName(PipeTypeEnum type, string systemKey, int dn)
        {
            return $"FJV-{type.ToString().ToUpperInvariant()}-{systemKey.ToUpperInvariant()}{dn}";
        }

        private static T WithPolyline<T>(string layer, double constantWidth, Func<Polyline, T> action)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new Ps2SkipException("No active document available for polyline creation.");
            var db = doc.Database;
            ObjectId createdId = ObjectId.Null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                PipeScheduleV2EntityRegistry.CreateLayerIfMissing(db, tr, layer);

                var pl = new Polyline();
                pl.SetDatabaseDefaults();
                pl.Layer = layer;
                pl.ConstantWidth = constantWidth;
                pl.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);

                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                createdId = pl.ObjectId;
                tr.Commit();
            }

            try
            {
                using var readTr = db.TransactionManager.StartOpenCloseTransaction();
                var entity = (Polyline)readTr.GetObject(createdId, OpenMode.ForRead);
                return action(entity);
            }
            finally
            {
                using var eraseTr = db.TransactionManager.StartTransaction();
                var ent = (Entity)eraseTr.GetObject(createdId, OpenMode.ForWrite);
                ent.Erase();
                eraseTr.Commit();
            }
        }

        private static ScheduleRow RequireScheduleRow(string key, PipeTypeEnum type, PipeSeriesEnum series)
        {
            var rows = LoadScheduleRows(key);
            var row = rows.FirstOrDefault(r => r.PipeType == type && r.PipeSeries == series);
            if (row == null)
                throw new Ps2SkipException($"Schedule row missing for {key} {type} {series}");
            return row;
        }

        private static RadiiRow RequireRadiiRow(string company, PipeTypeEnum type, int dn, int length)
        {
            var rows = LoadRadiiRows(company);
            var row = rows.FirstOrDefault(r => r.PipeType == type && r.Dn == dn && r.PipeLength == length);
            if (row == null)
                throw new Ps2SkipException($"Radii row missing for {company} {type} DN{dn} L{length}");
            return row;
        }

        private static class CoverageTracker
        {
            private static readonly HashSet<string> Covered = new HashSet<string>(StringComparer.Ordinal);

            public static void Register(MethodInfo method)
            {
                if (method == null) throw new ArgumentNullException(nameof(method));
                Covered.Add(Describe(method));
            }

            public static void Register(Type type, string methodName, params Type[] parameters)
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var method = type.GetMethod(methodName, flags, null, parameters ?? Type.EmptyTypes, null);
                if (method == null)
                    throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}({string.Join(", ", parameters.Select(p => p.Name))})");
                Register(method);
            }

            public static IReadOnlyCollection<string> Snapshot() => Covered;

            private static string Describe(MethodInfo method)
            {
                string paramList = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
                return $"{method.DeclaringType!.FullName}.{method.Name}({paramList})";
            }
        }

        private static string Describe(MethodInfo method)
        {
            string paramList = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{method.DeclaringType!.FullName}.{method.Name}({paramList})";
        }

        private static IEnumerable<MethodInfo> EnumerateTargetMethods()
        {
            var pipeScheduleMethods = typeof(PipeScheduleV2).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.DeclaringType == typeof(PipeScheduleV2));

            foreach (var m in pipeScheduleMethods)
                yield return m;

            var pipeTypeMethods = typeof(IPipeType).GetMethods();
            foreach (var m in pipeTypeMethods)
                yield return typeof(PipeTypeBase).GetMethod(m.Name, m.GetParameters().Select(p => p.ParameterType).ToArray()) ?? m;

            var pipeTypeRepoMethods = typeof(IPipeTypeRepository).GetMethods();
            foreach (var m in pipeTypeRepoMethods)
                yield return typeof(PipeTypeRepository).GetMethod(m.Name, m.GetParameters().Select(p => p.ParameterType).ToArray()) ?? m;

            var pipeTypeLoaderMethods = typeof(PipeTypeDataLoaderCSV).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(PipeTypeDataLoaderCSV));
            foreach (var m in pipeTypeLoaderMethods)
                yield return m;

            var pipeRadiusMethods = typeof(IPipeRadiusData).GetMethods();
            foreach (var m in pipeRadiusMethods)
                yield return typeof(PipeRadiusData).GetMethod(m.Name, m.GetParameters().Select(p => p.ParameterType).ToArray()) ?? m;

            var pipeRadiusRepoMethods = typeof(IPipeRadiusDataRepository).GetMethods();
            foreach (var m in pipeRadiusRepoMethods)
                yield return typeof(PipeRadiusDataRepository).GetMethod(m.Name, m.GetParameters().Select(p => p.ParameterType).ToArray()) ?? m;

            var pipeRadiusLoaderMethods = typeof(PipeRadiusDataLoaderCSV).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(PipeRadiusDataLoaderCSV));
            foreach (var m in pipeRadiusLoaderMethods)
                yield return m;
        }

        private static void RegisterCoverage(Type type, string name, params Type[] parameters)
        {
            CoverageTracker.Register(type, name, parameters);
        }

        private static void RegisterCoverage(MethodInfo method)
        {
            CoverageTracker.Register(method);
        }

        private static IReadOnlyCollection<string> CoveredMethods => CoverageTracker.Snapshot();
    }
}
