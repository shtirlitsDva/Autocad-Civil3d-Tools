using System;
using System.Collections.Generic;
using System.Linq;

using Teigha.DatabaseServices;
using Teigha.Runtime;

namespace Norsyn.AecPropertySets;

/// <summary>
/// AEC property-set objects on BricsCAD, through their filer stream. BricsCAD reads and
/// writes AEC objects (its AEC classes are ODA's) but has no managed API for them, so this
/// does what a drawing read does: an object is handed its stream through DwgIn, and its
/// content is read back through DwgOut. The drawing is laid out as Civil 3D lays it out:
///   NOD / AEC_PROPERTY_FORMAT_DEFS / "Standard"  AecDbScheduleDataFormat
///   NOD / AEC_PROPERTY_SET_DEFS / set name        AecDbPropertySetDef
///   entity / extension dictionary / AEC_PROPERTY_SETS / *A1, *A2 ...  AecDbPropertySet
/// Every call that reaches the host is behind Boundary: the host throws, we return faults.
/// </summary>
internal static class AecObjects
{
    public const string FormatDictionary = "AEC_PROPERTY_FORMAT_DEFS";
    public const string DefinitionDictionary = "AEC_PROPERTY_SET_DEFS";
    public const string SetDictionary = "AEC_PROPERTY_SETS";
    public const string FormatName = "Standard";
    public const string FormatClass = "AecDbScheduleDataFormat";
    public const string DefinitionClass = "AecDbPropertySetDef";
    public const string SetClass = "AecDbPropertySet";

    /// <summary>What an object files after its AcDbObject header. Pointer slots index Ids.</summary>
    public sealed record Recorded(IReadOnlyList<AecToken> Body, IReadOnlyList<ObjectId> Ids);

    /// <summary>
    /// BricsCAD registers the AEC classes when a drawing that holds AEC objects is read, so
    /// a session that has read none loads the module that defines them.
    /// </summary>
    public static Result<Unit> EnsureClasses()
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

    /// <summary>An empty object of an AEC class, not yet in a database.</summary>
    public static DBObject Create(string className) =>
        (DBObject)((RXClass)SystemObjects.ClassDictionary[className]).Create();

    /// <summary>The named child dictionary, made when it is not there.</summary>
    public static DBDictionary Subdictionary(Transaction tx, DBDictionary parent, string name, bool hard)
    {
        if (parent.Contains(name)) return (DBDictionary)tx.GetObject(parent.GetAt(name), OpenMode.ForRead);
        DBDictionary child = new DBDictionary();
        if (!parent.IsWriteEnabled) parent.UpgradeOpen();
        parent.SetAt(name, child);
        tx.AddNewlyCreatedDBObject(child, true);
        if (hard) child.TreatElementsAsHard = true;
        return child;
    }

    /// <summary>
    /// Hands a database-resident object, open for write, its stream: its own owner and
    /// reactors, then the body. Then records it again, and the object must file exactly the
    /// body it was given. A new object is filled this way; an existing one is rewritten
    /// (the transaction's undo keeps what it filed before).
    /// </summary>
    public static Result<Unit> Fill(DBObject obj, IReadOnlyList<AecToken> body, IReadOnlyList<ObjectId> bodyIds) =>
        Boundary($"write {obj.GetRXClass().Name}", () =>
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
            tokens.AddRange(body.Select(t => t.IsPointer ? t with { Integer = t.Integer + offset } : t));

            if (!obj.IsWriteEnabled) obj.UpgradeOpen();
            var replay = new ReplayFiler(tokens, ids);
            obj.DwgIn(replay);
            if (replay.Error.Length > 0)
                return Result<Unit>.Failure($"{obj.GetRXClass().Name} refused the stream: {replay.Error}");
            if (replay.Consumed != tokens.Count)
                return Result<Unit>.Failure($"{obj.GetRXClass().Name} read {replay.Consumed} of {tokens.Count} tokens");

            return Record(obj).Bind(recorded =>
            {
                bool same = recorded.Body.Count == body.Count && recorded.Body.Select((t, i) =>
                    t.IsPointer
                        ? t.Kind == body[i].Kind && recorded.Ids[(int)t.Integer] == bodyIds[(int)body[i].Integer]
                        : t == body[i]).All(x => x);
                return same
                    ? Result<Unit>.Success(Unit.Value)
                    : Result<Unit>.Failure($"{obj.GetRXClass().Name} did not keep the stream it was given");
            });
        });

    /// <summary>
    /// What an object files, without its AcDbObject header. The body's pointer slots index
    /// Ids, which holds the header's ids too.
    /// </summary>
    public static Result<Recorded> Record(DBObject obj) =>
        Boundary($"read {obj.GetRXClass().Name}", () =>
        {
            var recorder = new RecordingFiler();
            obj.DwgOut(recorder);
            if (recorder.Error.Length > 0)
                return Result<Recorded>.Failure($"{obj.GetRXClass().Name} filed {recorder.Error}");
            IReadOnlyList<AecToken> t = recorder.Tokens;
            int pointers = 0;
            while (pointers < t.Count && t[pointers].Kind == AecTokenKind.SoftPointer) pointers++;
            if (pointers == 0 || pointers >= t.Count || t[pointers].Kind != AecTokenKind.Int32
                || t[pointers].Integer != pointers - 1)
                return Result<Recorded>.Failure($"{obj.GetRXClass().Name} filed an object header of an unknown shape");
            return Result<Recorded>.Success(new Recorded(t.Skip(pointers + 1).ToList(), recorder.Ids));
        });

    /// <summary>The key the object has in the dictionary that owns it.</summary>
    public static Result<string> KeyInOwner(Transaction tx, DBObject obj) =>
        Boundary("find the owner key", () =>
        {
            if (tx.GetObject(obj.OwnerId, OpenMode.ForRead) is not DBDictionary owner)
                return Result<string>.Failure($"{obj.GetRXClass().Name} {obj.Handle} is not owned by a dictionary");
            foreach (DBDictionaryEntry entry in owner)
                if (entry.Value == obj.ObjectId) return Result<string>.Success(entry.Key);
            return Result<string>.Failure($"{obj.GetRXClass().Name} {obj.Handle} is not in its owner dictionary");
        });

    /// <summary>The host API boundary: what the host throws becomes a fault.</summary>
    public static Result<T> Boundary<T>(string what, Func<Result<T>> body)
    {
        try
        {
            return body();
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure($"Property sets, {what}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
