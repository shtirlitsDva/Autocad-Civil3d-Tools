using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using IntersectUtilities.LerHatchLayers;

using LERImporter.PropertySets;

using Teigha.DatabaseServices;

using static IntersectUtilities.UtilsCommon.Utils;

namespace LERImporter.Host.Brx;

/// <summary>
/// LER property sets on BricsCAD, through the shared AEC object layer (AecObjects: an
/// empty object by class, filled through DwgIn with the stream Civil 3D files). Only manual
/// properties of the four AEC data types. The drawing is laid out as Civil lays it out, so
/// Civil reads it with its own AEC API.
/// </summary>
internal static class LerPropertySets
{
    // The definitions this session wrote or read, per database by name. A definition is
    // decoded once per database; the table lets a database's entries go with it.
    private static readonly ConditionalWeakTable<Database, Dictionary<string, (ObjectId Id, LerSetDef Def)>> Definitions = new();

    private static Dictionary<string, (ObjectId Id, LerSetDef Def)> DefinitionsOf(Database db) =>
        Definitions.GetValue(db, _ => new Dictionary<string, (ObjectId, LerSetDef)>());

    /// <summary>Adds the definition to the database unless one of that name is there.</summary>
    public static Result<Unit> Define(Database db, LerSetDef def) =>
        AecObjects.EnsureClasses().Bind(_ => AecObjects.Boundary($"define {def.Name}", () =>
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary defs = AecObjects.Subdictionary(tx, nod, AecObjects.DefinitionDictionary, hard: false);
            if (defs.Contains(def.Name))
                return Known(db, tx, def.Name, defs.GetAt(def.Name)).Map(_ => Unit.Value);

            return Format(tx, nod).Bind(formatId =>
            {
                DBObject definition = AecObjects.Create(AecObjects.DefinitionClass);
                if (!defs.IsWriteEnabled) defs.UpgradeOpen();
                defs.SetAt(def.Name, definition);
                tx.AddNewlyCreatedDBObject(definition, true);
                IReadOnlyList<AecToken> body =
                    AecStream.Definition(def.Description, def.Properties, LerSetDef.AppliesTo, formatSlot: 0);
                return AecObjects.Fill(definition, body, new[] { formatId }).Map(_ =>
                {
                    DefinitionsOf(db)[def.Name] = (definition.ObjectId, def);
                    return Unit.Value;
                });
            });
        }));

    /// <summary>
    /// Attaches the set of a defined definition to the entity, with the GML values, the
    /// graveforespørgsel's bemærkning and its LER number. A value for a property the
    /// definition does not have is reported and skipped, as on Civil.
    /// </summary>
    public static Result<Unit> Attach(
        Database db, Entity ent, string setName, IReadOnlyDictionary<string, object> psData,
        string? bemaerkning, string? lerNummer) =>
        AecObjects.Boundary($"attach {setName} to {ent.Handle}", () =>
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            if (!DefinitionsOf(db).TryGetValue(setName, out var known))
                return Result<Unit>.Failure($"No property set named {setName} was found!");
            LerSetDef def = known.Def;
            string entityClass = ent.GetRXClass().Name;
            if (!LerSetDef.AppliesTo.Contains(entityClass))
                return Result<Unit>.Failure($"Property set {setName} does not apply to a {entityClass}.");

            var given = new Dictionary<string, object>(psData);
            given["GmlBemærkning"] = bemaerkning ?? "";
            given["LerNummer"] = lerNummer ?? "";
            AecValue[] values = def.Properties.Select(p => AecValue.Default(p.Type)).ToArray();
            foreach (var pair in given)
            {
                Option<int> id = def.IdOf(pair.Key);
                Result<Unit> stored = id.Match(
                    i => AecValue.From(def.Properties[i].Type, pair.Value, pair.Key).Map(v =>
                    {
                        values[i] = v;
                        return Unit.Value;
                    }),
                    () =>
                    {
                        prdDbg($"For propertyset {setName} property {pair.Key} not found!");
                        return Result<Unit>.Success(Unit.Value);
                    });
                if (stored.Match(_ => false, _ => true)) return stored;
            }

            if (!ent.IsWriteEnabled) ent.UpgradeOpen();
            if (ent.ExtensionDictionary.IsNull) ent.CreateExtensionDictionary();
            DBDictionary xdict = (DBDictionary)tx.GetObject(ent.ExtensionDictionary, OpenMode.ForWrite);
            xdict.TreatElementsAsHard = true;
            DBDictionary sets = AecObjects.Subdictionary(tx, xdict, AecObjects.SetDictionary, hard: true);

            DBObject set = AecObjects.Create(AecObjects.SetClass);
            if (!sets.IsWriteEnabled) sets.UpgradeOpen();
            sets.SetAt("*A", set);
            tx.AddNewlyCreatedDBObject(set, true);
            return AecObjects.Fill(set, AecStream.Set(entitySlot: 0, definitionSlot: 1, values),
                new[] { ent.ObjectId, known.Id });
        });

    /// <summary>
    /// The LER sets on an entity with the values LERHATCHLAYERS reads, decoded from the
    /// stored stream (the counterpart of LerHatchLayerService.ReadSets on Civil).
    /// </summary>
    public static LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>> ReadHatchLayerSets(Entity entity, Transaction tx)
    {
        var sets = new List<LerHatchLayerSet>();
        foreach (Result<AecAttachedSet> read in new AecSets(tx).SetsOf(entity))
        {
            string fault = read.Match(set => { sets.Add(HatchLayerSet(set)); return ""; }, message => message);
            if (fault.Length > 0)
                return LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Failure("Cannot read property sets: " + fault);
        }
        return LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Success(sets);
    }

    // ------------------------------------------------------------------ internals

    private static readonly string[] HatchLayerFields =
        { "Driftsstatus", "SpædningsNiveau", "SpændingsNiveau", "Forsyningsart", "LedningsEjersNavn" };

    private static LerHatchLayerSet HatchLayerSet(AecAttachedSet set)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (AecItem item in set.Set.Items)
        {
            Option<string> property = set.Definition.ById(item.Id).Match(
                p => HatchLayerFields.Contains(p.Name) ? Option<string>.Of(p.Name) : Option<string>.Nothing,
                () => Option<string>.Nothing);
            Option<string> value = item.Match(
                stored => Option<string>.Of(stored.Value.Match(
                    i => i.ToString(CultureInfo.InvariantCulture),
                    d => d.ToString(CultureInfo.InvariantCulture),
                    s => s,
                    b => b.ToString(CultureInfo.InvariantCulture))),
                empty => Option<string>.Nothing,
                unreadable => Option<string>.Nothing);
            property.Match(name => value.Match(v => { values[name] = v; return Unit.Value; }, () => Unit.Value),
                () => Unit.Value);
        }
        return new LerHatchLayerSet(set.Name, values);
    }

    /// <summary>
    /// A definition already in the drawing, decoded once. LERImporter writes into it, so it
    /// must be one LERImporter could have made: ids 0..n-1 in order, every property manual.
    /// </summary>
    private static Result<(ObjectId Id, LerSetDef Def)> Known(Database db, Transaction tx, string name, ObjectId defId)
    {
        if (DefinitionsOf(db).TryGetValue(name, out var known)) return Result<(ObjectId, LerSetDef)>.Success(known);
        return new AecSets(tx).Definition(defId).Bind(decoded =>
        {
            var properties = new List<AecPropertyDef>();
            for (int i = 0; i < decoded.Definition.Properties.Count; i++)
            {
                AecDefinedProperty p = decoded.Definition.Properties[i];
                if (p.Id != i)
                    return Result<(ObjectId, LerSetDef)>.Failure(
                        $"Property set {name}: property {p.Name} has id {p.Id}, expected {i}");
                string fault = p.Type.Match(
                    type => { properties.Add(new AecPropertyDef(p.Name, p.Description, type)); return ""; },
                    message => message);
                if (fault.Length > 0)
                    return Result<(ObjectId, LerSetDef)>.Failure($"Property set {name}: property {p.Name}: {fault}");
            }
            var entry = (defId, new LerSetDef(name, decoded.Definition.Description, properties));
            DefinitionsOf(db)[name] = entry;
            return Result<(ObjectId, LerSetDef)>.Success(entry);
        });
    }

    private static Result<ObjectId> Format(Transaction tx, DBDictionary nod)
    {
        DBDictionary formats = AecObjects.Subdictionary(tx, nod, AecObjects.FormatDictionary, hard: false);
        if (formats.Contains(AecObjects.FormatName)) return Result<ObjectId>.Success(formats.GetAt(AecObjects.FormatName));
        DBObject format = AecObjects.Create(AecObjects.FormatClass);
        if (!formats.IsWriteEnabled) formats.UpgradeOpen();
        formats.SetAt(AecObjects.FormatName, format);
        tx.AddNewlyCreatedDBObject(format, true);
        return AecObjects.Fill(format, AecStream.FormatStandard(), Array.Empty<ObjectId>()).Map(_ => format.ObjectId);
    }
}
