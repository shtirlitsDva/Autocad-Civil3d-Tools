using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using CSharpFunctionalExtensions;

using IntersectUtilities.LerHatchLayers;

using LERImporter.PropertySets;

using Teigha.DatabaseServices;

// CSharpFunctionalExtensions has an Entity too (a DDD base class).
using Entity = Teigha.DatabaseServices.Entity;

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
    public static UnitResult<AecError> Define(Database db, LerSetDef def)
    {
        Transaction tx = db.TransactionManager.TopTransaction;
        return AecObjects.EnsureClasses()
            .Bind(() => AecObjects.Open<DBDictionary>(tx, db.NamedObjectsDictionaryId, OpenMode.ForRead, "the named objects dictionary"))
            .Bind(nod => AecObjects.Subdictionary(tx, nod, AecObjects.DefinitionDictionary, hard: false).Bind(defs =>
                defs.Contains(def.Name)
                    ? AecObjects.Host($"find {def.Name}", () => defs.GetAt(def.Name))
                        .Bind(defId => Known(db, tx, def.Name, defId))
                        .Bind(_ => UnitResult.Success<AecError>())
                    : Format(tx, nod).Bind(formatId => AecObjects.Create(AecObjects.DefinitionClass).Bind(definition =>
                        AecObjects.Host($"define {def.Name}", () =>
                            {
                                if (!defs.IsWriteEnabled) defs.UpgradeOpen();
                                defs.SetAt(def.Name, definition);
                                tx.AddNewlyCreatedDBObject(definition, true);
                            })
                            .Bind(() => AecObjects.Fill(definition,
                                AecStream.Definition(def.Description, def.Properties, LerSetDef.AppliesTo, formatSlot: 0),
                                new[] { formatId }))
                            .Bind(() =>
                            {
                                DefinitionsOf(db)[def.Name] = (definition.ObjectId, def);
                                return UnitResult.Success<AecError>();
                            })))));
    }

    /// <summary>
    /// Attaches the set of a defined definition to the entity, with the GML values, the
    /// graveforespørgsel's bemærkning and its LER number. A value for a property the
    /// definition does not have is reported and skipped, as on Civil.
    /// </summary>
    public static UnitResult<AecError> Attach(
        Database db, Entity ent, string setName, IReadOnlyDictionary<string, object> psData,
        string? bemaerkning, string? lerNummer)
    {
        Transaction tx = db.TransactionManager.TopTransaction;
        return DefinitionsOf(db).TryFind(setName)
            .ToResult(AecError.Drawing($"No property set named {setName} was found!"))
            .Bind(known =>
            {
                LerSetDef def = known.Def;
                string entityClass = ent.GetRXClass().Name;
                if (!LerSetDef.AppliesTo.Contains(entityClass))
                    return UnitResult.Failure(AecError.Drawing($"Property set {setName} does not apply to a {entityClass}."));

                // The GML reader hands null for a graveforespørgsel without them.
                var given = new Dictionary<string, object>(psData)
                {
                    ["GmlBemærkning"] = bemaerkning ?? "",
                    ["LerNummer"] = lerNummer ?? "",
                };
                AecValue[] values = def.Properties.Select(p => AecValue.Default(p.Type)).ToArray();
                return given.Each(pair => def.IdOf(pair.Key).Match(
                        i => AecValue.From(def.Properties[i].Type, pair.Value, pair.Key).Bind(v =>
                        {
                            values[i] = v;
                            return UnitResult.Success<AecError>();
                        }),
                        () =>
                        {
                            prdDbg($"For propertyset {setName} property {pair.Key} not found!");
                            return UnitResult.Success<AecError>();
                        }))
                    .Bind(() => AecObjects.Host($"make the extension dictionary of {ent.Handle}", () =>
                    {
                        if (!ent.IsWriteEnabled) ent.UpgradeOpen();
                        if (ent.ExtensionDictionary.IsNull) ent.CreateExtensionDictionary();
                        return ent.ExtensionDictionary;
                    }))
                    .Bind(xdictId => AecObjects.Open<DBDictionary>(tx, xdictId, OpenMode.ForWrite,
                        $"the extension dictionary of {ent.Handle}"))
                    .Bind(xdict => AecObjects.Host($"harden the extension dictionary of {ent.Handle}",
                            () => { xdict.TreatElementsAsHard = true; })
                        .Bind(() => AecObjects.Subdictionary(tx, xdict, AecObjects.SetDictionary, hard: true)))
                    .Bind(sets => AecObjects.Create(AecObjects.SetClass).Bind(set =>
                        AecObjects.Host($"attach {setName} to {ent.Handle}", () =>
                            {
                                if (!sets.IsWriteEnabled) sets.UpgradeOpen();
                                sets.SetAt("*A", set);
                                tx.AddNewlyCreatedDBObject(set, true);
                            })
                            .Bind(() => AecObjects.Fill(set, AecStream.Set(entitySlot: 0, definitionSlot: 1, values),
                                new[] { ent.ObjectId, known.Id }))));
            });
    }

    /// <summary>
    /// The LER sets on an entity with the values LERHATCHLAYERS reads, decoded from the
    /// stored stream (the counterpart of LerHatchLayerService.ReadSets on Civil).
    /// </summary>
    public static LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>> ReadHatchLayerSets(Entity entity, Transaction tx) =>
        new AecSets(tx).SetsOf(entity).Traverse(read => read.Map(set => HatchLayerSet(set))).Match(
            sets => LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Success(sets),
            error => LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Failure("Cannot read property sets: " + error.Message));

    // ------------------------------------------------------------------ internals

    private static readonly string[] HatchLayerFields =
        { "Driftsstatus", "SpædningsNiveau", "SpændingsNiveau", "Forsyningsart", "LedningsEjersNavn" };

    private static LerHatchLayerSet HatchLayerSet(AecAttachedSet set)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (AecItem item in set.Set.Items)
        {
            Maybe<string> property = set.Definition.ById(item.Id)
                .Bind(p => HatchLayerFields.Contains(p.Name) ? Maybe.From(p.Name) : Maybe<string>.None);
            Maybe<string> value = item.Match(
                stored => Maybe.From(stored.Value.Match(
                    i => i.ToString(CultureInfo.InvariantCulture),
                    d => d.ToString(CultureInfo.InvariantCulture),
                    s => s,
                    b => b.ToString(CultureInfo.InvariantCulture))),
                empty => Maybe<string>.None,
                unreadable => Maybe<string>.None);
            property.Bind(name => value.Map(v => (Name: name, Value: v)))
                .Match(pair => { values[pair.Name] = pair.Value; }, () => { });
        }
        return new LerHatchLayerSet(set.Name, values);
    }

    /// <summary>
    /// A definition already in the drawing, decoded once. LERImporter writes into it, so it
    /// must be one LERImporter could have made: ids 0..n-1 in order, every property manual.
    /// </summary>
    private static Result<(ObjectId Id, LerSetDef Def), AecError> Known(Database db, Transaction tx, string name, ObjectId defId) =>
        DefinitionsOf(db).TryFind(name).Match(
            known => Result.Success<(ObjectId Id, LerSetDef Def), AecError>(known),
            () => new AecSets(tx).Definition(defId).Bind(decoded =>
                decoded.Definition.Properties.Select((p, i) => (Property: p, Index: i))
                    .Traverse(x => x.Property.Id != x.Index
                        ? Result.Failure<AecPropertyDef, AecError>(AecError.Drawing(
                            $"Property set {name}: property {x.Property.Name} has id {x.Property.Id}, expected {x.Index}"))
                        : x.Property.Type
                            .Map(type => new AecPropertyDef(x.Property.Name, x.Property.Description, type))
                            .MapError(error => error.In($"Property set {name}: property {x.Property.Name}")))
                    .Map(properties =>
                    {
                        (ObjectId Id, LerSetDef Def) entry = (defId, new LerSetDef(name, decoded.Definition.Description, properties));
                        DefinitionsOf(db)[name] = entry;
                        return entry;
                    })));

    private static Result<ObjectId, AecError> Format(Transaction tx, DBDictionary nod) =>
        AecObjects.Subdictionary(tx, nod, AecObjects.FormatDictionary, hard: false).Bind(formats =>
            formats.Contains(AecObjects.FormatName)
                ? AecObjects.Host($"find the {AecObjects.FormatName} format", () => formats.GetAt(AecObjects.FormatName))
                : AecObjects.Create(AecObjects.FormatClass).Bind(format =>
                    AecObjects.Host($"add the {AecObjects.FormatName} format", () =>
                        {
                            if (!formats.IsWriteEnabled) formats.UpgradeOpen();
                            formats.SetAt(AecObjects.FormatName, format);
                            tx.AddNewlyCreatedDBObject(format, true);
                        })
                        .Bind(() => AecObjects.Fill(format, AecStream.FormatStandard(), Array.Empty<ObjectId>()))
                        .Map(() => format.ObjectId)));
}
