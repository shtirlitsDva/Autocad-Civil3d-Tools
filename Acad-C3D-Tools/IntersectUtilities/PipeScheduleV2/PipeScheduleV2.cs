using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeScheduleV2 : IPipeScheduleV2
    {
        private IPipeTypeRepository _repository;

        public PipeScheduleV2(string pathToPipeTypesStore)
        {
            var csvs = System.IO.Directory.EnumerateFiles(
                pathToPipeTypesStore, "*.csv", System.IO.SearchOption.TopDirectoryOnly);

            _repository = new PipeTypeRepository();
            _repository.Initialize(PipeTypeDataLoaderCSV.Load(csvs));
        }

        public void ListAllPipeTypes() => prdDbg(string.Join("\n", _repository.ListAllPipeTypes()));
    }
}
