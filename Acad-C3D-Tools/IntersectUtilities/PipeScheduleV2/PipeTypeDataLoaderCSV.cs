using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeDataLoaderCSV
    {
        private static Dictionary<string, Type> typeDict = new Dictionary<string, Type>()
        {
            { "DN", typeof(PipeTypeDN) },
            { "ALUPEX", typeof(PipeTypeCommon) },
            { "CU", typeof(PipeTypeCommon) },
        };

        public Dictionary<string, IPipeType> Load(IEnumerable<string> paths)
        {
            Dictionary<string, IPipeType> dict = new Dictionary<string, IPipeType>();
            foreach (var path in paths)
            {
                string type = System.IO.Path.GetFileNameWithoutExtension(path);
                DataTable dataTable = CsvReader.ReadCsvToDataTable(path, type);
                IPipeType pipeType = Activator.CreateInstance(typeDict[type]) as IPipeType;
                pipeType.Initialize(dataTable);
                dict.Add(type, pipeType);
            }

            return dict;
        }
    }
}
