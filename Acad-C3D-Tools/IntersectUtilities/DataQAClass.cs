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
                "SÆNKET",
                "ALLE STIK = Ø40 PM",
                "ANV. SOM TRÆKRØR",
                "ANVENDT SOM TRÆKRØR",
                "ARMA-FLEX",
                "BELIGGENHED USIKKER",
                "B-RØR",
                "VENTILSKAB",
                "VINDUE",
                "I GAMMELT STIK",
                "M/R G40/65",
                "MÅSKE RELINET",
                "SKAB RS 830",
                "FRITST.M/R SKAB",
                "G10",
                "M/R SKAB",
                "T=0.2",
                "T=0.3",
                "T=0.4",
                "T=0.7",
                "TERRASSE",
                "TYPE G65",
                "SÆNKET 40 CM",
                "FRIT R-SKAB",
                "M/R SKAB TYPE G25",
                "REG.1843B",
                "T=0.0",
                "T=0.1",
                "TYPE G 16/25"
            };
            public static Dictionary<string, string> ReplaceLabelParts = new Dictionary<string, string>()
            {
                { "B-RØR 63 PM", "63 PM" },
                { "40 PC 026", "40 PC" },
                { "40 PM 026", "40 PM" },
                { "40 PM 20 MBAR", "40 PM" },
                { "63 PM 026", "63 PM" },
                { "63 PM 50 MB", "63 PM" },
                { "90 PM 50 MBAR", "90 PM" },
                { "90 PM 0.1", "90 PM" },
                { "75 ST/63 PM 026", "75 ST" }
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
    }
}
