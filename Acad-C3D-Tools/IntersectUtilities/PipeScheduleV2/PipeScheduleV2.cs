using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace IntersectUtilities.PipeScheduleV2
{
    public static class PipeScheduleV2
    {
        private static IPipeTypeRepository _repository;
        private static IPipeRadiusDataRepository _radiiRepo;

        static PipeScheduleV2()
        {
            LoadPipeTypeData(@"X:\AutoCAD DRI - 01 Civil 3D\PipeSchedule\Schedule\");
            LoadRadiiData(@"X:\AutoCAD DRI - 01 Civil 3D\PipeSchedule\Radier\");
        }

        #region Utility methods
        public static IEnumerable<int> ListAllDnsForPipeSystemTypeSerie(
            PipeSystemEnum system, PipeTypeEnum type, PipeSeriesEnum serie)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"Undefined PipeType system {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.ListAllDnsForPipeTypeSerie(type, serie);
        }
        public static IEnumerable<int> ListAllDnsForPipeSystemType(
            PipeSystemEnum system, PipeTypeEnum type)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"Undefined PipeType system {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.ListAllDnsForPipeType(type);
        }
        internal static IEnumerable<PipeTypeEnum> GetPipeSystemAvailableTypes(PipeSystemEnum system)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"Undefined PipeType system {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetAvailableTypes();
        }
        internal static IEnumerable<PipeSeriesEnum> ListAllSeriesForPipeSystemType(PipeSystemEnum system, PipeTypeEnum type)
        {
            //Get pipe type
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"Undefined PipeType system {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetAvailableSeriesForType(type);
        }
        public static string GetSystemString(PipeSystemEnum system)
        {
            if (!systemDictReversed.ContainsKey(system)) return "Ukendt";
            return systemDictReversed[system];
        }
        public static string GetSystemString(string layer)
        {
            layer = ExtractLayerName(layer);
            if (layerNameDataParser.IsMatch(layer))
                return layerNameDataParser.Match(layer).Groups["DATATYPE"].Value;
            return "Ukendt";
        }
        public static PipeSystemEnum GetSystemType(string s)
        {
            if (systemDict.ContainsKey(s)) return systemDict[s];
            return PipeSystemEnum.Ukendt;
        }
        #endregion

        #region Variables and dicts
        #region Radii
        public static Dictionary<string, Type> companyDict = new Dictionary<string, Type>()
        {
            { "Logstor", typeof(Logstor) },
            { "Isoplus", typeof(Isoplus) },
        };
        public static Dictionary<string, CompanyEnum> companyEnumDict = new Dictionary<string, CompanyEnum>()
        {
            {"Logstor", CompanyEnum.Logstor },
            {"Isoplus", CompanyEnum.Isoplus },
        };
        public static void LoadRadiiData(string pathToPipeTypesStore)
        {
            var csvs = System.IO.Directory.EnumerateFiles(
                pathToPipeTypesStore, "*.csv", System.IO.SearchOption.TopDirectoryOnly);

            _radiiRepo = new PipeRadiusDataRepository();
            _radiiRepo.Initialize(new PipeRadiusDataLoaderCSV().Load(csvs));
        }
        #endregion
        #region PipeTypes
        public static Dictionary<string, Type> typeDict = new Dictionary<string, Type>()
        {
            { "DN", typeof(PipeTypeDN) },
            { "ALUPEX", typeof(PipeTypeALUPEX) },
            { "CU", typeof(PipeTypeCU) },
            //{ "PEXU", typeof(PipeTypePEXU) },
            { "PRTFLEXL", typeof(PipeTypePRTFLEXL) },
            { "AQTHRM11", typeof(PipeTypeAQTHRM11) },
            { "PE", typeof(PipeTypePE) },
        };
        public static Dictionary<string, PipeSystemEnum> systemDict = new Dictionary<string, PipeSystemEnum>()
        {
            { "DN", PipeSystemEnum.Stål },
            { "ALUPEX", PipeSystemEnum.AluPex },
            { "CU", PipeSystemEnum.Kobberflex },
            //{"PEXU", PipeSystemEnum.PexU },
            { "PRTFLEXL", PipeSystemEnum.PertFlextra },
            { "AQTHRM11", PipeSystemEnum.AquaTherm11 },
            { "PE", PipeSystemEnum.PE },
        };
        private static Dictionary<PipeSystemEnum, string> systemDictReversed =
            new Dictionary<PipeSystemEnum, string>()
            {
                {PipeSystemEnum.Stål, "DN" },
                {PipeSystemEnum.AluPex , "ALUPEX" },
                {PipeSystemEnum.Kobberflex , "CU" },
                //{PipeSystemEnum.PexU , "PEXU" },
                {PipeSystemEnum.PertFlextra , "PRTFLEXL" },
                {PipeSystemEnum.AquaTherm11 , "AQTHRM11" },
                {PipeSystemEnum.PE, "PE" } ,
            };
        private static Dictionary<PipeSystemEnum, string> lineTypePrefixDict =
            new Dictionary<PipeSystemEnum, string>()
            {
                {PipeSystemEnum.Stål, "ST" },
                {PipeSystemEnum.AluPex , "AP" },
                {PipeSystemEnum.Kobberflex , "CU" },
                //{PipeSystemEnum.PexU , "PEXU" },
                {PipeSystemEnum.PertFlextra , "PRT" },
                {PipeSystemEnum.AquaTherm11 , "AT" },
                {PipeSystemEnum.PE, "PE" },
            };
        private static Dictionary<PipeSystemEnum, double[]> availableStdLengths = new()
        {
            {PipeSystemEnum.Stål, new double[] {12, 16}},
            {PipeSystemEnum.AluPex, new double[] {100}},
            {PipeSystemEnum.Kobberflex, new double[] { 100 }},
            //{PipeSystemEnum.PexU, new[] {100}},
            {PipeSystemEnum.PertFlextra, new double[] {12, 16, 100}},
            {PipeSystemEnum.AquaTherm11, new double[] {11.6}},
            {PipeSystemEnum.PE, new double[] {12, 50}}
        };
        public static double[] GetStdLengthsForSystem(PipeSystemEnum pipeSystem) => availableStdLengths[pipeSystem];
        private static string pipeTypes = string.Join(
            "|", Enum.GetNames(typeof(PipeTypeEnum)).Select(x => x.ToUpper()));
        private static string pipeDataTypes = string.Join(
            "|", typeDict.Keys);
        private static Regex layerNameDataParser =
            new Regex($@"FJV-(?<TYPE>{pipeTypes})-(?<DATATYPE>{pipeDataTypes})(?<DN>\d+)");
        public static void LoadPipeTypeData(string pathToPipeTypesStore)
        {
            var csvs = System.IO.Directory.EnumerateFiles(
                pathToPipeTypesStore, "*.csv", System.IO.SearchOption.TopDirectoryOnly);

            _repository = new PipeTypeRepository();
            _repository.Initialize(new PipeTypeDataLoaderCSV().Load(csvs));
        }
        public static void ListAllPipeTypes() => prdDbg(string.Join("\n", _repository.ListAllPipeTypes()));
        public static IEnumerable<IPipeType> GetPipeTypes() => _repository.GetPipeTypes();
        #endregion
        #endregion

        #region Pipe schedule methods
        private static string ExtractLayerName(Entity ent)
        {
            string layer = ent.Layer;
            if (layer.Contains("|")) return layer.Split('|').Last();
            else return layer;
        }
        private static string ExtractLayerName(string layer)
        {
            if (layer.Contains("|")) return layer.Split('|').Last();
            else return layer;
        }
        public static int GetPipeDN(string layer)
        {
            layer = ExtractLayerName(layer);
            if (layerNameDataParser.IsMatch(layer))
            {
                var dnstring = layerNameDataParser.Match(layer).Groups["DN"].Value;
                int dn;
                if (int.TryParse(dnstring, out dn)) return dn;
                return 0;
            }
            else
            {
                prdDbg($"Layer name {layer} failed to provide DN!");
                return 0;
            }
        }
        public static int GetPipeDN(Entity ent)
        {
            return GetPipeDN(ExtractLayerName(ent));
        }
        public static PipeTypeEnum GetPipeType(string layer)
        {
            layer = ExtractLayerName(layer);
            if (layerNameDataParser.IsMatch(layer))
            {
                var typeString = layerNameDataParser.Match(layer).Groups["TYPE"].Value;
                if (Enum.TryParse(typeString, true, out PipeTypeEnum type)) return type;
                return PipeTypeEnum.Ukendt;
            }
            return PipeTypeEnum.Ukendt;
        }
        public static PipeTypeEnum GetPipeType(Entity ent, bool FRtoEnkelt = false)
        {
            var type = GetPipeType(ExtractLayerName(ent));
            if (!FRtoEnkelt) return type;
            switch (type)
            {
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    return PipeTypeEnum.Enkelt;
                default: return type;
            }
        }
        public static PipeTypeEnum GetPipeType(Entity ent)
        {
            return GetPipeType(ExtractLayerName(ent));
        }
        public static PipeSystemEnum GetPipeSystem(string layer)
        {
            layer = ExtractLayerName(layer);
            if (layerNameDataParser.IsMatch(layer))
            {
                var systemString = layerNameDataParser.Match(layer).Groups["DATATYPE"].Value;
                PipeSystemEnum system;
                if (systemDict.TryGetValue(systemString, out system)) return system;
                return PipeSystemEnum.Ukendt;
            }
            return PipeSystemEnum.Ukendt;
        }
        public static PipeSystemEnum GetPipeSystem(Entity ent) => GetPipeSystem(ExtractLayerName(ent));
        public static double GetPipeOd(Entity ent)
        {
            int dn = GetPipeDN(ent);
            PipeSystemEnum system = GetPipeSystem(ent);
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetPipeOd(dn);
        }
        public static double GetPipeOd(PipeSystemEnum system, int dn)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetPipeOd(dn);
        }
        public static double GetPipeId(Entity ent)
        {
            int dn = GetPipeDN(ent);
            PipeSystemEnum system = GetPipeSystem(ent);
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetPipeId(dn);
        }
        public static double GetPipeThk(PipeSystemEnum system, int dn)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetPipeThk(dn);
        }
        public static double GetPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            PipeTypeEnum type = GetPipeType(ent, true);
            PipeSystemEnum system = GetPipeSystem(ent);

            if (!systemDictReversed.ContainsKey(system)) return 0.0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            int dn = GetPipeDN(ent);

            return pipeType.GetPipeKOd(dn, type, pipeSeries);
        }
        public static double GetPipeKOd(Entity ent, bool hardFail = false)
        {
            var series = GetPipeSeriesV2(ent, hardFail);
            var kOd = GetPipeKOd(ent, series);
            if (kOd == 0) prdDbg($"Ent {ent.Handle} has 0 kOd!");
            return kOd;
        }
        public static double GetPipeKOd(PipeSystemEnum system, int dn, PipeTypeEnum type, PipeSeriesEnum pipeSeries)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetPipeKOd(dn, type, pipeSeries);
        }
        public static double GetPipekThk(PipeSystemEnum system, int dn, PipeTypeEnum type, PipeSeriesEnum pipeSeries)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetPipekThk(dn, type, pipeSeries);
        }
        public static double GetPipeDistanceForTwin(PipeSystemEnum system, int dn, PipeTypeEnum type)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetPipeDistanceForTwin(dn, type);
        }
        public static PipeSeriesEnum GetPipeSeriesV2(Entity ent, bool hardFail = false)
        {
            if (ent is BlockReference br)
            {
                prdDbg($"GetPipeSeriesV2 cannot be called on a BlockReference! {br.RealName()} - {br.Handle}");
                throw new Exception();
            }

            double realKod;
            try
            {
                realKod = ((Polyline)ent).ConstantWidth * 1000;
            }
            catch (Exception)
            {
                if (hardFail) throw new Exception($"Ent {ent.Handle} ConstantWidth threw an exception!");
                prdDbg($"Ent {ent.Handle} ConstantWidth threw an exception!");
                return PipeSeriesEnum.Undefined;
            }

            PipeSystemEnum system = GetPipeSystem(ent);
            if (!systemDictReversed.ContainsKey(system))
            {
                if (hardFail) throw new Exception(
                    $"Ent {ent.Handle} has an undefined PipeSystem {system}!");
                return PipeSeriesEnum.Undefined;
            }
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            PipeTypeEnum type = GetPipeType(ent, true);
            int dn = GetPipeDN(ent);

            PipeSeriesEnum series = pipeType.GetPipeSeries(dn, type, realKod);

            if (hardFail && series == PipeSeriesEnum.Undefined)
                throw new Exception($"Determination of Series failed for pipe: {ent.Handle}!");
            return series;
        }
        //public static double GetPipeStdLength(Entity ent) => GetPipeDN(ent) <= 80 ? 12 : 16;
        public static double GetPipeStdLength(Entity ent)
        {
            PipeSystemEnum system = GetPipeSystem(ent);

            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetPipeStdLength(
                GetPipeDN(ent),
                GetPipeType(ent)
                );
        }
        public static bool IsInSituBent(Entity ent)
        {
            PipeTypeEnum type = GetPipeType(ent);
            PipeSystemEnum system = GetPipeSystem(ent);
            int dn = GetPipeDN(ent);

            //Flexrør er altid insitu bukkede
            if (
                system == PipeSystemEnum.Kobberflex ||
                system == PipeSystemEnum.AluPex
                ) return true;

            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    throw new Exception(
                        $"IsInSituBent -> Entity handle {ent.Handle} has invalid layer!");
                case PipeTypeEnum.Twin:
                    if (GetPipeDN(ent) < 65) return true;
                    break;
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    if (GetPipeDN(ent) < 100) return true;
                    break;
                default:
                    throw new Exception(
                        $"IsInSituBent -> Entity handle {ent.Handle} has invalid layer!");
            }
            return false;
        }
        public static bool IsInSituBent(PipeSystemEnum ps, int dn, PipeTypeEnum pt)
        {
            //Flexrør er altid insitu bukkede
            if (
                ps == PipeSystemEnum.Kobberflex ||
                ps == PipeSystemEnum.AluPex
                ) return true;

            switch (pt)
            {
                case PipeTypeEnum.Ukendt:
                    throw new Exception(
                        $"IsInSituBent -> {ps} {dn} {pt} Ukendt PipeTypeEnum (system)!");
                case PipeTypeEnum.Twin:
                    if (dn < 65) return true;
                    break;
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    if (dn < 100) return true;
                    break;
                default:
                    throw new Exception(
                        $"IsInSituBent -> {ps} {dn} {pt} Undefined PipeTypeEnum (system)!");
            }
            return false;
        }
        public static double GetPipeMinElasticRadiusHorizontalCharacteristic(Entity ent, bool considerInSituBending = true)
        {
            if (considerInSituBending && IsInSituBent(ent)) return 0;

            PipeTypeEnum type = GetPipeType(ent, true);
            PipeSystemEnum system = GetPipeSystem(ent);
            int dn = GetPipeDN(ent);

            return GetPipeMinElasticRadiusHorizontalCharacteristic(system, dn, type);
        }
        public static double GetPipeMinElasticRadiusHorizontalCharacteristic(PipeSystemEnum ps, int dn, PipeTypeEnum type,
            bool considerInSituBending = true)
        {
            if (considerInSituBending && IsInSituBent(ps, dn, type)) return 0;

            if (!systemDictReversed.ContainsKey(ps)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[ps]);

            return pipeType.GetMinElasticRadius(dn, type);
        }
        //public static double GetPipeMinElasticRadiusHorizontalDesign(Entity ent, bool considerInSituBending = true)
        //{
        //    return GetPipeMinElasticRadiusHorizontalCharacteristic(ent, considerInSituBending) * 1.2;
        //}
        public static double GetPipeMinElasticRadiusVerticalCharacteristic(Entity ent)
        {
            PipeSystemEnum system = GetPipeSystem(ent);
            int dn = GetPipeDN(ent);
            PipeTypeEnum type = GetPipeType(ent, true);

            return GetPipeMinElasticRadiusVerticalCharacteristic(system, dn, type);
        }
        public static double GetPipeMinElasticRadiusVerticalCharacteristic(PipeSystemEnum system, int dn, PipeTypeEnum type)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            var vertFactor = pipeType.GetFactorForVerticalElasticBending(dn, type);

            return vertFactor * GetPipeMinElasticRadiusHorizontalCharacteristic(system, dn, type, false);
        }
        //public static double GetPipeMinElasticRadiusVerticalDesign(Entity ent)
        //{
        //    return GetPipeMinElasticRadiusVerticalCharacteristic(ent) * 1.2;
        //}
        //public static double GetPipeMinElasticRadiusVerticalDesign(PipeSystemEnum system, int dn, PipeTypeEnum type)
        //{
        //    return GetPipeMinElasticRadiusVerticalCharacteristic(system, dn, type) * 1.2;
        //}
        public static double GetBuerorMinRadius(Entity ent)
        {
            int dn = GetPipeDN(ent);
            //int std = (int)GetPipeStdLength(ent);
            int std = 12; //Bruger kun radier for 12m rør bestemt af ledelsen

            double rad = GetBuerorMinRadius(ent, "Isoplus", std);

            return rad;
        }
        /// <param name="company">Logstor, Isoplus</param>
        public static double GetBuerorMinRadius(Entity ent, string company, int pipeLength = 12)
        {
            PipeSystemEnum system = GetPipeSystem(ent);
            int dn = GetPipeDN(ent);
            IPipeRadiusData radiusData = _radiiRepo.GetPipeRadiusData(company);
            double rad = radiusData.GetBuerorMinRadius(dn, pipeLength);
            return rad;
        }
        public static double AskForBuerorMinRadius(Entity ent, int pipeLength = 12)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptKeywordOptions options = new PromptKeywordOptions("\nChoose an option [Isoplus/Logstor]: ")
            {
                AllowNone = false,
            };
            options.Keywords.Add("Logstor");
            options.Keywords.Add("Isoplus");
            options.Keywords.Default = "Isoplus";

            PromptResult result = ed.GetKeywords(options);

            if (result.Status == PromptStatus.OK)
            {
                return GetBuerorMinRadius(ent, result.StringResult, pipeLength);
            }
            else
            {
                return GetBuerorMinRadius(ent, "Isoplus", pipeLength);
            }
        }
        public static string GetLabel(Entity ent)
        {
            int DN = GetPipeDN(ent);
            if (DN == 0)
            {
                prdDbg($"Layer {ExtractLayerName(ent)} failed to provide DN!");
                return "";
            }
            var type = GetPipeType(ent);
            if (type == PipeTypeEnum.Ukendt)
            {
                prdDbg("Kunne ikke finde systemet på valgte rør! Kontroller lag!");
                return "";
            }
            double od = GetPipeOd(ent);
            if (od < 1.0)
            {
                prdDbg("Kunne ikke finde rørdimensionen på valgte rør! Kontroller lag!");
                return "";
            }
            double kOd = GetPipeKOd(ent);
            if (kOd < 1.0)
            {
                prdDbg("Kunne ikke finde kappediameter på valgte rør! Kontroller lag!");
                return "";
            }
            PipeSystemEnum system = GetPipeSystem(ent);
            if (system == PipeSystemEnum.Ukendt)
            {
                prdDbg("Kunne ikke finde system på valgte rør! Kontroller lag!");
                return "";
            }
            if (!systemDictReversed.ContainsKey(system))
            {
                prdDbg("Kunne ikke finde system på valgte rør! Kontroller lag!");
                return "";
            }
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            string label = pipeType.GetLabel(DN, type, od, kOd);

#if DEBUG
            prdDbg($"DN: {DN}; Type: {type}; OD: {od}; kOD: {kOd}; System: {system}; PipeType: {pipeType.Name}");
            prdDbg($"Label: {label}");
#endif

            return label;
        }
        public static short GetLayerColor(PipeSystemEnum system, PipeTypeEnum type)
        {
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetLayerColor(type);
        }
        public static short GetLayerColor(Entity ent)
        {
            PipeSystemEnum system = GetPipeSystem(ent);
            PipeTypeEnum type = GetPipeType(ent, true);

            return GetLayerColor(system, type);
        }
        public static double GetTrenchWidth(
            int DN, PipeSystemEnum system, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"GetTrenchWidth received unknown system: {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetTrenchWidth(DN, type, series);
        }
        public static double GetOffset(
            int DN, PipeSystemEnum system, PipeTypeEnum type, PipeSeriesEnum series,
            double pathWidth, double offsetSupplement = 0)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"GetOffset received unknown system: {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            var offset = pipeType.GetOffset(DN, type, series);
            if (pathWidth > 7.5) offset += 0.15;
            return offset += offsetSupplement;
        }
        public static double GetOffset(
            Entity ent,
            double pathWidth, double offsetSupplement = 0)
        {
            return GetOffset(ent, GetPipeSeriesV2(ent), pathWidth, offsetSupplement);
        }
        public static double GetOffset(
            Entity ent, PipeSeriesEnum series,
            double pathWidth, double offsetSupplement = 0)
        {
            var system = GetPipeSystem(ent);
            var dn = GetPipeDN(ent);
            var type = GetPipeType(ent);

            return GetOffset(dn, system, type, series, pathWidth, offsetSupplement);
        }
        public static short GetColorForDim(string layer)
        {
            layer = ExtractLayerName(layer);
            var type = GetPipeType(layer);
            var system = GetPipeSystem(layer);
            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);
            return pipeType.GetSizeColor(GetPipeDN(layer), type);
        }
        /// <summary>
        /// Returns twin if below limit, else single
        /// </summary>
        public static PipeTypeEnum GetPipeTypeByAvailability(PipeSystemEnum ps, int dn)
        {
            if (!systemDictReversed.ContainsKey(ps)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[ps]);

            return pipeType.GetPipeTypeByAvailability(dn);
        }
        public static string GetLineTypeLayerPrefix(PipeSystemEnum system)
        {
            if (!lineTypePrefixDict.ContainsKey(system))
                throw new Exception($"Undefined PipeType system {system}!");
            return lineTypePrefixDict[system];
        }
        /// <summary>
        /// Currently set to return 0.6 m.
        /// </summary>        
        internal static double GetCoverDepth(int DN, PipeSystemEnum ps, PipeTypeEnum pt)
        {
            //Temporary solution to get cover depth from size entry
            return 0.6;
        }
        public static string GetLayerName(int DN, PipeSystemEnum ps, PipeTypeEnum pt)
        {
            return string.Concat(
                    "FJV-", pt.ToString(), "-", GetSystemString(ps), DN.ToString()).ToUpper();
        }
        #endregion
    }

    //All PipeScheduleV2 classes are gathered in here to simplify sharing

    #region POCOs
    /// <summary>
    /// Strongly-typed record for pipe schedule CSV data.
    /// </summary>
    public sealed record PipeScheduleEntry(
        int DN,
        PipeTypeEnum PipeType,
        PipeSeriesEnum PipeSeries,
        double pOd,
        double pThk,
        double kOd,
        double kThk,
        double pDst,
        double tWdth,
        double minElasticRadii,
        double VertFactor,
        short color,
        double DefaultL,
        double OffsetUnder7_5
    )
    {
        /// <summary>
        /// Parses a string[] row from CSV into a PipeScheduleEntry.
        /// </summary>
        public static PipeScheduleEntry Parse(string[] row)
        {
            return new PipeScheduleEntry(
                DN: int.TryParse(row[0], out var dn) ? dn : 0,
                PipeType: Enum.TryParse<PipeTypeEnum>(row[1], true, out var pt) ? pt : PipeTypeEnum.Ukendt,
                PipeSeries: Enum.TryParse<PipeSeriesEnum>(row[2], true, out var ps) ? ps : PipeSeriesEnum.Undefined,
                pOd: double.TryParse(row[3], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pod) ? pod : 0,
                pThk: double.TryParse(row[4], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pthk) ? pthk : 0,
                kOd: double.TryParse(row[5], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var kod) ? kod : 0,
                kThk: double.TryParse(row[6], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var kthk) ? kthk : 0,
                pDst: double.TryParse(row[7], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pdst) ? pdst : 0,
                tWdth: double.TryParse(row[8], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var twdth) ? twdth : 0,
                minElasticRadii: double.TryParse(row[9], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var mer) ? mer : 0,
                VertFactor: double.TryParse(row[10], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var vf) ? vf : 0,
                color: short.TryParse(row[11], out var col) ? col : (short)0,
                DefaultL: double.TryParse(row[12], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var dl) ? dl : 0,
                OffsetUnder7_5: row.Length > 13 && double.TryParse(row[13], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var offset) ? offset : 0
            );
        }
    }

    /// <summary>
    /// Strongly-typed record for pipe radius CSV data.
    /// </summary>
    public sealed record PipeRadiusEntry(
        int DN,
        PipeTypeEnum PipeType,
        int PipeLength,
        double BRpmin,
        double ERpmin
    )
    {
        /// <summary>
        /// Parses a string[] row from CSV into a PipeRadiusEntry.
        /// </summary>
        public static PipeRadiusEntry Parse(string[] row)
        {
            return new PipeRadiusEntry(
                DN: int.TryParse(row[0], out var dn) ? dn : 0,
                PipeType: Enum.TryParse<PipeTypeEnum>(row[1], true, out var pt) ? pt : PipeTypeEnum.Ukendt,
                PipeLength: int.TryParse(row[2], out var pl) ? pl : 0,
                BRpmin: double.TryParse(row[3], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var br) ? br : 0,
                ERpmin: double.TryParse(row[4], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var er) ? er : 0
            );
        }
    }
    #endregion

    #region All classes
    public interface IPipeType
    {
        string Name { get; }
        PipeSystemEnum System { get; }
        void Initialize(IEnumerable<PipeScheduleEntry> entries, PipeSystemEnum pipeSystemEnum);
        double GetPipeOd(int dn);
        double GetPipeThk(int dn);
        double GetPipeId(int dn);
        PipeSeriesEnum GetPipeSeries(
            int dn, PipeTypeEnum type, double realKod);
        double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum pipeSeries);
        double GetPipekThk(int dn, PipeTypeEnum type, PipeSeriesEnum pipeSeries);
        double GetPipeDistanceForTwin(int dn, PipeTypeEnum type);
        double GetMinElasticRadius(int dn, PipeTypeEnum type);
        double GetFactorForVerticalElasticBending(int dn, PipeTypeEnum type);
        double GetPipeStdLength(int dn, PipeTypeEnum type);
        IEnumerable<int> ListAllDnsForPipeTypeSerie(PipeTypeEnum type, PipeSeriesEnum serie);
        string GetLabel(int DN, PipeTypeEnum type, double od, double kOd);
        short GetLayerColor(PipeTypeEnum type);
        double GetTrenchWidth(int dN, PipeTypeEnum type, PipeSeriesEnum series);
        short GetSizeColor(int dn, PipeTypeEnum type);
        IEnumerable<int> ListAllDnsForPipeType(PipeTypeEnum type);
        IEnumerable<PipeTypeEnum> GetAvailableTypes();
        IEnumerable<PipeSeriesEnum> GetAvailableSeriesForType(PipeTypeEnum type);
        double GetDefaultLengthForDnAndType(int DN, PipeTypeEnum type);
        PipeTypeEnum GetPipeTypeByAvailability(int dn);
        double GetOffset(int dN, PipeTypeEnum type, PipeSeriesEnum series);
    }
    public abstract class PipeTypeBase : IPipeType
    {
        private PipeSystemEnum _system;
        public PipeSystemEnum System => _system;
        protected List<PipeScheduleEntry> _entries = new();
        public string Name => this.GetType().Name;

        // Lookup indexes for O(1) access
        private ILookup<int, PipeScheduleEntry>? _byDn;
        private ILookup<(int, PipeTypeEnum), PipeScheduleEntry>? _byDnType;
        private Dictionary<(int, PipeTypeEnum, PipeSeriesEnum), PipeScheduleEntry>? _byDnTypeSeries;

        private static PipeTypeEnum NormalizeType(PipeTypeEnum type) =>
            type == PipeTypeEnum.Retur || type == PipeTypeEnum.Frem ? PipeTypeEnum.Enkelt : type;

        public void Initialize(IEnumerable<PipeScheduleEntry> entries, PipeSystemEnum pipeSystemEnum)
        {
            _entries = entries.ToList();
            _system = pipeSystemEnum;

            // Build lookup indexes
            _byDn = _entries.ToLookup(e => e.DN);
            _byDnType = _entries.ToLookup(e => (e.DN, e.PipeType));
            _byDnTypeSeries = new Dictionary<(int, PipeTypeEnum, PipeSeriesEnum), PipeScheduleEntry>();
            foreach (var entry in _entries)
            {
                var key = (entry.DN, entry.PipeType, entry.PipeSeries);
                if (!_byDnTypeSeries.ContainsKey(key))
                    _byDnTypeSeries[key] = entry;
            }
        }

        public virtual double GetPipeOd(int dn)
        {
            var result = _byDn?[dn].FirstOrDefault();
            return result?.pOd ?? 0;
        }

        public virtual double GetPipeThk(int dn)
        {
            var result = _byDn?[dn].FirstOrDefault();
            return result?.pThk ?? 0;
        }

        public virtual double GetPipeId(int dn)
        {
            var result = _byDn?[dn].FirstOrDefault();
            if (result == null) return 0;
            return result.pOd - 2 * result.pThk;
        }

        public virtual PipeSeriesEnum GetPipeSeries(int dn, PipeTypeEnum type, double realKod)
        {
            type = NormalizeType(type);
            var results = _byDnType?[(dn, type)] ?? Enumerable.Empty<PipeScheduleEntry>();

            foreach (var entry in results)
            {
                if (entry.kOd.Equalz(realKod, 0.001))
                    return entry.PipeSeries;
            }
            return PipeSeriesEnum.Undefined;
        }

        public virtual double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            type = NormalizeType(type);
            if (_byDnTypeSeries != null && _byDnTypeSeries.TryGetValue((dn, type, series), out var entry))
                return entry.kOd;
            return 0;
        }

        public virtual double GetPipekThk(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            type = NormalizeType(type);
            if (_byDnTypeSeries != null && _byDnTypeSeries.TryGetValue((dn, type, series), out var entry))
                return entry.kThk;
            return 0;
        }

        public virtual double GetPipeDistanceForTwin(int dn, PipeTypeEnum type)
        {
            if (type == PipeTypeEnum.Enkelt ||
                type == PipeTypeEnum.Frem ||
                type == PipeTypeEnum.Retur) return 0;

            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.pDst ?? 0;
        }

        public virtual double GetMinElasticRadius(int dn, PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.minElasticRadii ?? 0;
        }

        public double GetFactorForVerticalElasticBending(int dn, PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.VertFactor ?? 0;
        }

        public double GetPipeStdLength(int dn, PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.DefaultL ?? 999;
        }

        public virtual IEnumerable<int> ListAllDnsForPipeTypeSerie(PipeTypeEnum type, PipeSeriesEnum series)
        {
            type = NormalizeType(type);
            var results = _entries
                .Where(e => e.PipeType == type && e.PipeSeries == series)
                .Select(e => e.DN);
            return results.Any() ? results : null;
        }

        public abstract string GetLabel(int DN, PipeTypeEnum type, double od, double kOd);

        public virtual short GetLayerColor(PipeTypeEnum type)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return 0;
                case PipeTypeEnum.Twin:
                    return 6;
                case PipeTypeEnum.Frem:
                    return 1;
                case PipeTypeEnum.Retur:
                    return 5;
                case PipeTypeEnum.Enkelt:
                    return 0;
                default: return 0;
            }
        }

        public virtual double GetTrenchWidth(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            type = NormalizeType(type);
            if (_byDnTypeSeries != null && _byDnTypeSeries.TryGetValue((dn, type, series), out var entry))
                return entry.tWdth;
            return 1000000;
        }

        public virtual double GetOffset(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            type = NormalizeType(type);

            // Normalize requested series to available range for given DN and Type
            var availableEntries = _byDnType?[(dn, type)]?.ToList();
            if (availableEntries != null && availableEntries.Count > 0)
            {
                var availableSeries = availableEntries
                    .Where(e => e.PipeSeries != PipeSeriesEnum.Undefined)
                    .Select(e => e.PipeSeries)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();

                if (availableSeries.Count > 0)
                {
                    var minSeries = availableSeries.First();
                    var maxSeries = availableSeries.Last();
                    if (series < minSeries) series = minSeries;
                    if (series > maxSeries) series = maxSeries;
                }
            }

            if (_byDnTypeSeries != null && _byDnTypeSeries.TryGetValue((dn, type, series), out var entry))
                return entry.OffsetUnder7_5;
            return 100;
        }

        public short GetSizeColor(int dn, PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.color ?? 0;
        }

        public virtual IEnumerable<int> ListAllDnsForPipeType(PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var results = _entries
                .Where(e => e.PipeType == type)
                .Select(e => e.DN)
                .Distinct();
            return results.Any() ? results : null;
        }

        public virtual IEnumerable<PipeTypeEnum> GetAvailableTypes()
        {
            return _entries
                .Select(e => e.PipeType)
                .Distinct()
                .OrderBy(e => e);
        }

        public IEnumerable<PipeSeriesEnum> GetAvailableSeriesForType(PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var results = _entries
                .Where(e => e.PipeType == type)
                .Select(e => e.PipeSeries)
                .Distinct()
                .OrderBy(e => e);
            return results.Any() ? results : null;
        }

        public double GetDefaultLengthForDnAndType(int dn, PipeTypeEnum type)
        {
            type = NormalizeType(type);
            var result = _byDnType?[(dn, type)].FirstOrDefault();
            return result?.DefaultL ?? 999;
        }

        /// <summary>
        /// Gets pipe type based on availability.
        /// Biased towards Twin if available.
        /// </summary>
        public PipeTypeEnum GetPipeTypeByAvailability(int dn)
        {
            var results = _byDn?[dn];
            if (results != null && results.Any())
            {
                if (results.Any(e => e.PipeType == PipeTypeEnum.Twin))
                    return PipeTypeEnum.Twin;
                else
                    return PipeTypeEnum.Frem;
            }
            return PipeTypeEnum.Ukendt;
        }
    }
    public class PipeTypeDN : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"DN{DN}-ø{od.ToString("N1")}+ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"DN{DN}-ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
    }
    public class PipeTypeALUPEX : PipeTypeBase
    {

        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"AluPex{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"AluPex{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
    }
    public class PipeTypeCU : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"CU{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"CU{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
        public override double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;
            if (series == PipeSeriesEnum.S3) series = PipeSeriesEnum.S2;
            var result = _entries.FirstOrDefault(e => e.DN == dn && e.PipeType == type && e.PipeSeries == series);
            return result?.kOd ?? 0;
        }
    }
    public class PipeTypePEXU : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"PEX{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"PEX{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
        public override double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;
            if (series != PipeSeriesEnum.S3) series = PipeSeriesEnum.S3;
            var result = _entries.FirstOrDefault(e => e.DN == dn && e.PipeType == type && e.PipeSeries == series);
            return result?.kOd ?? 0;
        }
        public override short GetLayerColor(PipeTypeEnum type)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return 0;
                case PipeTypeEnum.Twin:
                    return 190;
                case PipeTypeEnum.Frem:
                    return 1;
                case PipeTypeEnum.Retur:
                    return 5;
                case PipeTypeEnum.Enkelt:
                    return 190;
                default: return 0;
            }
        }
    }
    public class PipeTypePRTFLEXL : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"PE-RT{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"PE-RT{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
        public override double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            //We IGNORE the series for this type as it only has ONE series!

            var result = _entries.FirstOrDefault(e => e.DN == dn && e.PipeType == type);
            return result?.kOd ?? 0;
        }
        public override short GetLayerColor(PipeTypeEnum type)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return 0;
                case PipeTypeEnum.Twin:
                    return 190;
                case PipeTypeEnum.Frem:
                    return 1;
                case PipeTypeEnum.Retur:
                    return 5;
                case PipeTypeEnum.Enkelt:
                    return 190;
                default: return 0;
            }
        }
    }
    public class PipeTypeAQTHRM11 : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"AT{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"AT{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                default:
                    return "";
            }
        }
        public override double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            //We IGNORE the series for this type as it only has ONE series!

            var result = _entries.FirstOrDefault(e => e.DN == dn && e.PipeType == type);
            return result?.kOd ?? 0;
        }
        public override short GetLayerColor(PipeTypeEnum type)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return 0;
                case PipeTypeEnum.Twin:
                    return 200;
                case PipeTypeEnum.Frem:
                    return 20;
                case PipeTypeEnum.Retur:
                    return 160;
                case PipeTypeEnum.Enkelt:
                    return 200;
                default: return 0;
            }
        }
    }
    public class PipeTypePE : PipeTypeBase
    {
        public override string GetLabel(int DN, PipeTypeEnum type, double od, double kOd)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return "";
                case PipeTypeEnum.Twin:
                    return $"PE-ø{DN}+ø{DN}";
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return $"PE-ø{DN}";
                default:
                    return "";
            }
        }
        public override double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            //We IGNORE the series for this type as it only has ONE series!

            var result = _entries.FirstOrDefault(e => e.DN == dn && e.PipeType == type);
            return result?.kOd ?? 0;
        }
        public override short GetLayerColor(PipeTypeEnum type)
        {
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    return 0;
                case PipeTypeEnum.Twin:
                    return 200;
                case PipeTypeEnum.Frem:
                    return 20;
                case PipeTypeEnum.Retur:
                    return 160;
                case PipeTypeEnum.Enkelt:
                    return 200;
                default: return 0;
            }
        }
    }
    public interface IPipeTypeRepository
    {
        void Initialize(Dictionary<string, IPipeType> pipeTypeDict);
        IPipeType GetPipeType(string type);
        IEnumerable<string> ListAllPipeTypes();
        IEnumerable<IPipeType> GetPipeTypes();
    }
    public class PipeTypeRepository : IPipeTypeRepository
    {
        private Dictionary<string, IPipeType> _pipeTypeDictionary = new Dictionary<string, IPipeType>();
        public IPipeType GetPipeType(string type)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentNullException($"PipeType is Null or Empty!");

            if (_pipeTypeDictionary.ContainsKey(type)) return _pipeTypeDictionary[type];
            else throw new ArgumentNullException($"PipeType {type} does not exist!");
        }
        public void Initialize(Dictionary<string, IPipeType> pipeTypeDict)
        {
            _pipeTypeDictionary = pipeTypeDict;
        }
        public IEnumerable<string> ListAllPipeTypes()
        {
            foreach (var k in _pipeTypeDictionary) yield return k.Key;
        }
        public IEnumerable<IPipeType> GetPipeTypes()
        {
            foreach (var k in _pipeTypeDictionary) yield return k.Value;
        }
    }
    public class PipeTypeDataLoaderCSV
    {
        public Dictionary<string, IPipeType> Load(IEnumerable<string> paths)
        {
            Dictionary<string, IPipeType> dict = new Dictionary<string, IPipeType>();
            foreach (var path in paths)
            {
                string type = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!PipeScheduleV2.typeDict.ContainsKey(type))
                {
                    prdDbg($"PipeType {type} is not defined in PipeScheduleV2!");
                    continue;
                }

                // Load CSV using DataManager and parse to POCOs
                var csvData = PipeScheduleCsv.GetOrLoad(path);
                var entries = csvData.Rows.Select(PipeScheduleEntry.Parse);

                IPipeType pipeType = Activator.CreateInstance(
                    PipeScheduleV2.typeDict[type]) as IPipeType;
                pipeType.Initialize(entries, PipeScheduleV2.systemDict[type]);
                dict.Add(type, pipeType);
            }

            return dict;
        }
    }
    public interface IPipeRadiusData
    {
        void Initialize(IEnumerable<PipeRadiusEntry> entries, CompanyEnum compType);
        double GetBuerorMinRadius(int dn, int pipeLength);
        CompanyEnum Company { get; }
    }
    public abstract class PipeRadiusData : IPipeRadiusData
    {
        protected List<PipeRadiusEntry> _entries = new();
        protected CompanyEnum _company;
        public CompanyEnum Company => _company;

        // Lookup index for O(1) access
        private Dictionary<(int, int), PipeRadiusEntry>? _byDnLength;

        public void Initialize(IEnumerable<PipeRadiusEntry> entries, CompanyEnum companyEnum)
        {
            _entries = entries.ToList();
            _company = companyEnum;

            // Build lookup index
            _byDnLength = new Dictionary<(int, int), PipeRadiusEntry>();
            foreach (var entry in _entries)
            {
                var key = (entry.DN, entry.PipeLength);
                if (!_byDnLength.ContainsKey(key))
                    _byDnLength[key] = entry;
            }
        }

        public double GetBuerorMinRadius(int dn, int pipeLength)
        {
            if (_byDnLength != null && _byDnLength.TryGetValue((dn, pipeLength), out var entry))
                return entry.BRpmin;
            return 0;
        }
    }
    public class Logstor : PipeRadiusData
    {

    }
    public class Isoplus : PipeRadiusData
    {

    }
    public interface IPipeRadiusDataRepository
    {
        void Initialize(Dictionary<string, IPipeRadiusData> pipeRadiusDataDict);
        IPipeRadiusData GetPipeRadiusData(string company);
        IEnumerable<string> ListAllPipeRadiusData();
        IEnumerable<IPipeRadiusData> GetPipeRadiusData();
    }
    public class PipeRadiusDataRepository : IPipeRadiusDataRepository
    {
        private Dictionary<string, IPipeRadiusData> _pipeRadiusDataDictionary
            = new Dictionary<string, IPipeRadiusData>();
        public IPipeRadiusData GetPipeRadiusData(string company)
        {
            if (string.IsNullOrEmpty(company))
                throw new ArgumentNullException($"PipeRadiusData is Null or Empty!");

            if (_pipeRadiusDataDictionary.ContainsKey(company)) return _pipeRadiusDataDictionary[company];
            else throw new ArgumentNullException($"PipeRadiusData {company} does not exist!");
        }
        public void Initialize(Dictionary<string, IPipeRadiusData> pipeRadiusDataDict)
        {
            _pipeRadiusDataDictionary = pipeRadiusDataDict;
        }
        public IEnumerable<string> ListAllPipeRadiusData()
        {
            foreach (var k in _pipeRadiusDataDictionary) yield return k.Key;
        }
        public IEnumerable<IPipeRadiusData> GetPipeRadiusData()
        {
            foreach (var k in _pipeRadiusDataDictionary) yield return k.Value;
        }
    }
    public class PipeRadiusDataLoaderCSV
    {
        public Dictionary<string, IPipeRadiusData> Load(IEnumerable<string> paths)
        {
            Dictionary<string, IPipeRadiusData> dict = new Dictionary<string, IPipeRadiusData>();
            foreach (var path in paths)
            {
                string company = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!PipeScheduleV2.companyDict.ContainsKey(company))
                {
                    prdDbg($"PipeRadii {company} is not defined in PipeScheduleV2!");
                    continue;
                }

                // Load CSV using DataManager and parse to POCOs
                var csvData = PipeRadiusCsv.GetOrLoad(path);
                var entries = csvData.Rows.Select(PipeRadiusEntry.Parse);

                IPipeRadiusData pipeRadiusData = Activator.CreateInstance(
                    PipeScheduleV2.companyDict[company]) as IPipeRadiusData;
                pipeRadiusData.Initialize(entries, PipeScheduleV2.companyEnumDict[company]);
                dict.Add(company, pipeRadiusData);
            }

            return dict;
        }
    }
    #endregion
}