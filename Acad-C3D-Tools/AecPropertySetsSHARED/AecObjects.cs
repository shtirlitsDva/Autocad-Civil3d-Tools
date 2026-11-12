using System;
using System.Collections.Generic;
using System.Linq;

using CSharpFunctionalExtensions;

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
/// Each call that can throw into us is made through Host, which turns what BricsCAD throws
/// (Teigha.Runtime.Exception) into a fault; anything else is a bug and passes.
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
    public static UnitResult<AecError> EnsureClasses()
    {
        string[] classes = { FormatClass, DefinitionClass, SetClass };
        if (classes.All(SystemObjects.ClassDictionary.Contains)) return UnitResult.Success<AecError>();
        return Host("load AecScheduleData",
                () => SystemObjects.DynamicLinker.LoadModule("AecScheduleData.tx", false, false))
            .Bind(_ =>
            {
                string[] missing = classes.Where(c => !SystemObjects.ClassDictionary.Contains(c)).ToArray();
                return missing.Length == 0
                    ? UnitResult.Success<AecError>()
                    : UnitResult.Failure(AecError.Drawing(
                        "BricsCAD's AEC classes are not registered: " + string.Join(", ", missing)));
            });
    }

    /// <summary>An empty object of an AEC class, not yet in a database.</summary>
    public static Result<DBObject, AecError> Create(string className) =>
        Host($"create a {className}", () =>
                SystemObjects.ClassDictionary[className] is RXClass rxClass && rxClass.Create() is DBObject created
                    ? Maybe.From(created)
                    : Maybe<DBObject>.None)
            .Bind(created => created.ToResult(AecError.Drawing($"BricsCAD makes no database object of class {className}")));

    /// <summary>The named child dictionary, made when it is not there.</summary>
    public static Result<DBDictionary, AecError> Subdictionary(Transaction tx, DBDictionary parent, string name, bool hard) =>
        parent.Contains(name)
            ? Entry<DBDictionary>(tx, parent, name, OpenMode.ForRead)
            : Host($"make the dictionary {name}", () =>
            {
                DBDictionary child = new DBDictionary();
                if (!parent.IsWriteEnabled) parent.UpgradeOpen();
                parent.SetAt(name, child);
                tx.AddNewlyCreatedDBObject(child, true);
                if (hard) child.TreatElementsAsHard = true;
                return child;
            });

    /// <summary>The object, when it is a <typeparamref name="T"/>.</summary>
    public static Result<T, AecError> Open<T>(Transaction tx, ObjectId id, OpenMode mode, string what) where T : DBObject =>
        Host($"open {what}", () => tx.GetObject(id, mode)).Bind(opened => opened is T typed
            ? Result.Success<T, AecError>(typed)
            : Result.Failure<T, AecError>(AecError.Drawing($"{what} is a {opened.GetRXClass().Name}, not a {typeof(T).Name}")));

    /// <summary>The dictionary's entry under <paramref name="key"/>, when it is a <typeparamref name="T"/>.</summary>
    public static Result<T, AecError> Entry<T>(Transaction tx, DBDictionary dictionary, string key, OpenMode mode)
        where T : DBObject =>
        Host($"find {key}", () => dictionary.GetAt(key)).Bind(id => Open<T>(tx, id, mode, key));

    /// <summary>
    /// Hands a new, database-resident object its stream: its own owner and reactors, then
    /// the body. Then records it again, and the object must file exactly the body it was
    /// given. On a fault the caller aborts the transaction.
    /// </summary>
    public static UnitResult<AecError> Fill(DBObject obj, IReadOnlyList<AecToken> body, IReadOnlyList<ObjectId> bodyIds)
    {
        string name = obj.GetRXClass().Name;
        if (!obj.ExtensionDictionary.IsNull)
            return UnitResult.Failure(AecError.Drawing($"{name} has an extension dictionary; its header is not known"));
        // MEASURED (BricsCAD V26.2): DwgIn reads the AcDbObject part as owner, reactor
        // count, reactors; DwgOut files it as owner, reactors, count. The host hands null for
        // an object with no reactors.
        return Host($"read the reactors of {name}",
                () => obj.GetPersistentReactorIds()?.Cast<ObjectId>().ToArray() ?? Array.Empty<ObjectId>())
            .Bind(reactors =>
            {
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

                return Host($"write {name}", () =>
                    {
                        if (!obj.IsWriteEnabled) obj.UpgradeOpen();
                        // Disposed, always: each filer's native half holds a strong handle to it until the
                        // filer is disposed, so one never disposed stays in memory for good, and with it the
                        // code it belongs to (READ, decompiled TD_Mgd 27.3; MEASURED, Norsyn Utility Tools
                        // export under NSLOAD, 2026-10-27: 65 of each filer held per export).
                        using var replay = new ReplayFiler(tokens, ids);
                        obj.DwgIn(replay);
                        return (replay.Error, replay.Consumed);
                    })
                    .Bind(read => read.Error.Match(
                        error => UnitResult.Failure(AecError.Stream($"{name} refused the stream: {error}")),
                        () => read.Consumed == tokens.Count
                            ? UnitResult.Success<AecError>()
                            : UnitResult.Failure(AecError.Stream($"{name} read {read.Consumed} of {tokens.Count} tokens"))));
            })
            .Bind(() => Record(obj))
            .Bind(recorded =>
            {
                bool same = recorded.Body.Count == body.Count && recorded.Body.Select((t, i) =>
                    t.IsPointer
                        ? t.Kind == body[i].Kind && recorded.Ids[(int)t.Integer] == bodyIds[(int)body[i].Integer]
                        : t == body[i]).All(x => x);
                return same
                    ? UnitResult.Success<AecError>()
                    : UnitResult.Failure(AecError.Stream($"{name} did not keep the stream it was given"));
            });
    }

    /// <summary>
    /// What an object files, without its AcDbObject header. The body's pointer slots index
    /// Ids, which holds the header's ids too.
    /// </summary>
    public static Result<Recorded, AecError> Record(DBObject obj)
    {
        string name = obj.GetRXClass().Name;
        return Host($"read {name}", () =>
            {
                using var recorder = new RecordingFiler();
                obj.DwgOut(recorder);
                return (recorder.Error, recorder.Tokens, recorder.Ids);
            })
            .Bind(filed => filed.Error.Match(
                error => Result.Failure<Recorded, AecError>(AecError.Stream($"{name} filed {error}")),
                () =>
                {
                    IReadOnlyList<AecToken> t = filed.Tokens;
                    int pointers = 0;
                    while (pointers < t.Count && t[pointers].Kind == AecTokenKind.SoftPointer) pointers++;
                    if (pointers == 0 || pointers >= t.Count || t[pointers].Kind != AecTokenKind.Int32
                        || t[pointers].Integer != pointers - 1)
                        return Result.Failure<Recorded, AecError>(
                            AecError.Stream($"{name} filed an object header of an unknown shape"));
                    return Result.Success<Recorded, AecError>(new Recorded(t.Skip(pointers + 1).ToList(), filed.Ids));
                }));
    }

    /// <summary>The key the object has in the dictionary that owns it.</summary>
    public static Result<string, AecError> KeyInOwner(Transaction tx, DBObject obj)
    {
        string what = $"{obj.GetRXClass().Name} {obj.Handle}";
        return Open<DBDictionary>(tx, obj.OwnerId, OpenMode.ForRead, $"the owner of {what}")
            .Bind(owner => Host($"list the owner of {what}", () => owner.Cast<DBDictionaryEntry>()
                .Where(entry => entry.Value == obj.ObjectId)
                .Select(entry => entry.Key)
                .TryFirst()))
            .Bind(key => key.ToResult(AecError.Drawing($"{what} is not in its owner dictionary")));
    }

    /// <summary>One call into the host: what BricsCAD throws becomes a fault.</summary>
    public static Result<T, AecError> Host<T>(string what, Func<T> call)
    {
        try
        {
            return Result.Success<T, AecError>(call());
        }
        catch (Teigha.Runtime.Exception ex)
        {
            return Result.Failure<T, AecError>(AecError.Host(what, ex));
        }
    }

    /// <summary>One call into the host that returns nothing: what BricsCAD throws becomes a fault.</summary>
    public static UnitResult<AecError> Host(string what, Action call)
    {
        try
        {
            call();
            return UnitResult.Success<AecError>();
        }
        catch (Teigha.Runtime.Exception ex)
        {
            return UnitResult.Failure(AecError.Host(what, ex));
        }
    }
}
