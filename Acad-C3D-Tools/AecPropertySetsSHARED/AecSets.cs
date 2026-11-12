using System;
using System.Collections.Generic;
using System.Linq;

using CSharpFunctionalExtensions;

using Teigha.DatabaseServices;

namespace Norsyn.AecPropertySets;

// CSharpFunctionalExtensions has an Entity too (a DDD base class). Inside the namespace, so a project that already has
// a global alias of that name (NorsynUtilityTools) gets no duplicate (CS1537): this one shadows it.
using Entity = Teigha.DatabaseServices.Entity;

/// <summary>A property set attached to an entity, decoded with its definition.</summary>
/// <param name="Key">The set's key in the entity's AEC_PROPERTY_SETS dictionary (*A1 ...).</param>
/// <param name="Name">The definition's name: its key in AEC_PROPERTY_SET_DEFS.</param>
internal sealed record AecAttachedSet(
    ObjectId SetId, string Key, ObjectId DefinitionId, string Name, AecDefinition Definition, AecSet Set);

/// <summary>
/// Reads and edits the property sets on entities, on BricsCAD. One instance per
/// transaction: it decodes each definition once.
/// </summary>
internal sealed class AecSets
{
    private readonly Transaction _tx;
    private readonly Dictionary<ObjectId, Result<(string Name, AecDefinition Definition), AecError>> _definitions = new();

    public AecSets(Transaction tx) => _tx = tx;

    private sealed record SetEntry(string Key, ObjectId Id);

    /// <summary>
    /// Every set on the entity, in dictionary order. A set that cannot be read is a fault
    /// in its place, naming the set; the others still read.
    /// </summary>
    public IReadOnlyList<Result<AecAttachedSet, AecError>> SetsOf(Entity entity) =>
        SetEntriesOf(entity).Match(
            entries => entries.Select(entry => Set(entry.Key, entry.Id)).ToList(),
            error => new List<Result<AecAttachedSet, AecError>> { Result.Failure<AecAttachedSet, AecError>(error) });

    private Result<IReadOnlyList<SetEntry>, AecError> SetEntriesOf(Entity entity)
    {
        IReadOnlyList<SetEntry> none = Array.Empty<SetEntry>();
        if (entity.ExtensionDictionary.IsNull) return Result.Success<IReadOnlyList<SetEntry>, AecError>(none);
        return AecObjects.Open<DBDictionary>(_tx, entity.ExtensionDictionary, OpenMode.ForRead,
                $"the extension dictionary of {entity.Handle}")
            .Bind(xdict => xdict.Contains(AecObjects.SetDictionary)
                ? AecObjects.Entry<DBDictionary>(_tx, xdict, AecObjects.SetDictionary, OpenMode.ForRead)
                    .Bind(setDict => AecObjects.Host($"list the sets of {entity.Handle}", () =>
                        (IReadOnlyList<SetEntry>)setDict.Cast<DBDictionaryEntry>()
                            .Select(entry => new SetEntry(entry.Key, entry.Value))
                            .ToList()))
                : Result.Success<IReadOnlyList<SetEntry>, AecError>(none));
    }

    /// <summary>
    /// Stores the value for one property of an attached set, as a manual value of the
    /// property's data type, and returns the set's new id. The set is read again first, so
    /// the edit lands on what the drawing holds now.
    /// The set is REPLACED: a new set object is filled with the edited stream under the
    /// old one's key, and the old one is erased. DwgIn into the existing object would skip
    /// the write-enable that records undo (assertWriteEnabled); erase and add are ordinary,
    /// undoable database changes, and the fill is the one LERImporter's sets are made with.
    /// On a fault the caller aborts the transaction.
    /// </summary>
    public Result<ObjectId, AecError> Write(ObjectId setId, int propertyId, AecValue value) =>
        AecObjects.Open<DBObject>(_tx, setId, OpenMode.ForRead, $"set {setId.Handle}").Bind(old =>
            AecObjects.Record(old).Bind(recorded =>
                AecStream.ReadSet(recorded.Body).Bind(set =>
                    DefinitionOf(set, recorded).Bind(def =>
                        def.Definition.Definition.ById(propertyId)
                            .ToResult(AecError.Drawing($"{def.Definition.Name} has no property with id {propertyId}"))
                            .Bind(property => property.Type.Bind(type => type == value.Type
                                ? UnitResult.Success<AecError>()
                                : UnitResult.Failure(AecError.Value($"{property.Name} is a {type} property, not {value.Type}"))))
                            .Bind(() => AecStream.WithValue(recorded.Body, set, propertyId, value))
                            .Bind(body => Replace(old, body, recorded.Ids))))));

    private Result<ObjectId, AecError> Replace(DBObject old, IReadOnlyList<AecToken> body, IReadOnlyList<ObjectId> ids) =>
        AecObjects.KeyInOwner(_tx, old).Bind(key =>
            AecObjects.Open<DBDictionary>(_tx, old.OwnerId, OpenMode.ForWrite, $"the owner of set {key}").Bind(owner =>
                AecObjects.Host($"erase set {key}", () =>
                    {
                        owner.Remove(old.ObjectId);
                        old.UpgradeOpen();
                        old.Erase();
                    })
                    .Bind(() => AecObjects.Create(AecObjects.SetClass))
                    .Bind(fresh => AecObjects.Host($"add set {key}", () =>
                        {
                            owner.SetAt(key, fresh);
                            _tx.AddNewlyCreatedDBObject(fresh, true);
                        })
                        .Bind(() => AecObjects.Fill(fresh, body, ids))
                        .Map(() => fresh.ObjectId))));

    private Result<AecAttachedSet, AecError> Set(string key, ObjectId setId) =>
        AecObjects.Open<DBObject>(_tx, setId, OpenMode.ForRead, $"set {key}")
            .Bind(setObject => AecObjects.Record(setObject))
            .Bind(recorded => AecStream.ReadSet(recorded.Body).Bind(set =>
                DefinitionOf(set, recorded).Map(def => new AecAttachedSet(
                    setId, key, def.Id, def.Definition.Name, def.Definition.Definition, set))))
            .MapError(error => error.In($"set {key}"));

    // The set's header files the entity and the definition as hard pointers; the
    // definition is the one of the definition class.
    private Result<(ObjectId Id, (string Name, AecDefinition Definition) Definition), AecError> DefinitionOf(
        AecSet set, AecObjects.Recorded recorded) =>
        AecObjects.Host("find the set's definition", () => set.HeaderPointers
                .Where(slot => slot >= 0 && slot < recorded.Ids.Count)
                .Select(slot => recorded.Ids[slot])
                .Where(id => !id.IsNull && !id.IsErased)
                .TryFirst(id => _tx.GetObject(id, OpenMode.ForRead).GetRXClass().Name == AecObjects.DefinitionClass))
            .Bind(found => found.ToResult(AecError.Drawing("the set points at no property set definition")))
            .Bind(id => Definition(id).Map(def => (id, def)));

    /// <summary>A definition, decoded once per instance, with the name it has in the drawing.</summary>
    public Result<(string Name, AecDefinition Definition), AecError> Definition(ObjectId defId) =>
        _definitions.TryFind(defId).Match(
            known => known,
            () =>
            {
                Result<(string Name, AecDefinition Definition), AecError> decoded =
                    AecObjects.Open<DBObject>(_tx, defId, OpenMode.ForRead, "a definition").Bind(def =>
                        AecObjects.KeyInOwner(_tx, def).Bind(name =>
                            AecObjects.Record(def).Bind(recorded =>
                                AecStream.ReadDefinition(recorded.Body).Map(d => (name, d)))));
                _definitions[defId] = decoded;
                return decoded;
            });
}
