using System;
using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>What a legacy component block is to a connection.</summary>
internal enum LegacyPartRole
{
    /// <summary>Nothing a connection is made of (a pipe part, a valve, a reducer).</summary>
    None,
    /// <summary>
    /// A tee on the main: its BelongsToAlignment is the MAIN and its
    /// BranchesOffToAlignment the BRANCH.
    /// </summary>
    Tee,
    /// <summary>
    /// A stud or a svanehals welded onto the main: the other way round - its
    /// BranchesOffToAlignment is the MAIN and its BelongsToAlignment the BRANCH.
    /// </summary>
    Stud,
    /// <summary>A service connection (stik): out of scope, reported as not connected.</summary>
    ServiceConnection,
    /// <summary>A materialeskift: a branch part only where it sits on another pipeline's run.</summary>
    Materialeskift,
}

/// <summary>
/// The legacy component types (the FJV Dynamiske Komponenter.csv Type column)
/// by the role they play in a connection. Read from the unparsed Type, so the
/// materialeskift's templated type is recognised by its prefix.
/// </summary>
internal static class LegacyPartRoles
{
    private static readonly Dictionary<string, LegacyPartRole> ByType = new(StringComparer.Ordinal)
    {
        ["Afgrening med spring"] = LegacyPartRole.Tee,
        ["Afgrening, parallel"] = LegacyPartRole.Tee,
        ["Parallelafgrening"] = LegacyPartRole.Tee,
        ["Lige afgrening"] = LegacyPartRole.Tee,
        ["Svejsetee"] = LegacyPartRole.Tee,
        ["Preskobling tee"] = LegacyPartRole.Tee,
        ["Afgreningsstuds"] = LegacyPartRole.Stud,
        ["Svanehals"] = LegacyPartRole.Stud,
        ["Stikafgrening"] = LegacyPartRole.ServiceConnection,
        //STIKTEE's type; the only Muffetee in the component table.
        ["Muffetee"] = LegacyPartRole.ServiceConnection,
    };

    private const string MaterialeskiftPrefix = "Materialeskift";

    public static LegacyPartRole Of(string unparsedType)
    {
        if (ByType.TryGetValue(unparsedType, out LegacyPartRole role)) return role;
        return unparsedType.StartsWith(MaterialeskiftPrefix, StringComparison.Ordinal)
            ? LegacyPartRole.Materialeskift
            : LegacyPartRole.None;
    }
}
