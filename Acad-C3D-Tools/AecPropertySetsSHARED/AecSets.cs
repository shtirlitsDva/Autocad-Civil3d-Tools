using System.Collections.Generic;

using Teigha.DatabaseServices;

namespace Norsyn.AecPropertySets;

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
    private readonly Dictionary<ObjectId, Result<(string Name, AecDefinition Definition)>> _definitions = new();

    public AecSets(Transaction tx) => _tx = tx;

    /// <summary>
    /// Every set on the entity, in dictionary order. A set that cannot be read is a fault
    /// in its place, naming the set; the others still read.
    /// </summary>
    public IReadOnlyList<Result<AecAttachedSet>> SetsOf(Entity entity)
    {
        var sets = new List<Result<AecAttachedSet>>();
        Result<Unit> listed = AecObjects.Boundary($"list the sets of {entity.Handle}", () =>
        {
            if (entity.ExtensionDictionary.IsNull) return Result<Unit>.Success(Unit.Value);
            var xdict = (DBDictionary)_tx.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
            if (!xdict.Contains(AecObjects.SetDictionary)) return Result<Unit>.Success(Unit.Value);
            var setDict = (DBDictionary)_tx.GetObject(xdict.GetAt(AecObjects.SetDictionary), OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in setDict)
                sets.Add(Set(entry.Key, entry.Value));
            return Result<Unit>.Success(Unit.Value);
        });
        listed.Match(_ => Unit.Value, fault => { sets.Add(Result<AecAttachedSet>.Failure(fault)); return Unit.Value; });
        return sets;
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
    public Result<ObjectId> Write(ObjectId setId, int propertyId, AecValue value) =>
        AecObjects.Boundary($"write property {propertyId}", () =>
        {
            DBObject old = _tx.GetObject(setId, OpenMode.ForRead);
            return AecObjects.Record(old).Bind(recorded =>
                AecStream.ReadSet(recorded.Body).Bind(set =>
                    DefinitionOf(set, recorded).Bind(def =>
                        def.Definition.Definition.ById(propertyId)
                            .OrFault($"{def.Definition.Name} has no property with id {propertyId}")
                            .Bind(property => property.Type.Bind(type => type == value.Type
                                ? Result<Unit>.Success(Unit.Value)
                                : Result<Unit>.Failure($"{property.Name} is a {type} property, not {value.Type}")))
                            .Bind(_ => AecStream.WithValue(recorded.Body, set, propertyId, value))
                            .Bind(body => Replace(old, body, recorded.Ids)))));
        });

    private Result<ObjectId> Replace(DBObject old, IReadOnlyList<AecToken> body, IReadOnlyList<ObjectId> ids) =>
        AecObjects.KeyInOwner(_tx, old).Bind(key =>
        {
            var owner = (DBDictionary)_tx.GetObject(old.OwnerId, OpenMode.ForWrite);
            owner.Remove(old.ObjectId);
            old.UpgradeOpen();
            old.Erase();
            DBObject fresh = AecObjects.Create(AecObjects.SetClass);
            owner.SetAt(key, fresh);
            _tx.AddNewlyCreatedDBObject(fresh, true);
            return AecObjects.Fill(fresh, body, ids).Map(_ => fresh.ObjectId);
        });

    private Result<AecAttachedSet> Set(string key, ObjectId setId) =>
        AecObjects.Boundary($"read set {key}", () =>
            AecObjects.Record(_tx.GetObject(setId, OpenMode.ForRead)).Bind(recorded =>
                AecStream.ReadSet(recorded.Body).Bind(set =>
                    DefinitionOf(set, recorded).Map(def => new AecAttachedSet(
                        setId, key, def.Id, def.Definition.Name, def.Definition.Definition, set)))))
        .Match(Result<AecAttachedSet>.Success, fault => Result<AecAttachedSet>.Failure($"set {key}: {fault}"));

    // The set's header files the entity and the definition as hard pointers; the
    // definition is the one of the definition class.
    private Result<(ObjectId Id, (string Name, AecDefinition Definition) Definition)> DefinitionOf(
        AecSet set, AecObjects.Recorded recorded)
    {
        foreach (int slot in set.HeaderPointers)
        {
            if (slot < 0 || slot >= recorded.Ids.Count) continue;
            ObjectId id = recorded.Ids[slot];
            if (id.IsNull || id.IsErased) continue;
            DBObject candidate = _tx.GetObject(id, OpenMode.ForRead);
            if (candidate.GetRXClass().Name != AecObjects.DefinitionClass) continue;
            return Definition(id).Map(def => (id, def));
        }
        return Result<(ObjectId, (string, AecDefinition))>.Failure("the set points at no property set definition");
    }

    /// <summary>A definition, decoded once per instance, with the name it has in the drawing.</summary>
    public Result<(string Name, AecDefinition Definition)> Definition(ObjectId defId)
    {
        if (_definitions.TryGetValue(defId, out var known)) return known;
        Result<(string Name, AecDefinition Definition)> decoded = AecObjects.Boundary("read a definition", () =>
        {
            DBObject def = _tx.GetObject(defId, OpenMode.ForRead);
            return AecObjects.KeyInOwner(_tx, def).Bind(name =>
                AecObjects.Record(def).Bind(recorded =>
                    AecStream.ReadDefinition(recorded.Body).Map(d => (name, d))));
        });
        _definitions[defId] = decoded;
        return decoded;
    }
}
