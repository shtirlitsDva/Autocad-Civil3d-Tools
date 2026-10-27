using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using CSharpFunctionalExtensions;

using Norsyn.AecPropertySets;
using Norsyn.DrawingTools.Progress;
using Norsyn.DrawingTools.PropertySets;

using System.Globalization;

using AecType = Autodesk.Aec.PropertyData.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LER2;

/// <summary>
/// Civil 3D's side of the property set scanner seam (<see cref="IPropertySetScanner"/>,
/// NorsynDrawingTools' NorsynApplicationsSHARED): the active drawing's property sets read
/// through the AEC API, never the filer stream. BricsCAD's twin is NorsynDrawingToolsManaged's
/// AecPropertySetSource. LER2MANHOLEQA's shared window reads the drawing through it.
/// </summary>
internal sealed class CivilPropertySetScanner : IPropertySetScanner
{
    // Model space is read in batches: the progress port tells each batch, not each entity.
    private const int ScanBatch = 500;

    // The four manual data types, as the shared records spell them. Any other (automatic,
    // list, graphic, increments) is shown as its text and has no editable type.
    private static readonly Dictionary<AecType, (AecDataType Type, Func<object, AecValue> Read)> s_manual = new()
    {
        [AecType.Integer] = (AecDataType.Integer, v => new AecValue.Integer(Convert.ToInt32(v, CultureInfo.InvariantCulture))),
        [AecType.Real] = (AecDataType.Real, v => new AecValue.Real(Convert.ToDouble(v, CultureInfo.InvariantCulture))),
        [AecType.Text] = (AecDataType.Text, v => new AecValue.Text(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "")),
        [AecType.TrueFalse] = (AecDataType.TrueFalse, v => new AecValue.TrueFalse(Convert.ToBoolean(v, CultureInfo.InvariantCulture))),
    };

    public string DrawingName => Active().Match(doc => System.IO.Path.GetFileName(doc.Name), () => "");

    public Result<IReadOnlyList<EntitySets>, AecError> ScanModelSpace(IProgressSink progress) =>
        LastResort("scan model space", () => Active().Match(
            doc =>
            {
                using DocumentLock _ = doc.LockDocument();
                using Transaction tx = doc.Database.TransactionManager.StartTransaction();
                Result<IReadOnlyList<EntitySets>, AecError> result = Scan(doc.Database, tx, progress);
                tx.Commit();
                return result;
            },
            () => Result.Failure<IReadOnlyList<EntitySets>, AecError>(AecError.Drawing("No drawing is open."))));

    // The scan's one last-resort guard. LER2MANHOLEQA's window runs it from a button, so a
    // throw that is a bug becomes a fault the window shows, not a crash of Civil 3D. The
    // stop of a scan (OperationStoppedException) passes through: the caller catches it.
    private static Result<T, AecError> LastResort<T>(string what, Func<Result<T, AecError>> body)
    {
        try
        {
            return body();
        }
        catch (System.Exception ex) when (ex is not OperationStoppedException)
        {
            return Result.Failure<T, AecError>(AecError.Host(what, ex));
        }
    }

    private static Result<IReadOnlyList<EntitySets>, AecError> Scan(Database db, Transaction tx, IProgressSink progress) =>
        Host("list model space", () =>
            {
                var modelSpace = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
                var ids = new List<ObjectId>();
                foreach (ObjectId id in modelSpace)
                    ids.Add(id);
                return ids;
            })
            .Bind(ids =>
            {
                var batches = ids.Chunk(ScanBatch).ToList();
                var found = new List<EntitySets>();
                int done = 0;
                return Host("scan model space", () => progress.Each("Property sets", batches,
                        batch => $"entities {done + 1:N0}–{done + batch.Length:N0} of {ids.Count:N0}", batch =>
                        {
                            foreach (ObjectId id in batch)
                            {
                                if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity || entity.ExtensionDictionary.IsNull)
                                    continue;
                                ObjectIdCollection setIds = PropertyDataServices.GetPropertySets(entity);
                                if (setIds.Count == 0)
                                    continue;
                                found.Add(new EntitySets(
                                    new EntityKey(id.Handle.Value), Kind(entity),
                                    setIds.Cast<ObjectId>().Select(setId => ReadSet(tx, setId, entity)).ToList()));
                            }
                            done += batch.Length;
                        }))
                    .Map(() => (IReadOnlyList<EntitySets>)found);
            });

    // A set the AEC API cannot read is a fault in its place; the others still read.
    private static Result<SetOnEntity, AecError> ReadSet(Transaction tx, ObjectId setId, Entity entity) =>
        Host("read a property set", () => tx.GetObject(setId, OpenMode.ForRead))
            .Bind(opened => opened is PropertySet set
                ? Result.Success<PropertySet, AecError>(set)
                : Result.Failure<PropertySet, AecError>(AecError.Drawing($"{opened.Handle} is not a property set")))
            .Bind(set => Host($"read the definition of {set.PropertySetDefinitionName}", () =>
                    tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead))
                .Bind(opened => opened is PropertySetDefinition definition
                    ? Host($"read {set.PropertySetDefinitionName}", () => new SetOnEntity(set.PropertySetDefinitionName,
                        definition.Definitions.Cast<PropertyDefinition>()
                            .OrderBy(p => p.DisplayOrder)
                            .Select(p => ReadProperty(set, p, entity))
                            .ToList()))
                    : Result.Failure<SetOnEntity, AecError>(
                        AecError.Drawing($"{set.PropertySetDefinitionName} is not a property set definition"))));

    private static PropertyOnEntity ReadProperty(PropertySet set, PropertyDefinition property, Entity entity)
    {
        bool manual = !property.Automatic && s_manual.ContainsKey(property.DataType);
        Result<AecDataType, AecError> type = manual
            ? Result.Success<AecDataType, AecError>(s_manual[property.DataType].Type)
            : Result.Failure<AecDataType, AecError>(
                AecError.Unsupported(property.Automatic ? "automatic" : $"data type {property.DataType}"));
        return new PropertyOnEntity(property.Name, property.Description, type, ReadValue(set, property, entity, manual));
    }

    private static PropertyValue ReadValue(PropertySet set, PropertyDefinition property, Entity entity, bool manual) =>
        // The entity is the context an automatic property is worked out from. The API hands
        // null for a property the set stores no value for.
        Host($"read {property.Name}", () => set.GetAt(property.Id, entity) is object value
                ? Maybe.From(value)
                : Maybe<object>.None)
            .Bind(read => read.Match(
                value => Converted(property.Name, () => PropertyValue.Of(manual
                    ? s_manual[property.DataType].Read(value)
                    : new AecValue.Text(Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""))),
                () => Result.Success<PropertyValue, AecError>(PropertyValue.None)))
            .Match(value => value, error => PropertyValue.CannotRead(error.Message));

    private static string Kind(Entity entity)
    {
        string name = entity.GetRXClass().Name;
        return name.StartsWith("AcDb", StringComparison.Ordinal) ? name[4..] : name;
    }

    /// <summary>One call into Civil 3D: what it throws becomes a fault.</summary>
    private static Result<T, AecError> Host<T>(string what, Func<T> call)
    {
        try
        {
            return Result.Success<T, AecError>(call());
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return Result.Failure<T, AecError>(AecError.Host(what, ex));
        }
    }

    /// <summary>One call into Civil 3D that returns nothing: what it throws becomes a fault.</summary>
    private static UnitResult<AecError> Host(string what, Action call)
    {
        try
        {
            call();
            return UnitResult.Success<AecError>();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return UnitResult.Failure(AecError.Host(what, ex));
        }
    }

    // A value Convert cannot turn into the property's data type is a fault, not a crash.
    private static Result<PropertyValue, AecError> Converted(string propertyName, Func<PropertyValue> convert)
    {
        try
        {
            return Result.Success<PropertyValue, AecError>(convert());
        }
        catch (System.Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return Result.Failure<PropertyValue, AecError>(AecError.Value($"{propertyName}: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static Maybe<Document> Active() =>
        Application.DocumentManager.MdiActiveDocument is Document doc ? Maybe.From(doc) : Maybe<Document>.None;
}
