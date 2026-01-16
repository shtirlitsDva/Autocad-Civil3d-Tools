using System;
using System.Collections.Generic;
using System.Data;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

namespace IntersectUtilities.LongitudinalProfiles.Relocability
{
    // ------------------------------------------------------------
    //                    LER  TYPE  SYSTEM
    // ------------------------------------------------------------
    public readonly record struct LerType(LerTypeEnum Type, Spatial Spatial)
    {
        public override string ToString() => $"{Type}_{Spatial}";

        public static readonly LerType Unknown = new(LerTypeEnum.Ukendt, Spatial.Unknown);
    }

    // ------------------------------------------------------------
    //                 R E S O L V E R   C O N T R A C T S
    // ------------------------------------------------------------
    public interface ILerTypeResolver
    {
        LerType Resolve(string name);
    }

    // ------------------------------------------------------------
    //               R E S O L V E R   S E R V I C E
    // ------------------------------------------------------------
    public sealed class LerTypeResolver : ILerTypeResolver
    {
        private readonly Dictionary<string, LerType> _nameToTypeMap;

        public LerTypeResolver(Dictionary<string, LerType> nameToTypeMap)
        {
            _nameToTypeMap =
                nameToTypeMap ?? throw new ArgumentNullException(nameof(nameToTypeMap));
        }

        public LerType Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return LerType.Unknown;

            return _nameToTypeMap.TryGetValue(name, out var lerType) ? lerType : LerType.Unknown;
        }
    }

    // ------------------------------------------------------------
    //           L E R   T Y P E   M A P P I N G   B U I L D E R
    // ------------------------------------------------------------
    public static partial class LerTypeBuilder
    {
        /// <summary>
        /// Builds name-to-type mapping from Krydsninger CSV data source.
        /// </summary>
        public static Dictionary<string, LerType> BuildNameToTypeMappingFromKrydsninger(
            Krydsninger krydsninger
        )
        {
            var mapping = new Dictionary<string, LerType>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in krydsninger.Rows)
            {
                var navn = Krydsninger.Col(row, Krydsninger.Columns.Navn);
                var distance = Krydsninger.Col(row, Krydsninger.Columns.Distance);
                var type = Krydsninger.Col(row, Krydsninger.Columns.Type);

                if (string.IsNullOrWhiteSpace(navn) || string.IsNullOrWhiteSpace(distance))
                    continue;

                // Special case: UAD (Ude Af Drift) trumps everything else
                if (navn.Contains("UAD", StringComparison.OrdinalIgnoreCase))
                {
                    var spatialType = string.Equals(type, "3D", StringComparison.OrdinalIgnoreCase)
                        ? Spatial.ThreeD
                        : Spatial.TwoD;
                    mapping[navn] = new LerType(LerTypeEnum.UAD, spatialType);
                    continue;
                }
                var lerType = MapDistanceAndTypeToLerType(distance, type ?? string.Empty);
                mapping[navn] = lerType;
            }

            return mapping;
        }

        /// <summary>
        /// Maps distance and type values to LerType
        /// </summary>
        private static LerType MapDistanceAndTypeToLerType(string distance, string type)
        {
            // Special case: IGNORE type gets Ignored category with Unknown spatial
            if (string.Equals(type, "IGNORE", StringComparison.OrdinalIgnoreCase))
            {
                return new LerType(LerTypeEnum.Ignored, Spatial.Unknown);
            }

            // Determine geometry type based on Type column
            var geometryType = string.Equals(type, "3D", StringComparison.OrdinalIgnoreCase)
                ? Spatial.ThreeD
                : Spatial.TwoD;

            var utilityCategory = distance.ToUpperInvariant() switch
            {
                "AFLØB" => LerTypeEnum.Afløb,
                "DAMP" => LerTypeEnum.Damp,
                "EL_04" => LerTypeEnum.EL_LS, // Low Supply
                "EL_10" => LerTypeEnum.EL_HS, // High Supply
                "EL_30" => LerTypeEnum.EL_HS, // High Supply
                "EL_50" => LerTypeEnum.EL_HS, // High Supply
                "EL_132" => LerTypeEnum.EL_HS, // High Supply
                "FJV" => LerTypeEnum.FJV,
                "GAS" => LerTypeEnum.Gas,
                "LUFT" => LerTypeEnum.Luft,
                "OIL" => LerTypeEnum.Oil,
                "VAND" => LerTypeEnum.Vand,
                _ => LerTypeEnum.Ukendt,
            };

            if (utilityCategory == LerTypeEnum.Ukendt)
                return LerType.Unknown;

            return new LerType(utilityCategory, geometryType);
        }
    }

    // ------------------------------------------------------------
    //               F A C T O R Y   A N D   B U I L D E R
    // ------------------------------------------------------------
    public static partial class LerTypeResolverFactory
    {
        /// <summary>
        /// Creates a resolver from the Krydsninger CSV data source.
        /// </summary>
        public static ILerTypeResolver Create(Krydsninger krydsninger)
        {
            var mapping = LerTypeBuilder.BuildNameToTypeMappingFromKrydsninger(krydsninger);
            return new LerTypeResolver(mapping);
        }

        /// <summary>
        /// Creates a resolver using the active Krydsninger configuration.
        /// </summary>
        public static ILerTypeResolver Create()
        {
            return Create(Csv.Krydsninger);
        }
    }
}
