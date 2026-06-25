using System;

namespace NorsynDistrictZones;

/// <summary>
/// One-line human description of an NDZ command, shown in the load banner.
/// Decorate a <c>[CommandMethod]</c> method with this; <see cref="CommandBanner"/>
/// discovers commands by reflection and reads this for the text — there is no central
/// command list to keep in sync. Commands without it still appear (by name only).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandSummaryAttribute : Attribute
{
    public string Summary { get; }
    public CommandSummaryAttribute(string summary) => Summary = summary;
}
