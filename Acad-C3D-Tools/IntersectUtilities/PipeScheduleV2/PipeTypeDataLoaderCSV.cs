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
        public Dictionary<string, IPipeType> Load(IEnumerable<string> paths)
        {
            Dictionary<string, IPipeType> dict = new Dictionary<string, IPipeType>();
            foreach (var path in paths)
            {
                string type = System.IO.Path.GetFileNameWithoutExtension(path);
                DataTable dataTable = CsvReader.ReadCsvToDataTable(path, type);
                IPipeType pipeType = Activator.CreateInstance(
                    PipeScheduleV2.typeDict[type]) as IPipeType;
                pipeType.Initialize(dataTable);
                dict.Add(type, pipeType);
            }

            return dict;
        }
    }
}
