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
                    return new El30();
                default:
                    throw new Exception("Unknown symbol name (Block): " + blockName);
            }
        }
    }
}
