using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using static IntersectUtilities.Utils;
using IntersectUtilities.UtilsCommon;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Table = Autodesk.Gis.Map.ObjectData.Table;
using System.Collections.ObjectModel;
using Autodesk.AutoCAD.Runtime;

namespace IntersectUtilities.ODDataConverter
{
    public static class ODDataConverter
    {
        //Loops through all ODTables in document and creates corresponding PropertySetDefinitions
        public static void oddatacreatepropertysetsdefs()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Reference ODTables
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            StringCollection tableNames = tables.GetTableNames();

            foreach (string name in tableNames)
            {
                Table curTable = tables[name];

                try
                {
                    // (1) create prop set def
                    PropertySetDefinition propSetDef = new PropertySetDefinition();
                    propSetDef.SetToStandard(localDb);
                    propSetDef.SubSetDatabaseDefaults(localDb);
                    // alternatively, you can use dictionary's NewEntry
                    // Dim dictPropSetDef = New DictionaryPropertySetDefinitions(db)
                    // Dim propSetDef As PropertySetDefinition =
                    // dictPropSetDef.NewEntry()

                    // General tab
                    propSetDef.Description = name;
                    // Applies To tab
                    // apply to objects or styles. True if style, False if objects
                    bool isStyle = false;
                    var appliedTo = new StringCollection();
                    //appliedTo.Add("AcDbLine");
                    //appliedTo.Add("AcDbSpline");
                    //appliedTo.Add("AcDbPolyline");
                    //appliedTo.Add("AcDb3dPolyline");
                    appliedTo.Add(RXClass.GetClass(typeof(BlockReference)).Name);
                    propSetDef.SetAppliesToFilter(appliedTo, isStyle);

                    FieldDefinitions defs = curTable.FieldDefinitions;
                    int defsCount = defs.Count;
                    for (int i = 0; i < defsCount; i++)
                    {
                        FieldDefinition curFd = defs[i];
                        string fieldDefName = curFd.Name;
                        string fieldDefDescription = curFd.Description;
                        DataType fieldType = curFd.Type;


                        // Definition tab
                        // (2) we can add a set of property definitions. 
                        // We first make a container to hold them.
                        // This is the main part. A property set definition can contain
                        // a set of property definition.
                        // (2.1) let's first add manual property.
                        // Here we use text type
                        var propDefManual = new PropertyDefinition();
                        propDefManual.SetToStandard(localDb);
                        propDefManual.SubSetDatabaseDefaults(localDb);
                        propDefManual.Name = fieldDefName;
                        propDefManual.Description = fieldDefDescription;
                        propDefManual.DataType = GetCorrespondingPropertyDataType(fieldType);
                        propDefManual.DefaultData = ConvertDefaultValue(curFd);
                        // add to the prop set def
                        propSetDef.Definitions.Add(propDefManual);
                    }

                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        //check if prop set already exists
                        var dictPropSetDef = new DictionaryPropertySetDefinitions(localDb);
                        if (dictPropSetDef.Has(name, tx))
                        {
                            ed.WriteMessage("\nError - the property set defintion already exists: " + name);
                            tx.Abort();
                            continue;
                        }

                        dictPropSetDef.AddNewRecord(name, propSetDef);
                        tx.AddNewlyCreatedDBObject(propSetDef, true);
                        tx.Commit();
                    }

                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\nError while creating Property Set definitions: " + ex.ToString());
                    return;
                }
            }

            ed.WriteMessage("\nFinished!");
        }

        public static void attachpropertysetstoobjects()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Reference ODTables
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
            DictionaryPropertySetDefinitions dictPropSetDef = new DictionaryPropertySetDefinitions(localDb);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //I need to work with 3d polylines
                //Change here to add other types of objects
                HashSet<Entity> ents = new HashSet<Entity>();
                //ents.UnionWith(localDb.HashSetOfType<Line>(tx));
                //ents.UnionWith(localDb.HashSetOfType<Spline>(tx));
                //ents.UnionWith(localDb.HashSetOfType<Polyline>(tx));
                //ents.UnionWith(localDb.HashSetOfType<Polyline3d>(tx));
                ents.UnionWith(localDb.HashSetOfType<BlockReference>(tx));

                foreach (Entity ent in ents)
                {
                    using (Records records =
                        tables.GetObjectRecords(0, ent.Id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                    {
                        int recordsCount = records.Count;
                        for (int i = 0; i < recordsCount; i++)
                        {
                            Record record = records[i];
                            int recordCount = record.Count;
                            string tableName = record.TableName;

                            //Attach correct property set to entity
                            Oid dictId = dictPropSetDef.GetAt(tableName);
                            if (dictId == Oid.Null)
                            {
                                ed.WriteMessage($"\nODTable {tableName} does not have corresponding propertyset!" +
                                    $"Create propertysets first.");
                                tx.Abort();
                                return;
                            }
                            //Add property set to the object
                            ent.CheckOrOpenForWrite();
                            PropertyDataServices.AddPropertySet(ent, dictId);
                        }
                    }
                }
                tx.Commit();
                prdDbg("Finished!");
            }
        }

        public static void populatepropertysetswithoddata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Reference ODTables
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
            DictionaryPropertySetDefinitions dictPropSetDef = new DictionaryPropertySetDefinitions(localDb);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //I need to work with 3d polylines
                    //Change here to add other types of objects
                    HashSet<Entity> ents = new HashSet<Entity>();
                    //ents.UnionWith(localDb.HashSetOfType<Line>(tx));
                    //ents.UnionWith(localDb.HashSetOfType<Spline>(tx));
                    //ents.UnionWith(localDb.HashSetOfType<Polyline>(tx));
                    //ents.UnionWith(localDb.HashSetOfType<Polyline3d>(tx));
                    ents.UnionWith(localDb.HashSetOfType<BlockReference>(tx));

                    foreach (Entity ent in ents)
                    {
                        ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
                        List<PropertySet> pss = new List<PropertySet>();
                        foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(tx, OpenMode.ForWrite));

                        using (Records records =
                            tables.GetObjectRecords(0, ent.Id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                        {
                            int recordsCount = records.Count;
                            for (int i = 0; i < recordsCount; i++)
                            {
                                Record record = records[i];
                                string tableName = record.TableName;
                                //Specific to my implementation
                                if (tableName == "IdRecord") continue;

                                PropertySet propertySet = pss.Find(x => x.PropertySetDefinitionName == tableName);
                                if (propertySet == null)
                                {
                                    tx.Abort();
                                    ed.WriteMessage($"\nPropertySet with the name {tableName} could not be found!");
                                    return;
                                }

                                Table table = tables[tableName];
                                FieldDefinitions fDefs = table.FieldDefinitions;
                                int fieldsCount = fDefs.Count;

                                for (int j = 0; j < fieldsCount; j++)
                                {
                                    FieldDefinition fDef = fDefs[j];
                                    string fieldName = fDef.Name;

                                    int columnIndex = fDefs.GetColumnIndex(fieldName);
                                    MapValue value = record[columnIndex];

                                    int psIdCurrent = propertySet.PropertyNameToId(fieldName);
                                    propertySet.SetAt(psIdCurrent, GetMapValueData(value));
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage(ex.ToString());
                    return;
                }
                tx.Commit();
                prdDbg("Finished!");
            }
        }

        private static Autodesk.Aec.PropertyData.DataType GetCorrespondingPropertyDataType(DataType fieldDefType)
        {
            switch (fieldDefType)
            {
                case DataType.UnknownType:
                    return Autodesk.Aec.PropertyData.DataType.Text;
                case DataType.Integer:
                    return Autodesk.Aec.PropertyData.DataType.Integer;
                case DataType.Real:
                    return Autodesk.Aec.PropertyData.DataType.Real;
                case DataType.Character:
                    return Autodesk.Aec.PropertyData.DataType.Text;
                case DataType.Point:
                    return Autodesk.Aec.PropertyData.DataType.Text;
                default:
                    throw new System.Exception("DataType Default case not implemented!");
            }
        }
        private static object GetMapValueData(MapValue mapValue)
        {
            switch (mapValue.Type)
            {
                case DataType.UnknownType:
                    return mapValue.StrValue;
                case DataType.Integer:
                    return mapValue.Int32Value;
                case DataType.Real:
                    return mapValue.DoubleValue;
                case DataType.Character:
                    return mapValue.StrValue;
                case DataType.Point:
                    return mapValue.Point.ToString();
                default:
                    throw new System.Exception("DataType Default case not implemented!");
            }
        }
        /// <summary>
        /// Basic default values. Need to implement reading from field definition.
        /// </summary>
        private static object ConvertDefaultValue(FieldDefinition fieldDefinition)
        {
            switch (fieldDefinition.Type)
            {
                case DataType.UnknownType:
                    return "";
                case DataType.Integer:
                    return 0;
                case DataType.Real:
                    return 0;
                case DataType.Character:
                    return "";
                case DataType.Point:
                    return "";
                default:
                    throw new System.Exception($"Default DataType {fieldDefinition.Type.ToString()} not implemented!");
            }
        }
        public static void testing()
        {
            #region ListFilterItems
            //StringCollection names = dictPropSetDef.NamesInUse;
            //using (Transaction tx = localDb.TransactionManager.StartTransaction())
            //{
            //    foreach (string name in names)
            //    {
            //        Oid dictId = dictPropSetDef.GetAt(name);
            //        PropertySetDefinition propDef = dictId.Go<PropertySetDefinition>(tx);
            //        foreach (string filter in propDef.AppliesToFilter)
            //        {
            //            prdDbg(filter);
            //        }
            //    }

            //    tx.Abort();
            //} 
            #endregion
        }
    }
}
