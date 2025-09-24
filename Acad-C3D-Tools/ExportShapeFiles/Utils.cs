using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using IntersectUtilities;

namespace ExportShapeFilesEasyGis
{
    public static class Utils
    {
        private static System.Data.DataTable fjvBlocksDt = null;
        public static System.Data.DataTable GetFjvBlocksDt()
        {
            if (fjvBlocksDt == null)
            {
                if (!File.Exists(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv"))
                    throw new System.Exception(
                        "FJV Dynamiske Komponenter.csv is not available at standard location!");

                fjvBlocksDt = CsvReader.ReadCsvToDataTable(
                    @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
            }
            return fjvBlocksDt;
        }
    }
}
