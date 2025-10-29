using Autodesk.AutoCAD.Geometry;

using NTRExport.SoilModel;

namespace NTRExport.Ntr
{
    internal static class NtrFormat
    {
        public const double MetersToMillimeters = 1000.0;
        public const double DefaultSoilCoverM = 0.6;      // SOIL_H
        public const double CushionThkM = 0.08;           // SOIL_CUSH_THK

        public static string Pt(Point3d p)
        {
            var x = (p.X - NtrCoord.OffsetX) * MetersToMillimeters;
            var y = (p.Y - NtrCoord.OffsetY) * MetersToMillimeters;
            var z = p.Z * MetersToMillimeters;
            return "'" + $"{x:0.#}, {y:0.#}, {z:0.#}" + "'";
        }

        public static string SoilTokens(SoilProfile? soil)
        {
            // Always include cover; add cushion tokens when present
            var baseTok = $" SOIL_H={DefaultSoilCoverM:0.###}";
            if (soil != null && soil.CushionThk > 0)
            {
                return baseTok + $" SOIL_CUSH_TYPE=2 SOIL_CUSH_THK={CushionThkM:0.###}";
            }

            return baseTok;
        }
    }
}
