using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon
{
    public static class DataQa
    {
        public static class Gas
        {
            public static HashSet<string> ForbiddenValues()
            {
                HashSet<string> values = new HashSet<string>();

                var data = CsvReader.ReadCsvToDataTable(
                    @"X:\AutoCAD DRI - 01 Civil 3D\LER1.0\GasQaData\DataQa.Gas.ForbiddenValues.csv",
                    "ForbiddenValues");
                foreach (var value in data.AsEnumerable())
                {
                    values.Add(value[0].ToString());
                }

                return values;
            }

            public static Dictionary<string, string> ReplaceValues()
            {
                Dictionary<string, string> values = new Dictionary<string, string>();

                var data = CsvReader.ReadCsvToDataTable(
                    @"X:\AutoCAD DRI - 01 Civil 3D\LER1.0\GasQaData\DataQa.Gas.ReplaceValues.csv",
                    "ReplaceValues");
                foreach (var value in data.AsEnumerable())
                {
                    values.Add(value[0].ToString(), value[1].ToString());
                }

                return values;
            }
        }
        public static class Gis
        {
            public static bool ContainsForbiddenValues(string input)
            {
                foreach (string forbiddenValue in ForbiddenValues)
                {
                    if (input.Contains(forbiddenValue)) return true;
                }

                return false;
            }

            public static HashSet<string> ForbiddenValues = new HashSet<string>()
            {
                "GGF_ledninger",
                "REV",
                "0-HJÆLPELINJE"
            };
        }
        public static class Vand
        {
            #region Data
            public static Dictionary<string, double> imperialToDnDictDouble = new Dictionary<string, double>()
            {
                { "1/2\"", 15.0 },
                { "3/4\"", 20.0 },
                { "1\"", 25.0 },
                { "1 1/4\"", 32.0 },
                { "1 1/2\"", 40.0 },
                { "2\"", 50.0 },
                { "2 1/2\"", 65.0 },
                { "3\"", 80.0 },
                { "4\"", 100.0 },
                { "5\"", 125.0 },
                { "6\"", 150.0 },
                { "8\"", 200.0 },
                { "10\"", 250.0 },
                { "12\"", 300.0 },
                { "14\"", 350.0 },
                { "16\"", 400.0 },
                { "18\"", 450.0 },
                { "20\"", 500.0 },
                { "24\"", 600.0 },
                { "28\"", 700.0 },
                { "32\"", 800.0 },
            };
            public static Dictionary<string, string> imperialToDnDictString = new Dictionary<string, string>()
            {
                { "1/2\"", "15" },
                { "½\"", "15" },
                { "3/4\"", "20" },
                { "1\"", "25" },
                { "1 1/4\"", "32" },
                { "5/4\"", "32" },
                { "1 1/2\"", "40" },
                { "1½\"", "40" },
                { "2\"", "50" },
                { "2 1/2\"", "65" },
                { "11/4\"", "70" },
                { "3\"", "80" },
                { "4\"", "100" },
                { "5\"", "125" },
                { "6\"", "150" },
                { "8\"", "200" },
                { "10\"", "250" },
                { "12\"", "300" },
                { "14\"", "350" },
                { "16\"", "400" },
                { "18\"", "450" },
                { "20\"", "500" },
                { "24\"", "600" },
                { "28\"", "700" },
                { "32\"", "800" },
            };
            public static Dictionary<string, string> replaceDict = new Dictionary<string, string>()
            {
                { "Ø22", "22" },
                { "30-31 mm", "30" },
                { "32 m/m", "32" },
                { "32 mm", "32" },
                { "32mm", "32" },
                { "32/40", "40" },
                { "Ø40", "40" },
                { "40 mm", "40" },
                { "40 SLA", "40" },
                { "50 / 40", "50" },
                { "50+40", "50" },
                { "50-40", "50" },
                { "63 m/m", "63" },
                { "63 mm", "63" },
                { "63 SLA", "63" },
                { "69 mm", "69" },
                { "69mm", "69" },
                { "70 mm", "70" },
                { "70,2 mm", "70" },
                { "75 mm", "75" },
                { "sløjfet 90", "90" },
                { "90 mm", "90" },
                { "90 SLA", "90" },
                { "1\" jernrør", "1\"" },
                { "Ukendt", "0" },
            };
            #endregion
        }
    }
}
