using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace PipeScheduleV2Tests
{
    public partial class PipeScheduleV2TestsClass
    {
        private static void SkipIfDataUnavailable()
        {
            if (!EnsureScheduleAvailable())
                throw new Ps2SkipException("Pipe schedule CSV directories not available.");
        }

        [Ps2Test]
        private static void PipeSchedule_StaticLookups()
        {
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetSystemString), typeof(PipeSystemEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetSystemString), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetSystemType), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetStdLengthsForSystem), typeof(PipeSystemEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetLineTypeLayerPrefix), typeof(PipeSystemEnum));

            var dnKey = GetSystemString(PipeSystemEnum.Stål);
            if (!string.Equals(dnKey, "DN", StringComparison.Ordinal))
                throw new Exception($"Expected Stål system string to be DN, got {dnKey}");

            var systemFromLayer = GetSystemString("FJV-FREM-DN200");
            if (!string.Equals(systemFromLayer, "DN", StringComparison.Ordinal))
                throw new Exception($"Expected system string from layer to be DN, got {systemFromLayer}");

            var system = GetSystemType("DN");
            if (system != PipeSystemEnum.Stål)
                throw new Exception($"Expected DN to map to Stål, got {system}");

            if (GetSystemType("UNKNOWN") != PipeSystemEnum.Ukendt)
                throw new Exception("Expected unknown system string to return Ukendt.");

            var lengths = GetStdLengthsForSystem(PipeSystemEnum.Stål);
            if (!lengths.Contains(12) || !lengths.Contains(16))
                throw new Exception("Missing expected standard lengths for Stål system.");

            var prefix = GetLineTypeLayerPrefix(PipeSystemEnum.Stål);
            if (!string.Equals(prefix, "ST", StringComparison.Ordinal))
                throw new Exception($"Expected prefix ST for Stål, got {prefix}");
        }

        [Ps2Test]
        private static void PipeSchedule_PipeTypeListings()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(ListAllPipeTypes));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeTypes));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(ListAllDnsForPipeSystemTypeSerie), typeof(PipeSystemEnum), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(ListAllDnsForPipeSystemType), typeof(PipeSystemEnum), typeof(PipeTypeEnum));

            var types = GetPipeTypes().ToList();
            if (types.Count == 0) throw new Exception("Pipe types not loaded.");

            var dnRows = LoadScheduleRows("DN").Where(r => r.PipeType == PipeTypeEnum.Enkelt && r.PipeSeries == PipeSeriesEnum.S1).Select(r => r.Dn).Distinct().OrderBy(x => x).ToList();
            var listedDns = ListAllDnsForPipeSystemTypeSerie(PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1)
                .OrderBy(x => x).ToList();
            if (!dnRows.SequenceEqual(listedDns))
                throw new Exception("ListAllDnsForPipeSystemTypeSerie mismatch with schedule data.");

            var dnAllRows = LoadScheduleRows("DN").Where(r => r.PipeType == PipeTypeEnum.Enkelt).Select(r => r.Dn).Distinct().OrderBy(x => x).ToList();
            var listedAll = ListAllDnsForPipeSystemType(PipeSystemEnum.Stål, PipeTypeEnum.Enkelt).OrderBy(x => x).ToList();
            if (!dnAllRows.SequenceEqual(listedAll))
                throw new Exception("ListAllDnsForPipeSystemType mismatch with schedule data.");

            ListAllPipeTypes(); // Should not throw
        }

        [Ps2Test]
        private static void PipeSchedule_EntityParsingAndDimensions()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeDN), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeDN), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeType), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeType), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeType), typeof(Entity), typeof(bool));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeSystem), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeSystem), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeOd), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeId), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeKOd), typeof(Entity), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeKOd), typeof(Entity), typeof(bool));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeKOd), typeof(PipeSystemEnum), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeSeriesV2), typeof(Entity), typeof(bool));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeStdLength), typeof(Entity));

            var row = RequireScheduleRow("DN", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            string layer = BuildLayerName(PipeTypeEnum.Frem, "DN", row.Dn);
            double constantWidth = row.Kod / 1000.0;

            WithPolyline(layer, constantWidth, pl =>
            {
                if (GetPipeDN(layer) != row.Dn) throw new Exception("GetPipeDN from string failed");
                if (GetPipeDN(pl) != row.Dn) throw new Exception("GetPipeDN from entity failed");

                if (GetPipeType(layer) != PipeTypeEnum.Frem) throw new Exception("GetPipeType from string failed");
                if (GetPipeType(pl) != PipeTypeEnum.Frem) throw new Exception("GetPipeType from entity failed");
                if (GetPipeType(pl, true) != PipeTypeEnum.Enkelt) throw new Exception("FRtoEnkelt conversion failed");

                if (GetPipeSystem(layer) != PipeSystemEnum.Stål) throw new Exception("GetPipeSystem from string failed");
                if (GetPipeSystem(pl) != PipeSystemEnum.Stål) throw new Exception("GetPipeSystem from entity failed");

                double od = GetPipeOd(pl);
                if (Math.Abs(od - row.PipeOuterDiameter) > 1e-6) throw new Exception("GetPipeOd mismatch");

                double id = GetPipeId(pl);
                double expectedId = row.PipeOuterDiameter - 2 * row.PipeThickness;
                if (Math.Abs(id - expectedId) > 1e-6) throw new Exception("GetPipeId mismatch");

                double kodDirect = GetPipeKOd(PipeSystemEnum.Stål, row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
                if (Math.Abs(kodDirect - row.Kod) > 1e-6)
                    throw new Exception("GetPipeKOd direct mismatch");

                double kodEntitySeries = GetPipeKOd(pl, PipeSeriesEnum.S1);
                if (Math.Abs(kodEntitySeries - row.Kod) > 1e-6)
                    throw new Exception("GetPipeKOd entity mismatch");

                var series = GetPipeSeriesV2(pl, true);
                if (series != PipeSeriesEnum.S1)
                    throw new Exception("GetPipeSeriesV2 failed");

                double kodEntityAuto = GetPipeKOd(pl, false);
                if (Math.Abs(kodEntityAuto - row.Kod) > 1e-6)
                    throw new Exception("GetPipeKOd entity auto mismatch");

                double stdLength = GetPipeStdLength(pl);
                if (Math.Abs(stdLength - row.DefaultLength) > 1e-6)
                    throw new Exception("GetPipeStdLength mismatch");

                return 0;
            });
        }

        [Ps2Test]
        private static void PipeSchedule_MinElasticRadii()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeMinElasticRadiusHorizontalCharacteristic), typeof(Entity), typeof(bool));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeMinElasticRadiusHorizontalCharacteristic), typeof(PipeSystemEnum), typeof(int), typeof(PipeTypeEnum), typeof(bool));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeMinElasticRadiusVerticalCharacteristic), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeMinElasticRadiusVerticalCharacteristic), typeof(PipeSystemEnum), typeof(int), typeof(PipeTypeEnum));

            var row = RequireScheduleRow("DN", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            string layer = BuildLayerName(PipeTypeEnum.Frem, "DN", row.Dn);
            double constantWidth = row.Kod / 1000.0;

            WithPolyline(layer, constantWidth, pl =>
            {
                double horizontal = GetPipeMinElasticRadiusHorizontalCharacteristic(pl);
                if (Math.Abs(horizontal - row.MinElasticRadius) > 1e-6)
                    throw new Exception("Horizontal elastic radius mismatch");

                double horizontalNoInSitu = GetPipeMinElasticRadiusHorizontalCharacteristic(PipeSystemEnum.Stål, row.Dn, PipeTypeEnum.Enkelt, false);
                if (Math.Abs(horizontalNoInSitu - row.MinElasticRadius) > 1e-6)
                    throw new Exception("Horizontal elastic radius (system overload) mismatch");

                double vertical = GetPipeMinElasticRadiusVerticalCharacteristic(pl);
                if (Math.Abs(vertical - row.MinElasticRadius * row.VerticalFactor) > 1e-6)
                    throw new Exception("Vertical elastic radius mismatch");

                double verticalDirect = GetPipeMinElasticRadiusVerticalCharacteristic(PipeSystemEnum.Stål, row.Dn, PipeTypeEnum.Enkelt);
                if (Math.Abs(verticalDirect - row.MinElasticRadius * row.VerticalFactor) > 1e-6)
                    throw new Exception("Vertical elastic radius (system overload) mismatch");

                return 0;
            });
        }

        [Ps2Test]
        private static void PipeSchedule_InSituBendingRules()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(IsInSituBent), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(IsInSituBent), typeof(PipeSystemEnum), typeof(int), typeof(PipeTypeEnum));

            var aluRow = RequireScheduleRow("ALUPEX", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            string aluLayer = BuildLayerName(PipeTypeEnum.Frem, "ALUPEX", aluRow.Dn);
            WithPolyline(aluLayer, aluRow.Kod / 1000.0, pl =>
            {
                if (!IsInSituBent(pl)) throw new Exception("AluPex entity should be InSitu.");
                return 0;
            });

            if (!IsInSituBent(PipeSystemEnum.AluPex, aluRow.Dn, PipeTypeEnum.Enkelt))
                throw new Exception("AluPex system should be InSitu via overload.");

            var dnRows = LoadScheduleRows("DN").Where(r => r.PipeType == PipeTypeEnum.Frem).OrderBy(r => r.Dn).ToList();
            var smallFrem = dnRows.FirstOrDefault(r => r.Dn < 100);
            if (smallFrem == null)
                throw new Ps2SkipException("No Frem row below DN100 found in schedule.");
            string stLayer = BuildLayerName(PipeTypeEnum.Frem, "DN", smallFrem.Dn);
            WithPolyline(stLayer, smallFrem.Kod / 1000.0, pl =>
            {
                if (!IsInSituBent(pl)) throw new Exception("Frem DN<100 should be InSitu.");
                return 0;
            });

            if (!IsInSituBent(PipeSystemEnum.Stål, smallFrem.Dn, PipeTypeEnum.Frem))
                throw new Exception("Frem DN<100 should be InSitu via overload.");
        }

        [Ps2Test]
        private static void PipeSchedule_TrenchWidthAndOffsets()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetTrenchWidth), typeof(int), typeof(PipeSystemEnum), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetOffset), typeof(int), typeof(PipeSystemEnum), typeof(PipeTypeEnum), typeof(PipeSeriesEnum), typeof(double), typeof(double));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetOffset), typeof(Entity), typeof(double), typeof(double));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetOffset), typeof(Entity), typeof(PipeSeriesEnum), typeof(double), typeof(double));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetColorForDim), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetLayerColor), typeof(PipeSystemEnum), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetLayerColor), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetPipeTypeByAvailability), typeof(PipeSystemEnum), typeof(int));

            var row = RequireScheduleRow("DN", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            string layer = BuildLayerName(PipeTypeEnum.Frem, "DN", row.Dn);
            double constantWidth = row.Kod / 1000.0;

            double trench = GetTrenchWidth(row.Dn, PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            if (Math.Abs(trench - row.TrenchWidth) > 1e-6)
                throw new Exception("GetTrenchWidth mismatch");

            double offsetBase = GetOffset(row.Dn, PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1, 7.5, 0);
            if (Math.Abs(offsetBase - row.OffsetUnder7_5) > 1e-6)
                throw new Exception("GetOffset base mismatch");

            double offsetWide = GetOffset(row.Dn, PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1, 8.0, 0);
            if (Math.Abs(offsetWide - (row.OffsetUnder7_5 + 0.15)) > 1e-6)
                throw new Exception("GetOffset width adjustment mismatch");

            double offsetSupplement = GetOffset(row.Dn, PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1, 8.0, 0.2);
            if (Math.Abs(offsetSupplement - (row.OffsetUnder7_5 + 0.35)) > 1e-6)
                throw new Exception("GetOffset supplement mismatch");

            WithPolyline(layer, constantWidth, pl =>
            {
                double offsetEntity = GetOffset(pl, PipeSeriesEnum.S1, 7.5, 0);
                if (Math.Abs(offsetEntity - row.OffsetUnder7_5) > 1e-6)
                    throw new Exception("Entity offset mismatch");

                double offsetEntityAuto = GetOffset(pl, 8.0, 0.3);
                if (Math.Abs(offsetEntityAuto - (row.OffsetUnder7_5 + 0.45)) > 1e-6)
                    throw new Exception("Entity offset auto mismatch");

                short dimColor = GetColorForDim(layer);
                if (dimColor != row.Color)
                    throw new Exception("GetColorForDim mismatch");

                short layerColor = GetLayerColor(PipeSystemEnum.Stål, PipeTypeEnum.Frem);
                if (layerColor != 1)
                    throw new Exception("GetLayerColor(system,type) mismatch");

                short layerColorEntity = GetLayerColor(pl);
                if (layerColorEntity != 1)
                    throw new Exception("GetLayerColor(entity) mismatch");

                return 0;
            });

            var dnRows = LoadScheduleRows("DN").Where(r => r.Dn == row.Dn).Select(r => r.PipeType).Distinct().ToList();
            var availability = GetPipeTypeByAvailability(PipeSystemEnum.Stål, row.Dn);
            var expected = dnRows.Contains(PipeTypeEnum.Twin) ? PipeTypeEnum.Twin : PipeTypeEnum.Frem;
            if (availability != expected)
                throw new Exception("GetPipeTypeByAvailability mismatch");
        }

        [Ps2Test]
        private static void PipeSchedule_BuerorRadii()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetBuerorMinRadius), typeof(Entity));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetBuerorMinRadius), typeof(Entity), typeof(string), typeof(int));

            var row = RequireScheduleRow("DN", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            string layer = BuildLayerName(PipeTypeEnum.Frem, "DN", row.Dn);
            double constantWidth = row.Kod / 1000.0;

            var isoplus = RequireRadiiRow("Isoplus", PipeTypeEnum.Enkelt, row.Dn, 12);
            var logstor = RequireRadiiRow("Logstor", PipeTypeEnum.Enkelt, row.Dn, 12);

            WithPolyline(layer, constantWidth, pl =>
            {
                double defaultRadius = GetBuerorMinRadius(pl);
                if (Math.Abs(defaultRadius - isoplus.Brpmin) > 1e-6)
                    throw new Exception("Default buerør radius mismatch");

                double logstorRadius = GetBuerorMinRadius(pl, "Logstor", 12);
                if (Math.Abs(logstorRadius - logstor.Brpmin) > 1e-6)
                    throw new Exception("Logstor buerør radius mismatch");

                return 0;
            });
        }

        [Ps2Test]
        private static void PipeSchedule_LabelFormatting()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(GetLabel), typeof(Entity));

            var testCases = new List<(string Key, PipeTypeEnum SeriesType)>
            {
                ("DN", PipeTypeEnum.Frem),
                ("ALUPEX", PipeTypeEnum.Frem),
                ("CU", PipeTypeEnum.Frem),
                ("PRTFLEXL", PipeTypeEnum.Frem),
                ("AQTHRM11", PipeTypeEnum.Frem),
                ("PE", PipeTypeEnum.Frem)
            };

            foreach (var tc in testCases)
            {
                var rows = LoadScheduleRows(tc.Key).Where(r => r.PipeType == PipeTypeEnum.Enkelt).ToList();
                if (rows.Count == 0)
                    throw new Ps2SkipException($"No Enkelt rows found in schedule {tc.Key}");
                var row = rows.First();
                string layer = BuildLayerName(tc.SeriesType, tc.Key, row.Dn);
                double constantWidth = row.Kod / 1000.0;

                WithPolyline(layer, constantWidth, pl =>
                {
                    string label = GetLabel(pl);
                    if (string.IsNullOrWhiteSpace(label))
                        throw new Exception($"Empty label for {tc.Key}");
                    if (!label.Contains(row.Dn.ToString(CultureInfo.InvariantCulture)))
                        throw new Exception($"Label does not include DN for {tc.Key}: {label}");
                    return 0;
                });
            }
        }

        [Ps2Test]
        private static void PipeSchedule_Loaders_Reload()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeScheduleV2), nameof(LoadPipeTypeData), typeof(string));
            RegisterCoverage(typeof(PipeScheduleV2), nameof(LoadRadiiData), typeof(string));

            LoadPipeTypeData(ScheduleDirectory);
            LoadRadiiData(RadiiDirectory);

            if (!GetPipeTypes().Any())
                throw new Exception("Pipe types not loaded after reload.");
        }

        [Ps2Test]
        private static void PipeSchedule_GetCoverDepthConstant()
        {
            RegisterCoverage(typeof(PipeScheduleV2), "GetCoverDepth", typeof(int), typeof(PipeSystemEnum), typeof(PipeTypeEnum));
            double cover = PipeScheduleV2.GetCoverDepth(20, PipeSystemEnum.Stål, PipeTypeEnum.Frem);
            if (Math.Abs(cover - 0.6) > 1e-9)
                throw new Exception("GetCoverDepth expected 0.6m");
        }

        [Ps2Test]
        private static void PipeType_DN_Api_MatchesCsv()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeOd), typeof(int));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeId), typeof(int));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeSeries), typeof(int), typeof(PipeTypeEnum), typeof(double));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeKOd), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetMinElasticRadius), typeof(int), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetFactorForVerticalElasticBending), typeof(int), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeStdLength), typeof(int), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.ListAllDnsForPipeTypeSerie), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetLabel), typeof(int), typeof(PipeTypeEnum), typeof(double), typeof(double));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetLayerColor), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetTrenchWidth), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetSizeColor), typeof(int), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.ListAllDnsForPipeType), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetAvailableTypes));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetAvailableSeriesForType), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetDefaultLengthForDnAndType), typeof(int), typeof(PipeTypeEnum));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeTypeByAvailability), typeof(int));
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetOffset), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));

            var pipeType = PipeScheduleV2.GetPipeTypes().First(pt => pt.System == PipeSystemEnum.Stål);
            var row = RequireScheduleRow("DN", PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);

            if (Math.Abs(pipeType.GetPipeOd(row.Dn) - row.PipeOuterDiameter) > 1e-6)
                throw new Exception("IPipeType.GetPipeOd mismatch");

            if (Math.Abs(pipeType.GetPipeId(row.Dn) - (row.PipeOuterDiameter - 2 * row.PipeThickness)) > 1e-6)
                throw new Exception("IPipeType.GetPipeId mismatch");

            var series = pipeType.GetPipeSeries(row.Dn, PipeTypeEnum.Enkelt, row.Kod);
            if (series != PipeSeriesEnum.S1) throw new Exception("IPipeType.GetPipeSeries mismatch");

            if (Math.Abs(pipeType.GetPipeKOd(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1) - row.Kod) > 1e-6)
                throw new Exception("IPipeType.GetPipeKOd mismatch");

            if (Math.Abs(pipeType.GetMinElasticRadius(row.Dn, PipeTypeEnum.Enkelt) - row.MinElasticRadius) > 1e-6)
                throw new Exception("IPipeType.GetMinElasticRadius mismatch");

            if (Math.Abs(pipeType.GetFactorForVerticalElasticBending(row.Dn, PipeTypeEnum.Enkelt) - row.VerticalFactor) > 1e-6)
                throw new Exception("IPipeType.GetFactorForVerticalElasticBending mismatch");

            if (Math.Abs(pipeType.GetPipeStdLength(row.Dn, PipeTypeEnum.Enkelt) - row.DefaultLength) > 1e-6)
                throw new Exception("IPipeType.GetPipeStdLength mismatch");

            var dnsForSerie = pipeType.ListAllDnsForPipeTypeSerie(PipeTypeEnum.Enkelt, PipeSeriesEnum.S1)?.OrderBy(x => x).ToList();
            var expectedDns = LoadScheduleRows("DN").Where(r => r.PipeType == PipeTypeEnum.Enkelt && r.PipeSeries == PipeSeriesEnum.S1).Select(r => r.Dn).Distinct().OrderBy(x => x).ToList();
            if (dnsForSerie == null || !dnsForSerie.SequenceEqual(expectedDns))
                throw new Exception("IPipeType.ListAllDnsForPipeTypeSerie mismatch");

            string label = pipeType.GetLabel(row.Dn, PipeTypeEnum.Enkelt, row.PipeOuterDiameter, row.Kod);
            if (string.IsNullOrWhiteSpace(label) || !label.Contains("DN" + row.Dn))
                throw new Exception("IPipeType.GetLabel mismatch");

            if (pipeType.GetLayerColor(PipeTypeEnum.Frem) != 1)
                throw new Exception("IPipeType.GetLayerColor mismatch");

            if (Math.Abs(pipeType.GetTrenchWidth(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1) - row.TrenchWidth) > 1e-6)
                throw new Exception("IPipeType.GetTrenchWidth mismatch");

            if (pipeType.GetSizeColor(row.Dn, PipeTypeEnum.Enkelt) != row.Color)
                throw new Exception("IPipeType.GetSizeColor mismatch");

            var dnsForType = pipeType.ListAllDnsForPipeType(PipeTypeEnum.Enkelt)?.OrderBy(x => x).ToList();
            var expectedDnsForType = LoadScheduleRows("DN").Where(r => r.PipeType == PipeTypeEnum.Enkelt).Select(r => r.Dn).Distinct().OrderBy(x => x).ToList();
            if (dnsForType == null || !dnsForType.SequenceEqual(expectedDnsForType))
                throw new Exception("IPipeType.ListAllDnsForPipeType mismatch");

            var availableTypes = pipeType.GetAvailableTypes().OrderBy(x => x).ToList();
            if (!availableTypes.Contains(PipeTypeEnum.Enkelt))
                throw new Exception("IPipeType.GetAvailableTypes missing Enkelt");

            var availableSeries = pipeType.GetAvailableSeriesForType(PipeTypeEnum.Enkelt)?.OrderBy(x => x).ToList();
            if (availableSeries == null || !availableSeries.Contains(PipeSeriesEnum.S1))
                throw new Exception("IPipeType.GetAvailableSeriesForType missing S1");

            if (Math.Abs(pipeType.GetDefaultLengthForDnAndType(row.Dn, PipeTypeEnum.Enkelt) - row.DefaultLength) > 1e-6)
                throw new Exception("IPipeType.GetDefaultLengthForDnAndType mismatch");

            var availability = pipeType.GetPipeTypeByAvailability(row.Dn);
            var dnTypes = LoadScheduleRows("DN").Where(r => r.Dn == row.Dn).Select(r => r.PipeType).Distinct().ToList();
            var expectedAvailability = dnTypes.Contains(PipeTypeEnum.Twin) ? PipeTypeEnum.Twin : PipeTypeEnum.Frem;
            if (availability != expectedAvailability)
                throw new Exception("IPipeType.GetPipeTypeByAvailability mismatch");

            if (Math.Abs(pipeType.GetOffset(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1) - row.OffsetUnder7_5) > 1e-6)
                throw new Exception("IPipeType.GetOffset mismatch");
        }

        [Ps2Test]
        private static void PipeType_CU_SeriesOverride()
        {
            SkipIfDataUnavailable();
            var pipeType = PipeScheduleV2.GetPipeTypes().First(pt => pt.System == PipeSystemEnum.Kobberflex);
            var rows = LoadScheduleRows("CU").Where(r => r.PipeType == PipeTypeEnum.Enkelt).ToList();
            if (rows.Count == 0)
                throw new Ps2SkipException("No CU rows available");
            var reference = rows.First(r => r.PipeSeries == PipeSeriesEnum.S2);
            double kodS2 = pipeType.GetPipeKOd(reference.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S2);
            double kodS3 = pipeType.GetPipeKOd(reference.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S3);
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeKOd), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            if (Math.Abs(kodS2 - kodS3) > 1e-6)
                throw new Exception("CU GetPipeKOd should treat S3 as S2");
        }

        [Ps2Test]
        private static void PipeType_PRTFlex_Label()
        {
            SkipIfDataUnavailable();
            var pipeType = PipeScheduleV2.GetPipeTypes().First(pt => pt.System == PipeSystemEnum.PertFlextra);
            var row = LoadScheduleRows("PRTFLEXL").First(r => r.PipeType == PipeTypeEnum.Enkelt);
            string label = pipeType.GetLabel(row.Dn, PipeTypeEnum.Enkelt, row.PipeOuterDiameter, row.Kod);
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetLabel), typeof(int), typeof(PipeTypeEnum), typeof(double), typeof(double));
            if (!label.StartsWith("PE-RT", StringComparison.OrdinalIgnoreCase))
                throw new Exception("PertFlextra label format unexpected");
        }

        [Ps2Test]
        private static void PipeType_AQTHRM11_SingleSeries()
        {
            SkipIfDataUnavailable();
            var pipeType = PipeScheduleV2.GetPipeTypes().First(pt => pt.System == PipeSystemEnum.AquaTherm11);
            var row = LoadScheduleRows("AQTHRM11").First(r => r.PipeType == PipeTypeEnum.Enkelt);
            double kodS1 = pipeType.GetPipeKOd(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            double kodS3 = pipeType.GetPipeKOd(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S3);
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeKOd), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            if (Math.Abs(kodS1 - kodS3) > 1e-6)
                throw new Exception("AquaTherm11 GetPipeKOd should ignore series");
        }

        [Ps2Test]
        private static void PipeType_PE_SingleSeries()
        {
            SkipIfDataUnavailable();
            var pipeType = PipeScheduleV2.GetPipeTypes().First(pt => pt.System == PipeSystemEnum.PE);
            var row = LoadScheduleRows("PE").First(r => r.PipeType == PipeTypeEnum.Enkelt);
            double kod = pipeType.GetPipeKOd(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S1);
            double kodOther = pipeType.GetPipeKOd(row.Dn, PipeTypeEnum.Enkelt, PipeSeriesEnum.S3);
            RegisterCoverage(typeof(PipeTypeBase), nameof(IPipeType.GetPipeKOd), typeof(int), typeof(PipeTypeEnum), typeof(PipeSeriesEnum));
            if (Math.Abs(kod - kodOther) > 1e-6)
                throw new Exception("PE GetPipeKOd should ignore series");
        }

        [Ps2Test]
        private static void PipeTypeRepository_Basics()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeTypeRepository), nameof(IPipeTypeRepository.GetPipeType), typeof(string));
            RegisterCoverage(typeof(PipeTypeRepository), nameof(IPipeTypeRepository.Initialize), typeof(Dictionary<string, IPipeType>));
            RegisterCoverage(typeof(PipeTypeRepository), nameof(IPipeTypeRepository.ListAllPipeTypes));
            RegisterCoverage(typeof(PipeTypeRepository), nameof(IPipeTypeRepository.GetPipeTypes));

            var loader = new PipeTypeDataLoaderCSV();
            var data = loader.Load(System.IO.Directory.EnumerateFiles(ScheduleDirectory, "DN.csv"));
            RegisterCoverage(typeof(PipeTypeDataLoaderCSV), nameof(PipeTypeDataLoaderCSV.Load), typeof(IEnumerable<string>));
            if (!data.ContainsKey("DN")) throw new Exception("Loader failed to load DN");

            var repo = new PipeTypeRepository();
            repo.Initialize(data);
            if (!repo.ListAllPipeTypes().Any()) throw new Exception("Repository list empty");
            var dn = repo.GetPipeType("DN");
            if (dn == null) throw new Exception("Failed to retrieve DN pipe type");
        }

        [Ps2Test]
        private static void PipeRadiusData_Api()
        {
            SkipIfDataUnavailable();
            RegisterCoverage(typeof(PipeRadiusData), nameof(IPipeRadiusData.GetBuerorMinRadius), typeof(int), typeof(int));
            RegisterCoverage(typeof(PipeRadiusData), nameof(IPipeRadiusData.Initialize), typeof(DataTable), typeof(CompanyEnum));
            RegisterCoverage(typeof(PipeRadiusDataLoaderCSV), nameof(PipeRadiusDataLoaderCSV.Load), typeof(IEnumerable<string>));

            var loader = new PipeRadiusDataLoaderCSV();
            var data = loader.Load(System.IO.Directory.EnumerateFiles(RadiiDirectory, "Logstor.csv"));
            if (!data.ContainsKey("Logstor")) throw new Exception("Failed to load Logstor radii");

            var repo = new PipeRadiusDataRepository();
            RegisterCoverage(typeof(PipeRadiusDataRepository), nameof(IPipeRadiusDataRepository.Initialize), typeof(Dictionary<string, IPipeRadiusData>));
            RegisterCoverage(typeof(PipeRadiusDataRepository), nameof(IPipeRadiusDataRepository.GetPipeRadiusData), typeof(string));
            RegisterCoverage(typeof(PipeRadiusDataRepository), nameof(IPipeRadiusDataRepository.ListAllPipeRadiusData));
            RegisterCoverage(typeof(PipeRadiusDataRepository), nameof(IPipeRadiusDataRepository.GetPipeRadiusData));
            repo.Initialize(data);
            if (!repo.ListAllPipeRadiusData().Any()) throw new Exception("Pipe radius repo listing empty");
            var logstor = repo.GetPipeRadiusData("Logstor");
            var allData = repo.GetPipeRadiusData().ToList();
            if (allData.Count == 0) throw new Exception("Pipe radius repo GetPipeRadiusData() returned empty");

            var sampleRow = LoadRadiiRows("Logstor").FirstOrDefault();
            if (sampleRow == null)
                throw new Ps2SkipException("Logstor radii data empty");
            double radius = logstor.GetBuerorMinRadius(sampleRow.Dn, sampleRow.PipeLength);
            if (Math.Abs(radius - sampleRow.Brpmin) > 1e-6)
                throw new Exception("PipeRadiusData GetBuerorMinRadius mismatch");
        }

        [Ps2Test]
        private static void Coverage_All_Methods_Accounted_For()
        {
            SkipIfDataUnavailable();
            var covered = new HashSet<string>(CoveredMethods);
            var allowedMissing = new HashSet<string>(StringComparer.Ordinal)
            {
                "IntersectUtilities.PipeScheduleV2.PipeScheduleV2.AskForBuerorMinRadius(Entity,Int32)",
            };

            var targets = EnumerateTargetMethods()
                .Select(Describe)
                .Where(name => !allowedMissing.Contains(name))
                .Distinct()
                .ToList();

            var missing = targets.Where(name => !covered.Contains(name)).ToList();
            if (missing.Count > 0)
            {
                throw new Exception("Uncovered methods: " + string.Join(", ", missing));
            }
        }
    }
}
