using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static void LoadPipeTypeData(string pathToPipeTypesStore)
        {
            var csvs = System.IO.Directory.EnumerateFiles(
                pathToPipeTypesStore, "*.csv", System.IO.SearchOption.TopDirectoryOnly);

            _repository = new PipeTypeRepository();
            _repository.Initialize(new PipeTypeDataLoaderCSV().Load(csvs));
        }

        public static void ListAllPipeTypes() => prdDbg(string.Join("\n", _repository.ListAllPipeTypes()));

        #region Pipe schedule methods

        #endregion
    }
}
