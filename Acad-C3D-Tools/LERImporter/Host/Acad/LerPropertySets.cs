using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;

// CSharpFunctionalExtensions has an Entity too (a DDD base class).
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

using CSharpFunctionalExtensions;

using IntersectUtilities.LerHatchLayers;
using IntersectUtilities;

using LERImporter.PropertySets;

// AecDataType switches name every member and have no discard arm (see AecValues.cs).
#pragma warning disable CS8524

namespace LERImporter.Host.Acad;

/// <summary>
/// LER property sets on Civil 3D, through the AEC API. Its BricsCAD twin
/// (Host\Brx\LerPropertySets.cs) writes the same objects without that API.
/// </summary>
internal static class LerPropertySets
{
    /// <summary>Adds the definition to the database unless one of that name is there.</summary>
    public static UnitResult<AecError> Define(Database db, LerSetDef def) => Host($"define {def.Name}", () =>
    {
        using (Transaction tx = db.TransactionManager.StartTransaction())
        {
            var dictPropSetDef = new DictionaryPropertySetDefinitions(db);
            if (dictPropSetDef.Has(def.Name, tx))
            {
                tx.Abort();
                return;
            }
            PropertySetDefinition propSetDef = CreatePropertySetDefinition(db, def);
            dictPropSetDef.AddNewRecord(def.Name, propSetDef);
            tx.AddNewlyCreatedDBObject(propSetDef, true);
            tx.Commit();
        }
    });

    /// <summary>
    /// Attaches the definition's set to the entity, with the GML values, the
    /// graveforespørgsel's bemærkning and its LER number.
    /// </summary>
    public static UnitResult<AecError> Attach(
        Database db, Entity ent, string setName, Dictionary<string, object> psData,
        string? bemaerkning, string? lerNummer) => Host($"attach {setName} to {ent.Handle}", () =>
    {
        PropertySetManager.AttachNonDefinedPropertySet(db, ent, setName);
        PropertySetManager.PopulateNonDefinedPropertySet(db, ent, setName, psData);
        PropertySetManager.WriteNonDefinedPropertySetString(ent, setName, "GmlBemærkning", bemaerkning);
        PropertySetManager.WriteNonDefinedPropertySetString(ent, setName, "LerNummer", lerNummer);
    });

    public static LerHatchLayerResult<IReadOnlyList<LerHatchLayerSet>> ReadHatchLayerSets(Entity entity, Transaction tx) =>
        LerHatchLayerService.ReadSets(entity, tx);

    private static PropertySetDefinition CreatePropertySetDefinition(Database db, LerSetDef def)
    {
        var propSetDef = new PropertySetDefinition();
        propSetDef.SetToStandard(db);
        propSetDef.SubSetDatabaseDefaults(db);
        propSetDef.Description = def.Description;
        var appliedTo = new StringCollection();
        foreach (string className in LerSetDef.AppliesTo) appliedTo.Add(className);
        propSetDef.SetAppliesToFilter(appliedTo, false);

        foreach (AecPropertyDef property in def.Properties)
        {
            var propDef = new PropertyDefinition();
            propDef.SetToStandard(db);
            propDef.SubSetDatabaseDefaults(db);
            propDef.Name = property.Name;
            propDef.Description = property.Description;
            propDef.DataType = property.Type switch
            {
                AecDataType.Integer => Autodesk.Aec.PropertyData.DataType.Integer,
                AecDataType.Real => Autodesk.Aec.PropertyData.DataType.Real,
                AecDataType.Text => Autodesk.Aec.PropertyData.DataType.Text,
                AecDataType.TrueFalse => Autodesk.Aec.PropertyData.DataType.TrueFalse,
            };
            propDef.DefaultData = AecValue.Default(property.Type).Match<object>(
                integer: i => i, real: d => d, text: s => s, trueFalse: b => b);
            propSetDef.Definitions.Add(propDef);
        }
        return propSetDef;
    }

    /// <summary>The AEC-API boundary: what Civil 3D throws becomes a fault.</summary>
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
}
