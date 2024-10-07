using IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles
{
    internal static class ProfileViewSymbolFactory
    {
        internal static IProfileViewSymbol GetProfileViewSymbol(string blockName)
        {
            switch (blockName)
            {
                case "Cirkel, Bund":
                    return new CirkelBund();
                case "Cirkel, Top":
                    return new CirkelTop();
                case "EL 0.4kV":
                case "EL 04kV":
                    return new El04();
                case "EL 10kV":
                    return new El10();
                case "EL 30kV":
                case "EL 50kV":
                    return new El30();
                case "EL 132kV":
                    return new El132();
                case "EL LUFT":
                    return new ElLuft();
                default:
                    throw new Exception("Unknown symbol name (Block): " + blockName);
            }
        }
    }
}
