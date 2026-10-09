using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using IntersectUtilities.LerHatchLayers;

using LERImporter.PropertySets;

using Teigha.DatabaseServices;
using Teigha.Runtime;

using static IntersectUtilities.UtilsCommon.Utils;

namespace LERImporter.Host.Brx;

/// <summary>
/// LER property sets on BricsCAD. BricsCAD reads and writes AEC property sets (its AEC
/// classes are ODA's), but has no managed API to make them. This makes them the way a
/// drawing read does: an empty object by class, filled through DwgIn with the stream Civil
/// 3D files (AecStream). Only manual properties of the four LER data types.
/// The drawing is laid out as Civil lays it out, so Civil reads it with its own AEC API:
///   NOD / AEC_PROPERTY_FORMAT_DEFS / "Standard"  AecDbScheduleDataFormat
///   NOD / AEC_PROPERTY_SET_DEFS / set name        AecDbPropertySetDef
///   entity / extension dictionary / AEC_PROPERTY_SETS / *A1, *A2 ...  AecDbPropertySet
/// </summary>
internal static class LerPropertySets
{
    private const string FormatDictionary = "AEC_PROPERTY_FORMAT_DEFS";
    private const string DefinitionDictionary = "AEC_PROPERTY_SET_DEFS";
    private const string SetDictionary = "AEC_PROPERTY_SETS";
    private const string FormatName = "Standard";
    private const string FormatClass = "AecDbScheduleDataFormat";
    private const string DefinitionClass = "AecDbPropertySetDef";
    private const string SetClass = "AecDbPropertySet";

    // The definitions this session wrote or read, per database by name. A definition is
    // decoded once per database; the table lets a database's entries go with it.
    private static readonly ConditionalWeakTable<Database, Dictionary<string, (ObjectId Id, LerSetDef Def)>> Definitions = new();

    private static Dictionary<string, (ObjectId Id, LerSetDef Def)> DefinitionsOf(Database db) =>
        Definitions.GetValue(db, _ => new Dictionary<string, (ObjectId, LerSetDef)>());

    /// <summary>Adds the definition to the database unless one of that name is there.</summary>
    public static Result<Unit> Define(Database db, LerSetDef def) =>
        EnsureClasses().Bind(_ => Boundary($"define {def.Name}", () =>
        {
            Transaction tx = db.TransactionManager.TopTransaction;
            DBDictionary nod = (DBDictionary)tx.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
            DBDictionary defs = Subdictionary(tx, nod, DefinitionDictionary, hard: false);
            if (defs.Contains(def.Name))
                return Known(db, tx, def.Name, defs.GetAt(def.Name)).Map(_ => Unit.Value);

            return Format(tx, nod).Bind(formatId =>
            {
                DBObject definition = Create(DefinitionClass);
                if (!defs.IsWriteEnabled) defs.UpgradeOpen();
                defs.SetAt(def.Name, definition);
                tx.AddNewlyCreatedDBObject(definition, true);
                return Fill(definition, AecStream.Definition(def, formatSlot: 0), new[] { formatId })
                    .Map(_ =>
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
        Boundary($"attach {setName} to {ent.Handle}", () =>
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
            var values = def.Properties.Select(p => (p.Type, Value: LerValue.Default(p.Type))).ToArray();
            foreach (var pair in given)
            {
                Option<int> id = def.IdOf(pair.Key);
                Result<Unit> stored = id.Match(
                    i => LerValue.From(def.Properties[i].Type, pair.Value, pair.Key).Map(v =>
                    {
                        values[i] = (def.Properties[i].Type, v);
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
            DBDictionary sets = Subdictionary(tx, xdict, SetDictionary, hard: true);

            DBObject set = Create(SetClass);
            if (!sets.IsWriteEnabled) sets.UpgradeOpen();
            sets.SetAt("*A", set);
            tx.AddNewlyCreatedDBObject(set, true);
            return Fill(set, AecStream.Set(entitySlot: 0, definitionSlot: 1, values),
                new[] { ent.ObjectId, known.Id });
        });

    /// <summary>
    /// The LER sets on an entity with the values LERHATCHLAYERS reads, decoded from the
    /// stored stream (the counterpart of LerHatchLayerService.ReadSets on Civil).
    /// </summary>
    public static LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>> ReadHatchLayerSets(Entity entity, Transaction tx)
    {
        var sets = new List<LerHatchLayerSet>();
        string error = "";
        if (!entity.ExtensionDictionary.IsNull)
        {
            DBDictionary xdict = (DBDictionary)tx.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
            if (xdict.Contains(SetDictionary))
            {
                DBDictionary setDict = (DBDictionary)tx.GetObject(xdict.GetAt(SetDictionary), OpenMode.ForRead);
                foreach (DBDictionaryEntry entry in setDict)
                {
                    Result<LerHatchLayerSet> read = ReadSet(entity.Database, tx, entry.Value);
                    if (read.Match(set => { sets.Add(set); return false; }, message => { error = message; return true; }))
                        break;
                }
            }
        }
        return error.Length > 0
            ? LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Failure("Cannot read property sets: " + error)
            : LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>>.Success(sets);
    }

    // ------------------------------------------------------------------ internals

    private static readonly string[] HatchLayerFields =
        { "Driftsstatus", "SpædningsNiveau", "SpændingsNiveau", "Forsyningsart", "LedningsEjersNavn" };

    private static Result<LerHatchLayerSet> ReadSet(Database db, Transaction tx, ObjectId setId) =>
        Record(tx.GetObject(setId, OpenMode.ForRead)).Bind(recorded =>
            AecStream.ReadSet(recorded.Body).Bind(set =>
            {
                ObjectId defId = recorded.Ids[set.DefinitionSlot];
                return DefinitionName(tx, defId).Bind(name => Known(db, tx, name, defId).Map(known =>
                {
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (id, value) in set.Values)
                    {
                        if (id < 0 || id >= known.Def.Properties.Count) continue;
                        string property = known.Def.Properties[id].Name;
                        if (!HatchLayerFields.Contains(property)) continue;
                        values[property] = value.Match(
                            i => i.ToString(CultureInfo.InvariantCulture),
                            d => d.ToString(CultureInfo.InvariantCulture),
                            s => s,
                            b => b.ToString(CultureInfo.InvariantCulture));
                    }
                    return new LerHatchLayerSet(name, values);
                }));
            }));

    private static Result<string> DefinitionName(Transaction tx, ObjectId defId)
    {
        DBObject def = tx.GetObject(defId, OpenMode.ForRead);
        DBDictionary owner = (DBDictionary)tx.GetObject(def.OwnerId, OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in owner)
            if (entry.Value == defId) return Result<string>.Success(entry.Key);
        return Result<string>.Failure($"definition {defId.Handle} is not in its owner dictionary");
    }

    /// <summary>A definition already in the drawing, decoded once.</summary>
    private static Result<(ObjectId Id, LerSetDef Def)> Known(Database db, Transaction tx, string name, ObjectId defId)
    {
        if (DefinitionsOf(db).TryGetValue(name, out var known)) return Result<(ObjectId, LerSetDef)>.Success(known);
        return Record(tx.GetObject(defId, OpenMode.ForRead)).Bind(recorded =>
            AecStream.ReadDefinition(recorded.Body).Map(decoded =>
            {
                var entry = (defId, new LerSetDef(name, decoded.Description, decoded.Properties));
                DefinitionsOf(db)[name] = entry;
                return entry;
            }));
    }

    private static Result<ObjectId> Format(Transaction tx, DBDictionary nod)
    {
        DBDictionary formats = Subdictionary(tx, nod, FormatDictionary, hard: false);
        if (formats.Contains(FormatName)) return Result<ObjectId>.Success(formats.GetAt(FormatName));
        DBObject format = Create(FormatClass);
        if (!formats.IsWriteEnabled) formats.UpgradeOpen();
        formats.SetAt(FormatName, format);
        tx.AddNewlyCreatedDBObject(format, true);
        return Fill(format, AecStream.FormatStandard(), Array.Empty<ObjectId>()).Map(_ => format.ObjectId);
    }

    private static DBDictionary Subdictionary(Transaction tx, DBDictionary parent, string name, bool hard)
    {
        if (parent.Contains(name)) return (DBDictionary)tx.GetObject(parent.GetAt(name), OpenMode.ForRead);
        DBDictionary child = new DBDictionary();
        if (!parent.IsWriteEnabled) parent.UpgradeOpen();
        parent.SetAt(name, child);
        tx.AddNewlyCreatedDBObject(child, true);
        if (hard) child.TreatElementsAsHard = true;
        return child;
    }

    private static DBObject Create(string className) =>
        (DBObject)((RXClass)SystemObjects.ClassDictionary[className]).Create();

    /// <summary>
    /// Hands a new, database-resident object its stream: its own owner and reactors, then
    /// the body. Then reads it back, and the object must file exactly what it was given.
    /// </summary>
    private static Result<Unit> Fill(DBObject obj, IReadOnlyList<AecToken> body, IReadOnlyList<ObjectId> bodyIds)
    {
        if (!obj.ExtensionDictionary.IsNull)
            return Result<Unit>.Failure($"{obj.GetRXClass().Name} has an extension dictionary; its header is not known");
        // MEASURED (BricsCAD V26.2): DwgIn reads the AcDbObject part as owner, reactor
        // count, reactors; DwgOut files it as owner, reactors, count.
        ObjectId[] reactors = (obj.GetPersistentReactorIds()?.Cast<ObjectId>() ?? Enumerable.Empty<ObjectId>()).ToArray();
        var ids = new List<ObjectId> { obj.OwnerId };
        var tokens = new List<AecToken> { AecToken.Soft(0), AecToken.Int32(reactors.Length) };
        foreach (ObjectId reactor in reactors)
        {
            tokens.Add(AecToken.Soft(ids.Count));
            ids.Add(reactor);
        }
        int offset = ids.Count;
        ids.AddRange(bodyIds);
        tokens.AddRange(body.Select(t => t.Kind is AecTokenKind.SoftPointer or AecTokenKind.HardPointer
            ? t with { Integer = t.Integer + offset } : t));

        var replay = new ReplayFiler(tokens, ids);
        obj.DwgIn(replay);
        if (replay.Error.Length > 0)
            return Result<Unit>.Failure($"{obj.GetRXClass().Name} refused the stream: {replay.Error}");
        if (replay.Consumed != tokens.Count)
            return Result<Unit>.Failure($"{obj.GetRXClass().Name} read {replay.Consumed} of {tokens.Count} tokens");

        return Record(obj).Bind(recorded =>
        {
            bool same = recorded.Body.Count == body.Count && recorded.Body.Select((t, i) =>
                t.Kind is AecTokenKind.SoftPointer or AecTokenKind.HardPointer
                    ? t.Kind == body[i].Kind && recorded.Ids[(int)t.Integer] == bodyIds[(int)body[i].Integer]
                    : t == body[i]).All(x => x);
            return same
                ? Result<Unit>.Success(Unit.Value)
                : Result<Unit>.Failure($"{obj.GetRXClass().Name} did not keep the stream it was given");
        });
    }

    /// <summary>What an object files, split into its AcDbObject header and the body after it.</summary>
    private static Result<(IReadOnlyList<AecToken> Body, IReadOnlyList<ObjectId> Ids)> Record(DBObject obj)
    {
        var recorder = new RecordingFiler();
        obj.DwgOut(recorder);
        if (recorder.Error.Length > 0)
            return Result<(IReadOnlyList<AecToken>, IReadOnlyList<ObjectId>)>.Failure(
                $"{obj.GetRXClass().Name} filed {recorder.Error}");
        IReadOnlyList<AecToken> t = recorder.Tokens;
        int pointers = 0;
        while (pointers < t.Count && t[pointers].Kind == AecTokenKind.SoftPointer) pointers++;
        if (pointers == 0 || pointers >= t.Count || t[pointers].Kind != AecTokenKind.Int32
            || t[pointers].Integer != pointers - 1)
            return Result<(IReadOnlyList<AecToken>, IReadOnlyList<ObjectId>)>.Failure(
                $"{obj.GetRXClass().Name} filed an object header of an unknown shape");
        return Result<(IReadOnlyList<AecToken>, IReadOnlyList<ObjectId>)>.Success(
            (t.Skip(pointers + 1).ToList(), recorder.Ids));
    }

    /// <summary>
    /// BricsCAD registers the AEC classes when a drawing that holds AEC objects is read, so
    /// a session that has read none loads the module that defines them.
    /// </summary>
    private static Result<Unit> EnsureClasses()
    {
        string[] classes = { FormatClass, DefinitionClass, SetClass };
        if (classes.All(SystemObjects.ClassDictionary.Contains)) return Result<Unit>.Success(Unit.Value);
        return Boundary("load AecScheduleData", () =>
        {
            SystemObjects.DynamicLinker.LoadModule("AecScheduleData.tx", false, false);
            string[] missing = classes.Where(c => !SystemObjects.ClassDictionary.Contains(c)).ToArray();
            return missing.Length == 0
                ? Result<Unit>.Success(Unit.Value)
                : Result<Unit>.Failure("BricsCAD's AEC classes are not registered: " + string.Join(", ", missing));
        });
    }

    /// <summary>The AutoCAD-API boundary: what the host throws becomes a fault.</summary>
    private static Result<T> Boundary<T>(string what, Func<Result<T>> body)
    {
        try
        {
            return body();
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure($"Property sets, {what}: {ex}");
        }
    }
}
