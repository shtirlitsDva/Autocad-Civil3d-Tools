using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeDataLoaderCSV : IPipeDataLoader
    {
        private readonly IPipeRepository _pipeRepository;

        public PipeDataLoaderCSV(IPipeRepository pipeRepository)
        {
            _pipeRepository = pipeRepository;
        }
        public void Load(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                string type = System.IO.Path.GetFileNameWithoutExtension(path);
                DataTable dataTable = CsvReader.ReadCsvToDataTable(path, type);
                _pipeRepository.Initialize(type, dataTable);
            }
        }
    }
}
