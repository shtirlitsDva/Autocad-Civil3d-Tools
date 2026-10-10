using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

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
/// NorsynDrawingTools' NorsynPropertySetsSHARED): the active drawing's property sets read
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

    public Result<IReadOnlyList<EntitySets>> ScanModelSpace(IProgressSink progress) =>
        Active().Match(
            doc =>
            {
                using DocumentLock _ = doc.LockDocument();
                using Transaction tx = doc.Database.TransactionManager.StartTransaction();
                Result<IReadOnlyList<EntitySets>> result = Guard(() => Scan(doc.Database, tx, progress));
                tx.Commit();
                return result;
            },
            () => Result<IReadOnlyList<EntitySets>>.Failure("No drawing is open."));

    private static Result<IReadOnlyList<EntitySets>> Scan(Database db, Transaction tx, IProgressSink progress)
    {
        var modelSpace = (BlockTableRecord)tx.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        var ids = new List<ObjectId>();
        foreach (ObjectId id in modelSpace)
            ids.Add(id);
        var batches = ids.Chunk(ScanBatch).ToList();
        var found = new List<EntitySets>();
        int done = 0;
        progress.Each("Property sets", batches, batch => $"entities {done + 1:N0}–{done + batch.Length:N0} of {ids.Count:N0}", batch =>
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
        });
        return Result<IReadOnlyList<EntitySets>>.Success(found);
    }

    // A set the AEC API cannot read is a fault in its place; the others still read.
    private static Result<SetOnEntity> ReadSet(Transaction tx, ObjectId setId, Entity entity)
    {
        try
        {
            var set = (PropertySet)tx.GetObject(setId, OpenMode.ForRead);
            var definition = (PropertySetDefinition)tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead);
            IReadOnlyList<PropertyOnEntity> properties = definition.Definitions.Cast<PropertyDefinition>()
                .OrderBy(p => p.DisplayOrder)
                .Select(p => ReadProperty(set, p, entity))
                .ToList();
            return Result<SetOnEntity>.Success(new SetOnEntity(set.PropertySetDefinitionName, properties));
        }
        catch (System.Exception ex)
        {
            return Result<SetOnEntity>.Failure($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static PropertyOnEntity ReadProperty(PropertySet set, PropertyDefinition property, Entity entity)
    {
        bool manual = !property.Automatic && s_manual.ContainsKey(property.DataType);
        Result<AecDataType> type = manual
            ? Result<AecDataType>.Success(s_manual[property.DataType].Type)
            : Result<AecDataType>.Failure(property.Automatic ? "automatic" : $"data type {property.DataType}");
        return new PropertyOnEntity(property.Name, property.Description, type, ReadValue(set, property, entity, manual));
    }

    private static PropertyValue ReadValue(PropertySet set, PropertyDefinition property, Entity entity, bool manual)
    {
        try
        {
            // The entity is the context an automatic property is worked out from.
            object? value = set.GetAt(property.Id, entity);
            if (value is null) return PropertyValue.None;
            return PropertyValue.Of(manual
                ? s_manual[property.DataType].Read(value)
                : new AecValue.Text(Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""));
        }
        catch (System.Exception ex)
        {
            return PropertyValue.CannotRead($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string Kind(Entity entity)
    {
        string name = entity.GetRXClass().Name;
        return name.StartsWith("AcDb", StringComparison.Ordinal) ? name[4..] : name;
    }

    // The stop of a scan passes through (the caller catches it); any other throw is a fault.
    private static Result<T> Guard<T>(Func<Result<T>> body)
    {
        try
        {
            return body();
        }
        catch (OperationStoppedException)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            return Result<T>.Failure($"Property sets, scan model space: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Option<Document> Active()
    {
        Document? doc = Application.DocumentManager.MdiActiveDocument;
        return doc is null ? Option<Document>.Nothing : Option<Document>.Of(doc);
    }
}
