using Autodesk.AutoCAD.Geometry;

using NTRExport.SoilModel;

using System.Text;

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
            var profile = soil ?? SoilProfile.Default;
            var parts = new StringBuilder();
            parts.Append($" SOIL_H={profile.CoverHeight:0.###}");
            if (profile.GroundWaterDistance.HasValue)
                parts.Append($" SOIL_HW={profile.GroundWaterDistance.Value:0.###}");
            if (profile.SoilWeightAbove.HasValue)
                parts.Append($" SOIL_GS={profile.SoilWeightAbove.Value:0.###}");
            if (profile.SoilWeightBelow.HasValue)
                parts.Append($" SOIL_GSW={profile.SoilWeightBelow.Value:0.###}");
            if (profile.FrictionAngleDeg.HasValue)
                parts.Append($" SOIL_PHI={profile.FrictionAngleDeg.Value:0.###}");
            if (profile.CushionType.HasValue)
                parts.Append($" SOIL_CUSH_TYPE={profile.CushionType.Value}");
            if (profile.CushionThk > 0)
                parts.Append($" SOIL_CUSH_THK={profile.CushionThk:0.###}");
            return parts.ToString();
        }
    }
}
