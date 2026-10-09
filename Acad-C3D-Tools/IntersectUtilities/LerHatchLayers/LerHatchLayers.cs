using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntersectUtilities.LerHatchLayers;

internal abstract record LerHatchLayerResult<T>
{
    private LerHatchLayerResult() { }
    public abstract TOut Match<TOut>(Func<T, TOut> mapped, Func<string, TOut> skipped);
    public static LerHatchLayerResult<T> Success(T value) => new Mapped(value);
    public static LerHatchLayerResult<T> Failure(string reason) => new Skipped(reason);

    private sealed record Mapped(T Value) : LerHatchLayerResult<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> mapped, Func<string, TOut> skipped) => mapped(Value);
    }

    private sealed record Skipped(string Reason) : LerHatchLayerResult<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> mapped, Func<string, TOut> skipped) => skipped(Reason);
    }
}

internal sealed record LerHatchLayerSet(string Name, IReadOnlyDictionary<string, string> Values);
internal sealed record LerHatchLayerLine(string SetName, string LayerName, string Owner);
internal sealed record LerHatchLayerAssignment(ObjectId Id, string Handle, string LayerName);
internal sealed record LerHatchLayerSkip(string Handle, string Reason);
internal sealed record LerHatchLayerPlan(
    IReadOnlyList<LerHatchLayerAssignment> Assignments,
    IReadOnlyList<LerHatchLayerSkip> Skipped);

internal static class LerHatchLayerMapper
{
    internal static readonly IReadOnlyDictionary<string, string> ComponentLineSets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Elkomponent"] = "Elledning",
            ["Vandkomponent"] = "Vandledning",
            ["Afloebskomponent"] = "Afloebsledning",
            ["Oliekomponent"] = "Olieledning",
            ["Gaskomponent"] = "Gasledning",
            ["TermiskKomponent"] = "Termiskledning",
            ["Telekommunikationskomponent"] = "Telekommunikationsledning",
            ["AndenKomponent"] = "AndenLedning"
        };

    internal static LerHatchLayerResult<string> Resolve(
        IReadOnlyList<LerHatchLayerSet> sets,
        IReadOnlyCollection<string> layers,
        IReadOnlyList<LerHatchLayerLine> lines)
    {
        LerHatchLayerSet[] components = sets.Where(set => ComponentLineSets.ContainsKey(set.Name)).ToArray();
        if (components.Length != 1)
            return LerHatchLayerResult<string>.Failure(components.Length == 0
                ? "No recognized LER component property set."
                : "Multiple LER component property sets: " + string.Join(", ", components.Select(set => set.Name)));

        LerHatchLayerSet component = components[0];
        if (!component.Values.TryGetValue("Driftsstatus", out string? status))
            return LerHatchLayerResult<string>.Failure(component.Name + ": missing Driftsstatus.");

        return StatusSuffix(status).Match(
            suffix => ResolveComponent(component, suffix, layers, lines),
            LerHatchLayerResult<string>.Failure);
    }

    private static LerHatchLayerResult<string> StatusSuffix(string status)
    {
        string normalized = Regex.Replace(status.Trim(), @"\s+", " ").ToLowerInvariant();
        if (normalized == "i drift" || normalized == "under etablering")
            return LerHatchLayerResult<string>.Success("");
        if (normalized == "permanent ude af drift")
            return LerHatchLayerResult<string>.Success("_UAD");
        return LerHatchLayerResult<string>.Failure("Unknown Driftsstatus: '" + status + "'.");
    }

    private static LerHatchLayerResult<string> ResolveComponent(
        LerHatchLayerSet component, string suffix,
        IReadOnlyCollection<string> layers, IReadOnlyList<LerHatchLayerLine> lines)
    {
        if (component.Name.Equals("Elkomponent", StringComparison.OrdinalIgnoreCase))
            return ElectricalLayer(component.Values).Match(
                layer => ExistingLayer(layer + suffix, layers),
                LerHatchLayerResult<string>.Failure);
        if (component.Name.Equals("Vandkomponent", StringComparison.OrdinalIgnoreCase))
            return ExistingLayer("Vandledning_L2" + suffix, layers);
        if (component.Name.Equals("Afloebskomponent", StringComparison.OrdinalIgnoreCase))
            return ExistingLayer("Afløbsledning" + suffix, layers);
        if (component.Name.Equals("Oliekomponent", StringComparison.OrdinalIgnoreCase))
            return ExistingLayer("Olieledning" + suffix, layers);
        if (component.Name.Equals("AndenKomponent", StringComparison.OrdinalIgnoreCase))
        {
            if (!component.Values.TryGetValue("Forsyningsart", out string? supply) || string.IsNullOrWhiteSpace(supply))
                return LerHatchLayerResult<string>.Failure("AndenKomponent: missing Forsyningsart.");
            return ExistingLayer("AndenLedning-" + supply.Trim() + suffix, layers);
        }

        // These component sets do not contain the cable/pipe subtype used by the importer.
        // Only reuse a layer when polylines of the same family and owner establish one answer.
        string lineSet = ComponentLineSets[component.Name];
        string owner = component.Values.TryGetValue("LedningsEjersNavn", out string? ownerValue) ? ownerValue.Trim() : "";
        bool hasOwner = owner.Length > 0;
        string[] candidates = lines
            .Where(line => line.SetName.Equals(lineSet, StringComparison.OrdinalIgnoreCase))
            .Where(line => !hasOwner || line.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase))
            .Select(line => StripDimension(line.LayerName))
            .Where(layer => layer.EndsWith("_UAD", StringComparison.OrdinalIgnoreCase) == (suffix.Length > 0))
            .Where(layer => IsFamilyLayer(lineSet, layer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(layer => layer, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 1) return ExistingLayer(candidates[0], layers);
        return LerHatchLayerResult<string>.Failure(component.Name + (hasOwner ? " / " + owner : "")
            + (candidates.Length == 0 ? ": no matching LER polyline layer."
                : ": ambiguous polyline layers: " + string.Join(", ", candidates)));
    }

    private static bool IsFamilyLayer(string setName, string layer)
    {
        if (setName.Equals("Gasledning", StringComparison.OrdinalIgnoreCase))
            return layer.StartsWith("GAS-", StringComparison.OrdinalIgnoreCase);
        if (setName.Equals("Telekommunikationsledning", StringComparison.OrdinalIgnoreCase))
            return layer.StartsWith("TELE-", StringComparison.OrdinalIgnoreCase);
        return new[] { "Damp-", "Køl-", "Fjv-", "TermiskLedn-" }
            .Any(prefix => layer.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static LerHatchLayerResult<string> ExistingLayer(string target, IReadOnlyCollection<string> layers)
    {
        foreach (string name in new[] { target, target + "-2D" })
        {
            string[] matches = layers.Where(layer => layer.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1) return LerHatchLayerResult<string>.Success(matches[0]);
        }
        return LerHatchLayerResult<string>.Failure("Target layer does not exist: " + target + ".");
    }

    private static LerHatchLayerResult<string> ElectricalLayer(IReadOnlyDictionary<string, string> values)
    {
        // SpædningsNiveau is the historical Elkomponent importer spelling.
        // Elledning and newer property definitions use SpændingsNiveau.
        HashSet<string> voltages = new(StringComparer.OrdinalIgnoreCase);
        bool unknownVoltage = false;
        foreach (string field in new[] { "SpædningsNiveau", "SpændingsNiveau" })
        {
            if (!values.TryGetValue(field, out string? raw) || string.IsNullOrWhiteSpace(raw)) continue;
            Match match = Regex.Match(raw.Trim(), @"^(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>kV|V)$", RegexOptions.IgnoreCase);
            if (!match.Success || !decimal.TryParse(match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number) || number <= 0)
            {
                unknownVoltage = true;
                continue;
            }
            if (match.Groups["unit"].Value.Equals("V", StringComparison.OrdinalIgnoreCase)) number /= 1000;
            voltages.Add(number.ToString("0.############################", CultureInfo.InvariantCulture) + "kV");
        }
        if (voltages.Count > 1)
            return LerHatchLayerResult<string>.Failure("Elkomponent: conflicting voltage properties.");
        return LerHatchLayerResult<string>.Success(voltages.Count == 0 || unknownVoltage
            ? "EL-Elledning-Ukendt"
            : "EL-Forsyningskabel-" + voltages.Single());
    }

    internal static string StripDimension(string layer) =>
        layer.EndsWith("-2D", StringComparison.OrdinalIgnoreCase) || layer.EndsWith("-3D", StringComparison.OrdinalIgnoreCase)
            ? layer[..^3] : layer;
}

internal static class LerHatchLayerService
{
    internal static LerHatchLayerResult<LerHatchLayerPlan> BuildPlan(Database database, Transaction tx)
        => BuildPlan(database, tx, Array.Empty<string>(), _ => true);

    internal static LerHatchLayerResult<LerHatchLayerPlan> BuildPlan(
        Database database, Transaction tx, IReadOnlyCollection<string> availableLayerNames,
        Func<Hatch, bool> includeHatch)
    {
        LayerTable layerTable = (LayerTable)tx.GetObject(database.LayerTableId, OpenMode.ForRead);
        string[] layers = layerTable.Cast<ObjectId>()
            .Select(id => ((LayerTableRecord)tx.GetObject(id, OpenMode.ForRead)).Name)
            .Concat(availableLayerNames).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        List<LerHatchLayerLine> lines = new();
        List<Hatch> hatches = new();
        List<string> lineErrors = new();
        foreach (ObjectId id in modelSpace)
        {
            DBObject entity = tx.GetObject(id, OpenMode.ForRead);
            if (entity is Hatch hatch && hatch.Layer == "0" && includeHatch(hatch)) hatches.Add(hatch);
            if (entity is Polyline || entity is Polyline3d)
            {
                Entity line = (Entity)entity;
                ReadSets(line, tx).Match(
                    sets =>
                    {
                        foreach (LerHatchLayerSet set in sets)
                        {
                            if (LerHatchLayerMapper.ComponentLineSets.Values.Contains(set.Name, StringComparer.OrdinalIgnoreCase))
                                lines.Add(new(set.Name, line.Layer, set.Values.TryGetValue("LedningsEjersNavn", out string? owner)
                                    ? owner.Trim() : ""));
                        }
                        return true;
                    },
                    error => { lineErrors.Add(line.Handle + ": " + error); return false; });
            }
        }
        if (lineErrors.Count > 0)
            return LerHatchLayerResult<LerHatchLayerPlan>.Failure(
                "Cannot establish polyline mappings: " + string.Join("; ", lineErrors));

        List<LerHatchLayerAssignment> assignments = new();
        List<LerHatchLayerSkip> skipped = new();
        foreach (Hatch hatch in hatches)
        {
            LerHatchLayerResult<string> mapping = ReadSets(hatch, tx).Match(
                sets => LerHatchLayerMapper.Resolve(sets, layers, lines),
                LerHatchLayerResult<string>.Failure);
            mapping.Match(
                layer =>
                {
                    bool targetLocked = layerTable.Has(layer)
                        && ((LayerTableRecord)tx.GetObject(layerTable[layer], OpenMode.ForRead)).IsLocked;
                    if (targetLocked || ((LayerTableRecord)tx.GetObject(hatch.LayerId, OpenMode.ForRead)).IsLocked)
                        skipped.Add(new(hatch.Handle.ToString(), "Source or target layer is locked: " + layer));
                    else assignments.Add(new(hatch.ObjectId, hatch.Handle.ToString(), layer));
                    return true;
                },
                reason => { skipped.Add(new(hatch.Handle.ToString(), reason)); return false; });
        }
        return LerHatchLayerResult<LerHatchLayerPlan>.Success(new(assignments, skipped));
    }

    internal static void Apply(Transaction tx, LerHatchLayerPlan plan)
    {
        foreach (LerHatchLayerAssignment assignment in plan.Assignments)
        {
            Hatch hatch = (Hatch)tx.GetObject(assignment.Id, OpenMode.ForWrite);
            LayerTable layerTable = (LayerTable)tx.GetObject(hatch.Database.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(assignment.LayerName))
            {
                if (!layerTable.IsWriteEnabled) layerTable.UpgradeOpen();
                LayerTableRecord layer = new() { Name = assignment.LayerName };
                layerTable.Add(layer);
                tx.AddNewlyCreatedDBObject(layer, true);
            }
            hatch.Layer = assignment.LayerName;
            hatch.ColorIndex = 256;
        }
    }

    private static LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>> ReadSets(Entity entity, Transaction tx)
    {
        try
        {
            List<LerHatchLayerSet> sets = new();
            foreach (ObjectId id in PropertyDataServices.GetPropertySets(entity))
            {
                PropertySet set = (PropertySet)tx.GetObject(id, OpenMode.ForRead);
                string name = set.PropertySetDefinitionName;
                if (!LerHatchLayerMapper.ComponentLineSets.ContainsKey(name)
                    && !LerHatchLayerMapper.ComponentLineSets.Values.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                PropertySetDefinition definition = (PropertySetDefinition)tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead);
                Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyDefinition property in definition.Definitions)
                {
                    if (property.Name is not ("Driftsstatus" or "SpædningsNiveau" or "SpændingsNiveau"
                        or "Forsyningsart" or "LedningsEjersNavn")) continue;
                    values[property.Name] = Convert.ToString(set.GetAt(property.Id), CultureInfo.InvariantCulture) ?? "";
                }
                sets.Add(new(name, values));
            }
            return LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Success(sets);
        }
        catch (System.Exception ex)
        {
            return LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Failure("Cannot read property sets: " + ex.Message);
        }
    }
}
