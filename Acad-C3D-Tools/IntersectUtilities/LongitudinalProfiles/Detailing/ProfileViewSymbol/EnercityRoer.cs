using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon; // <-- ensure this using is present

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal class EnercityRoer : BlockBase
    {
        // Diameter in metres (adjust to your actual pipe diameter)
        private double dia = 0.11;

        public EnercityRoer()
            : base("Enercity Roer") { }

        internal override void HandleBlockDefinition(Database localDb)
        {
            // Requires an active transaction (same pattern as other symbol classes)
            localDb.CheckOrImportBlockRecord(
                @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg",
                _blockName
            );
        }

        // Use circular top logic from BlockBase by providing diameter
        protected override double getDia() => dia;
    }
}