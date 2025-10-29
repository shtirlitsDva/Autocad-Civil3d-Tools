using System.Collections.Generic;

namespace NTRExport.SoilModel
{
    internal class SoilProfile
    {
        public static readonly SoilProfile Default = new("Soil_Default", 0.00);
        public string Name { get; }
        public double CushionThk { get; }

        public SoilProfile(string name, double cushionThk)
        {
            Name = name;
            CushionThk = cushionThk;
        }
    }

    internal interface INtrSoilAdapter
    {
        IEnumerable<string> Define(SoilProfile profile);
        string? RefToken(SoilProfile profile);
    }
}
