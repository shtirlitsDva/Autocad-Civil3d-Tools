using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;

using CSharpFunctionalExtensions;

using IntersectUtilities.UtilsCommon;

using Norsyn.AecPropertySets;

using AecDataType = Autodesk.Aec.PropertyData.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// Property-set values and definitions reached through the open transaction. Reading attaches no set; the checks
/// decide, before anything is written, whether a later write can be made. Automatic properties compute their own
/// values, so they are never written and never a reason to refuse.
/// </summary>
internal static class PropertySetFields
{
    /// <summary>The value as text; None when the entity carries no such set or property, or the value is empty.</summary>
    internal static Result<Maybe<string>, AecError> ReadText(
        Entity entity, Transaction tx, string setName, string propertyName) =>
        AutoCadCall.Run(
            $"læs {setName}.{propertyName} på {entity.Handle}",
            () => Field(entity, tx, setName, propertyName)
                .Bind(field => Maybe.From(field.Set.GetAt(field.Id)))
                .Map(value => $"{value}"));

    /// <summary>
    /// Whether a value is to be written to the property: true when the entity carries it as a manual property
    /// that takes a value of <paramref name="type"/>; false when it does not carry it or it is automatic; a refusal
    /// when it carries a manual property that cannot take the value.
    /// </summary>
    internal static Result<bool, AecError> CarriesWritable(
        Entity entity, Transaction tx, string setName, string propertyName, AecDataType type) =>
        AutoCadCall
            .Run(
                $"læs {setName}.{propertyName} på {entity.Handle}",
                () => Sets(entity, tx)
                    .TryFirst(set => set.PropertySetDefinitionName == setName)
                    .Bind(set => PropertyOf(DefinitionOf(set, tx), propertyName)))
            .Bind((Maybe<PropertyDefinition> carried) => carried.Match(
                property => property.Automatic
                    ? Result.Success<bool, AecError>(false)
                    : WriteBlocker(property, type).Match(
                        reason => Result.Failure<bool, AecError>(AecError.Drawing(
                            $"{setName}.{propertyName} på {entity.Handle} kan ikke sættes ({reason})")),
                        () => Result.Success<bool, AecError>(true)),
                () => Result.Success<bool, AecError>(false)));

    /// <summary>
    /// Refuses when a manual property of a set the entity carries is read-only, so <see cref="CopySets"/> could not
    /// write its value to a new entity.
    /// </summary>
    internal static UnitResult<AecError> CheckCopyable(Entity entity, Transaction tx) =>
        AutoCadCall
            .Run(
                $"læs egenskabssæt på {entity.Handle}",
                () => Sets(entity, tx)
                    .Select(set => DefinitionOf(set, tx))
                    .SelectMany(definition => definition.Definitions
                        .Cast<PropertyDefinition>()
                        .Where(property => !property.Automatic && property.IsReadOnly)
                        .Select(property => $"{definition.Name}.{property.Name} er skrivebeskyttet"))
                    .TryFirst())
            .Bind((Maybe<string> blocker) => blocker.Match(
                reason => UnitResult.Failure(AecError.Drawing(
                    $"polylinje {entity.Handle} kan ikke kopieres til et nyt stykke ({reason})")),
                () => UnitResult.Success<AecError>()));

    /// <summary>
    /// When the drawing defines the set: refuses unless the property is manual and takes a value of
    /// <paramref name="type"/>, and the set applies to every class named. A drawing without the set passes; code
    /// creates it.
    /// </summary>
    internal static UnitResult<AecError> CheckDefinition(
        Database db,
        Transaction tx,
        string setName,
        string propertyName,
        AecDataType type,
        IEnumerable<string> classNames) =>
        AutoCadCall
            .Run($"læs definitionen {setName}", () => DefinitionNamed(db, tx, setName).Bind(definition =>
                PropertyOf(definition, propertyName)
                    .Match(
                        property => property.Automatic ? Maybe.From("automatisk") : WriteBlocker(property, type),
                        () => Maybe.From("mangler"))
                    .Match(
                        reason => Maybe.From($"{propertyName}: {reason}"),
                        () => classNames
                            .TryFirst(name => !definition.AppliesToAll &&
                                !definition.AppliesToFilter.Cast<string>().Contains(name))
                            .Map(name => $"gælder ikke for {name}"))))
            .Bind((Maybe<string> blocker) => blocker.Match(
                reason => UnitResult.Failure(AecError.Drawing($"{setName} i tegningen kan ikke skrives ({reason})")),
                () => UnitResult.Success<AecError>()));

    /// <summary>
    /// Writes the value where the entity carries the set and the property; elsewhere does nothing. Call it only for
    /// a property <see cref="CarriesWritable"/> said is to be written.
    /// </summary>
    internal static void WriteWhereCarried(
        Entity entity, Transaction tx, string setName, string propertyName, object value) =>
        Field(entity, tx, setName, propertyName).Tap(field =>
        {
            field.Set.CheckOrOpenForWrite();
            field.Set.SetAt(field.Id, value);
        });

    /// <summary>
    /// Gives <paramref name="target"/> every property set <paramref name="source"/> carries, with the values of its
    /// manual properties; automatic properties compute their own. Call it only for a source
    /// <see cref="CheckCopyable"/> passed. Replaces PropertySetManager.CopyAllProperties, which writes every
    /// property, automatic ones included.
    /// </summary>
    internal static void CopySets(Entity source, Entity target, Transaction tx)
    {
        foreach (PropertySet sourceSet in Sets(source, tx).ToList())
        {
            PropertySetDefinition definition = DefinitionOf(sourceSet, tx);
            ObjectId targetSetId = Sets(target, tx)
                .TryFirst(set => set.PropertySetDefinition == sourceSet.PropertySetDefinition)
                .Match(set => set.ObjectId, () => Attach(target, definition.ObjectId));
            PropertySet targetSet = (PropertySet)tx.GetObject(targetSetId, OpenMode.ForWrite);

            foreach (PropertyDefinition property in definition.Definitions
                .Cast<PropertyDefinition>()
                .Where(property => !property.Automatic))
            {
                Maybe.From(sourceSet.GetAt(sourceSet.PropertyNameToId(property.Name)))
                    .Tap(value => targetSet.SetAt(targetSet.PropertyNameToId(property.Name), value));
            }
        }
    }

    private static ObjectId Attach(Entity target, ObjectId definitionId)
    {
        target.CheckOrOpenForWrite();
        PropertyDataServices.AddPropertySet(target, definitionId);
        return PropertyDataServices.GetPropertySet(target, definitionId);
    }

    private static Maybe<(PropertySet Set, int Id)> Field(
        Entity entity, Transaction tx, string setName, string propertyName) =>
        Sets(entity, tx)
            .TryFirst(set => set.PropertySetDefinitionName == setName)
            .Where(set => PropertyOf(DefinitionOf(set, tx), propertyName).HasValue)
            .Map(set => (Set: set, Id: set.PropertyNameToId(propertyName)));

    private static IEnumerable<PropertySet> Sets(Entity entity, Transaction tx) =>
        PropertyDataServices.GetPropertySets(entity)
            .Cast<ObjectId>()
            .Select(id => (PropertySet)tx.GetObject(id, OpenMode.ForRead));

    private static PropertySetDefinition DefinitionOf(PropertySet set, Transaction tx) =>
        (PropertySetDefinition)tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead);

    private static Maybe<PropertySetDefinition> DefinitionNamed(Database db, Transaction tx, string setName)
    {
        DictionaryPropertySetDefinitions definitions = new(db);
        return definitions.Has(setName, tx)
            ? Maybe.From((PropertySetDefinition)tx.GetObject(definitions.GetAt(setName), OpenMode.ForRead))
            : Maybe<PropertySetDefinition>.None;
    }

    private static Maybe<PropertyDefinition> PropertyOf(PropertySetDefinition definition, string propertyName) =>
        definition.Definitions.Cast<PropertyDefinition>().TryFirst(property => property.Name == propertyName);

    /// <summary>Why a value of <paramref name="type"/> cannot be written to a manual property; None when it can.</summary>
    private static Maybe<string> WriteBlocker(PropertyDefinition property, AecDataType type) =>
        property.IsReadOnly ? Maybe.From("skrivebeskyttet")
        : property.DataType != type ? Maybe.From($"datatype {property.DataType}")
        : Maybe<string>.None;
}
