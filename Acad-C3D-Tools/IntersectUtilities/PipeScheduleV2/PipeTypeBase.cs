using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public abstract class PipeTypeBase : IPipeType
    {
        protected DataTable _data;
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
        public virtual UtilsCommon.Utils.PipeSeriesEnum GetPipeSeries(
            int dn, UtilsCommon.Utils.PipeTypeEnum type, double realKod)
        {
            if (type == UtilsCommon.Utils.PipeTypeEnum.Retur ||
                type == UtilsCommon.Utils.PipeTypeEnum.Frem)
                type = UtilsCommon.Utils.PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}'");

            foreach (DataRow row in results)
            {
                double kOd = (double)row["kOd"];
                if (kOd.Equalz(realKod, 0.001))
                {
                    string sS = (string)row["PipeSeries"];
                    if (Enum.TryParse(sS, true, out UtilsCommon.Utils.PipeSeriesEnum series)) return series;
                    return UtilsCommon.Utils.PipeSeriesEnum.Undefined;
                }
            }
            return UtilsCommon.Utils.PipeSeriesEnum.Undefined;
        }
        public virtual double GetPipeKOd(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series)
        {
            if (type == UtilsCommon.Utils.PipeTypeEnum.Retur ||
                type == UtilsCommon.Utils.PipeTypeEnum.Frem)
                type = UtilsCommon.Utils.PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["kOd"];
            return 0;
        }
        public virtual double GetMinElasticRadius(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series)
        {
            if (type == UtilsCommon.Utils.PipeTypeEnum.Retur ||
                type == UtilsCommon.Utils.PipeTypeEnum.Frem)
                type = UtilsCommon.Utils.PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["minElasticRadii"];
            return 0;
        }
        public abstract double GetBuerorMinRadius(int dn, int std);
        public virtual IEnumerable<int> ListAllDnsForPipeTypeSerie(UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series)
        {
            if (type == UtilsCommon.Utils.PipeTypeEnum.Retur ||
                type == UtilsCommon.Utils.PipeTypeEnum.Frem)
                type = UtilsCommon.Utils.PipeTypeEnum.Enkelt;
            DataRow[] results = _data.Select($"PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return results.Select(x => (int)x["DN"]);
            return null;
        }
        public abstract string GetLabel(int DN, UtilsCommon.Utils.PipeTypeEnum type, double od, double kOd);
        public virtual short GetLayerColor(UtilsCommon.Utils.PipeTypeEnum type)
        {
            switch (type)
            {
                case UtilsCommon.Utils.PipeTypeEnum.Ukendt:
                    return 0;
                case UtilsCommon.Utils.PipeTypeEnum.Twin:
                    return 6;
                case UtilsCommon.Utils.PipeTypeEnum.Frem:
                    return 1;
                case UtilsCommon.Utils.PipeTypeEnum.Retur:
                    return 5;
                case UtilsCommon.Utils.PipeTypeEnum.Enkelt:
                    return 0;
                default: return 0;
            }
        }
        public virtual double GetTrenchWidth(int dn, UtilsCommon.Utils.PipeTypeEnum type, UtilsCommon.Utils.PipeSeriesEnum series)
        {
            if (type == UtilsCommon.Utils.PipeTypeEnum.Retur || 
                type == UtilsCommon.Utils.PipeTypeEnum.Frem)
                type = UtilsCommon.Utils.PipeTypeEnum.Enkelt;

            DataRow[] results = _data.Select($"DN = {dn} AND PipeType = '{type}' AND PipeSeries = '{series}'");
            if (results != null && results.Length > 0) return (double)results[0]["tWdth"];
            return 1000000;
        }
    }
}