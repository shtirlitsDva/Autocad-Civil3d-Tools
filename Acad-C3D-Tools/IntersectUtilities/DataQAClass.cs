using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon
{
    public static class DataQa
    {
        public static class Gas
        {
            public static HashSet<string> ForbiddenValues = new HashSet<string>()
            {
                "ALLE STIK ER",
                "IKKE FJERNES",
                "UDLUFTNING MÅ",
                "1843B TYPE D",
                "ALLE STIK = Ø40 PM",
                "ANV. SOM TRÆKRØR",
                "ANVENDT SOM TRÆKRØR",
                "ARMA-FLEX",
                "ARMAFLEX",
                "B-RØR",
                "BELIGGENHED USIKKER",
                "BELIG. USIKKER",
                "EXISTENS USIKKER",
                "FLEXOPFØRINGSRØR",
                "FRIT R-SKAB",
                "FRITST.M/R SKAB",
                "G10",
                "I GAMMELT STIK",
                "M/R G40/65",
                "M/R SKAB TYPE G25",
                "M/R SKAB",
                "MÅSKE RELINET",
                "PC-COATET",
                "R-SKAB",
                "REG.1843B",
                "SKAB RS 830",
                "SÆNKET 40 CM",
                "SÆNKET",
                "T=0.0",
                "T=0.1",
                "T=0.2",
                "T=0.3",
                "T=0.4",
                "T=0.7",
                "TERRASSE",
                "TRAPPE",
                "TYPE G 16/25",
                "TYPE G25",
                "TYPE G65",
                "VENTILSKAB",
                "VINDUE",
                "M/R G100",
                "SKAB RS 1000"
            };
            public static Dictionary<string, string> ReplaceLabelParts = new Dictionary<string, string>()
            {
                { "B-RØR 63 PM", "63 PM" },
                { "40 PC 026", "40 PC" },
                { "40 PM 026", "40 PM" },
                { "40 PM 20 MBAR", "40 PM" },
                { "63PM", "63 PM" },
                { "63 PM 026", "63 PM" },
                { "63 PM 50 MB", "63 PM" },
                { "90 PM 50 MBAR", "90 PM" },
                { "90 PM 0.1", "90 PM" },
                { "75 ST/63 PM 026", "75 ST" },
                { "ALLE STIK = 20 PM", "20 PM" },
                { "ALLE STIK 20 PM", "20 PM" },
                { "ALLAE STIK 20 PM", "20 PM" },
                { "ALLE STIK ER 20 PM 4.0", "20 PM" }
                { "ALLE STIK ER 20 PM 4.0", "20 PM" },
                { "LIGGER UNDER 63 PM", "63 PM" }
            };
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
