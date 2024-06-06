using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using DataTable = System.Data.DataTable;
using DataColumn = System.Data.DataColumn;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipeScheduleV2
{
    public static class PipeScheduleV2
    {
        private static IPipeTypeRepository _repository;

        static PipeScheduleV2()
        {
            LoadPipeTypeData(@"X:\AutoCAD DRI - 01 Civil 3D\PipeSchedule\");
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
        public static Dictionary<string, Type> typeDict = new Dictionary<string, Type>()
        {
            { "DN", typeof(PipeTypeDN) },
            { "ALUPEX", typeof(PipeTypeALUPEX) },
            { "CU", typeof(PipeTypeCU) },
            { "PEXU", typeof(PipeTypePEXU) },
            { "PRTFLEXL", typeof(PipeTypePRTFLEXL) },
        };
        private static Dictionary<string, PipeSystemEnum> systemDict = new Dictionary<string, PipeSystemEnum>()
        {
            {"DN", PipeSystemEnum.Stål },
            {"ALUPEX", PipeSystemEnum.AluPex },
            {"CU", PipeSystemEnum.Kobberflex },
            {"PEXU", PipeSystemEnum.PexU },
            {"PRTFLEXL", PipeSystemEnum.PertFlextra },
        };
        private static Dictionary<PipeSystemEnum, string> systemDictReversed = new Dictionary<PipeSystemEnum, string>()
        {
            {PipeSystemEnum.Stål, "DN" },
            {PipeSystemEnum.AluPex , "ALUPEX" },
            {PipeSystemEnum.Kobberflex , "CU" },
            {PipeSystemEnum.PexU , "PEXU" },
            {PipeSystemEnum.PertFlextra , "PRTFLEXL" },
        };
        private static string pipeTypes = string.Join(
            "|", Enum.GetNames(typeof(PipeTypeEnum)).Select(x => x.ToUpper()));
        private static string pipeDataTypes = string.Join(
            "|", typeDict.Keys);
        private static Regex layerNameDataParser =
            new Regex($@"FJV-(?<TYPE>{pipeTypes})-(?<DATATYPE>{pipeDataTypes})(?<DN>\d+)");
        public static Dictionary<string, Type> columnTypeDict = new Dictionary<string, Type>()
        {
            {"DN", typeof(int)},
            {"PipeType", typeof(string)},
            {"PipeSeries" , typeof(string)},
            {"pOd", typeof(double)},
            {"kOd", typeof(double)},
            {"tWdth", typeof(double)},
            {"minElasticRadii", typeof(double)},
            {"VpMax12", typeof(double)},
            {"VpMax16", typeof(double)},
            {"color", typeof(short)},
            {"DefaultL", typeof(int)}
        };
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
        public static int GetPipeStdLength(Entity ent)
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
        public static double GetPipeMinElasticRadius(Entity ent, bool considerInSituBending = true)
        {
            if (considerInSituBending && IsInSituBent(ent)) return 0;

            PipeTypeEnum type = GetPipeType(ent, true);
            PipeSeriesEnum series = GetPipeSeriesV2(ent);
            PipeSystemEnum system = GetPipeSystem(ent);

            if (!systemDictReversed.ContainsKey(system)) return 0;
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            int dn = GetPipeDN(ent);

            var rad = pipeType.GetMinElasticRadius(dn, type, series);

            return rad;
        }
        public static double GetBuerorMinRadius(Entity ent)
        {
            PipeSystemEnum system = GetPipeSystem(ent);

            if (!systemDictReversed.ContainsKey(system)) return 0;
            IPipeType pipeType = _repository.GetPipeType(systemDictReversed[system]);

            int dn = GetPipeDN(ent);
            //int std = (int)GetPipeStdLength(ent);
            int std = 12; //Bruger kun radier for 12m rør

            double rad = pipeType.GetBuerorMinRadius(dn, std);

            return rad;
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
        public static double GetTrenchWidth(
            int DN, PipeSystemEnum system, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (!systemDictReversed.ContainsKey(system))
                throw new Exception($"GetTrenchWidht received unknown system: {system}!");
            var pipeType = _repository.GetPipeType(systemDictReversed[system]);

            return pipeType.GetTrenchWidth(DN, type, series);
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
        #endregion
    }

    //All PipeScheduleV2 classes are gathered in here to simplify sharing

    #region All classes
    public interface IPipeType
    {
        string Name { get; }
        void Initialize(DataTable table);
        double GetPipeOd(int dn);
        PipeSeriesEnum GetPipeSeries(
            int dn, PipeTypeEnum type, double realKod);
        double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum pipeSeries);
        double GetMinElasticRadius(int dn, PipeTypeEnum type, PipeSeriesEnum series);
        double GetBuerorMinRadius(int dn, int std);
        int GetPipeStdLength(int dn, PipeTypeEnum type);
        IEnumerable<int> ListAllDnsForPipeTypeSerie(PipeTypeEnum type, PipeSeriesEnum serie);
        string GetLabel(int DN, PipeTypeEnum type, double od, double kOd);
        short GetLayerColor(PipeTypeEnum type);
        double GetTrenchWidth(int dN, PipeTypeEnum type, PipeSeriesEnum series);
        short GetSizeColor(int dn, PipeTypeEnum type);
        IEnumerable<int> ListAllDnsForPipeType(PipeTypeEnum type);
        IEnumerable<PipeTypeEnum> GetAvailableTypes();
        IEnumerable<PipeSeriesEnum> GetAvailableSeriesForType(PipeTypeEnum type);
        int GetDefaultLengthForDnAndType(int DN, PipeTypeEnum type);
    }
    public abstract class PipeTypeBase : IPipeType
    {
        protected DataTable _data;
        public string Name => this.GetType().Name;
        public virtual double GetPipeOd(int dn)
        {
            DataRow[] results = _data.Select($"DN = {dn}");
            if (results != null && results.Length > 0)
                return (double)results[0]["pOd"];
            return 0;
        }
        private void ConvertDataTypes()
        {
            DataTable newTable = _data.Clone();

            #region Check if columns are missing from dict
            // Check for columns in originalTable not present in dictionary
            List<string> missingColumns = new List<string>();
            foreach (DataColumn col in _data.Columns)
                if (!PipeScheduleV2.columnTypeDict.ContainsKey(col.ColumnName))
                    missingColumns.Add(col.ColumnName);

            if (missingColumns.Count > 0)
                throw new Exception($"Missing data type definitions for columns: " +
                    $"{string.Join(", ", missingColumns)}");
            #endregion

            // Set data types based on dictionary
            foreach (var columnType in PipeScheduleV2.columnTypeDict)
                if (newTable.Columns.Contains(columnType.Key))
                    newTable.Columns[columnType.Key].DataType = columnType.Value;

            foreach (DataRow row in _data.Rows) newTable.ImportRow(row);

            _data = newTable;
        }
        public void Initialize(DataTable table)
        { _data = table; ConvertDataTypes(); }
        public virtual PipeSeriesEnum GetPipeSeries(
            int dn, PipeTypeEnum type, double realKod)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}'");

            foreach (DataRow row in results)
            {
                double kOd = (double)row["kOd"];
                if (kOd.Equalz(realKod, 0.001))
                {
                    string sS = (string)row["PipeSeries"];
                    if (Enum.TryParse(sS, true, out PipeSeriesEnum series)) return series;
                    return PipeSeriesEnum.Undefined;
                }
            }
            return PipeSeriesEnum.Undefined;
        }
        public virtual double GetPipeKOd(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
        }
        public virtual double GetMinElasticRadius(int dn, PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["minElasticRadii"];
            return 0;
        }
        public abstract double GetBuerorMinRadius(int dn, int std);
        public int GetPipeStdLength(int dn, PipeTypeEnum type)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}'");
            if (results != null && results.Length > 0) return (int)results[0]["DefaultL"];
            else return 999;
        }
        public virtual IEnumerable<int> ListAllDnsForPipeTypeSerie(PipeTypeEnum type, PipeSeriesEnum series)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return results.Select(x => (int)x["DN"]);
            return null;
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
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["tWdth"];
            return 1000000;
        }
        public short GetSizeColor(int dn, PipeTypeEnum type)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}'");
            if (results != null && results.Length > 0) return (short)results[0]["color"];
            return 0;
        }
        public virtual IEnumerable<int> ListAllDnsForPipeType(PipeTypeEnum type)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"PipeType = '{type}'");
            if (results != null && results.Length > 0)
                return results.Select(x => (int)x["DN"]).Distinct();
            return null;
        }
        public virtual IEnumerable<PipeTypeEnum> GetAvailableTypes()
        {
            var typeStrings = _data.Select().Select(x => (string)x["PipeType"])
                .Distinct().OrderBy(x => x);
            foreach (var typeString in typeStrings)
                if (Enum.TryParse(typeString, true, out PipeTypeEnum type))
                    yield return type;
        }
        public IEnumerable<PipeSeriesEnum> GetAvailableSeriesForType(PipeTypeEnum type)
        {
            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"PipeType = '{type}'");
            if (results != null && results.Length > 0)
                return results.Select(x => (string)x["PipeSeries"])
                    .Distinct().OrderBy(x => x)
                    .Select(x => (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum), x));
            return null;
        }
        public int GetDefaultLengthForDnAndType(int dn, PipeTypeEnum type)
        {
            //It is assumed that series does not matter for default length

            if (type == PipeTypeEnum.Retur ||
                type == PipeTypeEnum.Frem)
                type = PipeTypeEnum.Enkelt;

            DataRow[] results = 
                _data.Select($"DN = {dn} AND PipeType = '{type}'");
            if (results != null && results.Length > 0) return (int)results[0]["DefaultL"];
            return 999;
        }
    }
    public class PipeTypeDN : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std)
        {
            DataRow[] results = _data.Select($"DN = {dn}");

            if (results != null && results.Length > 0)
            {
                double vpMax12 = (double)results[0]["VpMax12"];
                if (vpMax12 == 0) return 0;
                return (180 * std) / (Math.PI * vpMax12);
            }
            return 0;
        }
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
        public override double GetBuerorMinRadius(int dn, int std) => 0.0;

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
        public override double GetBuerorMinRadius(int dn, int std) => 0.0;
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
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
        }
    }
    public class PipeTypePEXU : PipeTypeBase
    {
        public override double GetBuerorMinRadius(int dn, int std) => 0;
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
            var results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
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
        public override double GetBuerorMinRadius(int dn, int std) => 0;
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
            if (series != PipeSeriesEnum.S3) series = PipeSeriesEnum.S3;
            var results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
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
                DataTable dataTable = CsvReader.ReadCsvToDataTable(path, type);
                if (!PipeScheduleV2.typeDict.ContainsKey(type))
                    throw new Exception($"PipeType {type} is not defined in PipeScheduleV2!");
                IPipeType pipeType = Activator.CreateInstance(
                    PipeScheduleV2.typeDict[type]) as IPipeType;
                pipeType.Initialize(dataTable);
                dict.Add(type, pipeType);
            }

            return dict;
        }
    }
    #endregion
}
