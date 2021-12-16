using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;

namespace IntersectUtilities
{
    public static class GisData
    {
        public static void creategisdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\nRemember to freeze unneeded linework!");
            try
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                        #region Populate Block GIS data
                        #region OD Table definition
                        string tableNameKomponenter = "Components";

                        string[] columnNames = new string[12]
                               {"BlockName",
                                "Type",
                                "Rotation",
                                "System",
                                "DN1",
                                "DN2",
                                "Serie",
                                "Width",
                                "Height",
                                "OffsetX",
                                "OffsetY",
                                "Flip"
                               };
                        string[] columnDescrs = new string[12]
                            {"Name of source block",
                             "Type of the component",
                             "Rotation of the symbol",
                             "Twin or single",
                             "Main run dimension",
                             "Secondary run dimension",
                             "Insulation series of pipes",
                             "Width of symbol",
                             "Height of symbol",
                             "X offset from Origo to CL",
                             "Y offset from Origo to CL",
                             "Describes block's mirror state"
                            };
                        DataType[] dataTypes = new DataType[12]
                            {DataType.Character,
                             DataType.Character,
                             DataType.Real,
                             DataType.Character,
                             DataType.Integer,
                             DataType.Integer,
                             DataType.Character,
                             DataType.Real,
                             DataType.Real,
                             DataType.Real,
                             DataType.Real,
                             DataType.Character
                            };
                        Func<BlockReference, System.Data.DataTable, MapValue>[] populateKomponentData =
                            new Func<BlockReference, System.Data.DataTable, MapValue>[12]
                        {
                            ODDataReader.Komponenter.ReadBlockName,
                            ODDataReader.Komponenter.ReadComponentType,
                            ODDataReader.Komponenter.ReadBlockRotation,
                            ODDataReader.Komponenter.ReadComponentSystem,
                            ODDataReader.Komponenter.ReadComponentDN1,
                            ODDataReader.Komponenter.ReadComponentDN2,
                            ODDataReader.Komponenter.ReadComponentSeries,
                            ODDataReader.Komponenter.ReadComponentWidth,
                            ODDataReader.Komponenter.ReadComponentHeight,
                            ODDataReader.Komponenter.ReadComponentOffsetX,
                            ODDataReader.Komponenter.ReadComponentOffsetY,
                            ODDataReader.Komponenter.ReadComponentFlipState
                        };

                        CheckOrCreateTable(tables, tableNameKomponenter, "Komponentdata", columnNames, columnDescrs, dataTypes);

                        #endregion

                        HashSet<BlockReference> brSet = localDb.HashSetOfType<BlockReference>(tx);
                        foreach (BlockReference br in brSet)
                        {
                            if (br.IsDynamicBlock) continue;

                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                for (int i = 0; i < columnNames.Length; i++)
                                {
                                    if (DoesRecordExist(tables, br.ObjectId, tableNameKomponenter, columnNames[i]))
                                    {
                                        UpdateODRecord(tables, tableNameKomponenter, columnNames[i],
                                            br.ObjectId, populateKomponentData[i].Invoke(br, fjvKomponenter));
                                    }
                                    else AddODRecord(tables, tableNameKomponenter, columnNames[i],
                                            br.ObjectId, populateKomponentData[i].Invoke(br, fjvKomponenter));
                                }
                            }
                            else prdDbg($"Non-dynamic block {br.Name} does not exist in FJV Komponenter.csv");
                        }
                        #endregion

                        #region Populate dynamic block data

                        fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                        #region Dynamic OD Reader
                        Func<BlockReference, System.Data.DataTable, MapValue>[] populateDynamicKomponentData =
                            new Func<BlockReference, System.Data.DataTable, MapValue>[12]
                        {
                            ODDataReader.DynKomponenter.ReadBlockName,
                            ODDataReader.DynKomponenter.ReadComponentType,
                            ODDataReader.DynKomponenter.ReadBlockRotation,
                            ODDataReader.DynKomponenter.ReadComponentSystem,
                            ODDataReader.DynKomponenter.ReadComponentDN1,
                            ODDataReader.DynKomponenter.ReadComponentDN2,
                            ODDataReader.DynKomponenter.ReadComponentSeries,
                            ODDataReader.DynKomponenter.ReadComponentWidth,
                            ODDataReader.DynKomponenter.ReadComponentHeight,
                            ODDataReader.DynKomponenter.ReadComponentOffsetX,
                            ODDataReader.DynKomponenter.ReadComponentOffsetY,
                            ODDataReader.DynKomponenter.ReadComponentFlipState
                        };
                        #endregion

                        foreach (BlockReference br in brSet)
                        {
                            if (br.IsDynamicBlock)
                            {
                                string realName = ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
                                if (ReadStringParameterFromDataTable(realName, fjvKomponenter, "Navn", 0) != null)
                                {
                                    #region Properties list
                                    //prdDbg("-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-");

                                    //ed.WriteMessage("\nDynamic properties for \"{0}\"\n", realName);

                                    //DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;

                                    //foreach (DynamicBlockReferenceProperty prop in pc)
                                    //{
                                    //    // Start with the property name, type and description
                                    //    ed.WriteMessage("\nProperty: \"{0}\" : {1}",
                                    //      prop.PropertyName,
                                    //      prop.UnitsType);

                                    //    if (prop.Description != "")
                                    //        ed.WriteMessage("\n  Description: {0}",
                                    //          prop.Description);

                                    //    // Is it read-only?
                                    //    if (prop.ReadOnly)
                                    //        ed.WriteMessage(" (Read Only)");

                                    //    // Get the allowed values, if it's constrained
                                    //    bool first = true;

                                    //    foreach (object value in prop.GetAllowedValues())
                                    //    {
                                    //        ed.WriteMessage((first ? "\n  Allowed values: [" : ", "));
                                    //        ed.WriteMessage("\"{0}\"", value);
                                    //        first = false;
                                    //    }

                                    //    if (!first) ed.WriteMessage("]");

                                    //    // And finally the current value
                                    //    ed.WriteMessage("\n  Current value: \"{0}\"\n",
                                    //      prop.Value);
                                    //} 
                                    #endregion

                                    for (int i = 0; i < columnNames.Length; i++)
                                    {
                                        if (DoesRecordExist(tables, br.ObjectId, tableNameKomponenter, columnNames[i]))
                                        {
                                            UpdateODRecord(tables, tableNameKomponenter, columnNames[i],
                                                br.ObjectId, populateDynamicKomponentData[i].Invoke(br, fjvKomponenter));
                                        }
                                        else AddODRecord(tables, tableNameKomponenter, columnNames[i],
                                                br.ObjectId, populateDynamicKomponentData[i].Invoke(br, fjvKomponenter));
                                    }
                                }
                                else prdDbg($"Dynamic block {realName} does not exist in FJV Dynamiske Komponenter.csv");
                            }
                        }
                        #endregion

                        #region Populate (p)lines and arc GIS data
                        #region OD Table definition
                        string tableNamePipes = "Pipes";

                        string[] columnNamesPipes = new string[3]
                            {"DN",
                            //"Length",
                            "System",
                            "Serie"};
                        string[] columnDescrsPipes = new string[3]
                            {"Dimension of pipe",
                            //"Length of pipe",
                            "System of pipe",
                            "Series of the pipe" };
                        DataType[] dataTypesPipes = new DataType[3]
                            {DataType.Integer,
                            //DataType.Real,
                            DataType.Character,
                            DataType.Character };
                        Func<Entity, MapValue>[] pipeData =
                            new Func<Entity, MapValue>[3]
                        {
                            ODDataReader.Pipes.ReadPipeDimension,
                            //ODDataReader.Pipes.ReadPipeLength,
                            ODDataReader.Pipes.ReadPipeSystem,
                            ODDataReader.Pipes.ReadPipeSeries
                        };

                        CheckOrCreateTable(tables, tableNamePipes, "Rørdata", columnNamesPipes, columnDescrsPipes, dataTypesPipes);

                        #endregion
                        HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                        HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                        HashSet<Arc> arcs = localDb.HashSetOfType<Arc>(tx, true);
                        HashSet<Entity> ents = new HashSet<Entity>(plines.Count + lines.Count + arcs.Count);
                        ents.UnionWith(plines.Cast<Entity>());
                        ents.UnionWith(lines.Cast<Entity>());
                        ents.UnionWith(arcs.Cast<Entity>());

                        //Filter for known non pipe layers
                        ents = ents.Where(x => !DataQa.Gis.ContainsForbiddenValues(x.Layer)).ToHashSet();

                        foreach (Entity ent in ents)
                        {
                            for (int i = 0; i < columnNamesPipes.Length; i++)
                            {
                                if (DoesRecordExist(tables, ent.ObjectId, tableNamePipes, columnNamesPipes[i]))
                                {
                                    UpdateODRecord(tables, tableNamePipes, columnNamesPipes[i],
                                        ent.ObjectId, pipeData[i].Invoke(ent));
                                }
                                else AddODRecord(tables, tableNamePipes, columnNamesPipes[i],
                                        ent.ObjectId, pipeData[i].Invoke(ent));
                            }
                        }
                        #endregion
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        ed.WriteMessage(ex.Message);
                        throw;
                    }

                    tx.Commit();
                }

            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        private static void GetAllXrefNames(GraphNode i_root, List<string> list, Transaction i_Tx)
        {
            for (int o = 0; o < i_root.NumOut; o++)
            {
                XrefGraphNode child = i_root.Out(o) as XrefGraphNode;
                if (child.XrefStatus == XrefStatus.Resolved)
                {
                    BlockTableRecord bl = i_Tx.GetObject(child.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                    list.Add(child.Database.Filename);
                    // Name of the Xref (found name)
                    // You can find the original path too:
                    //if (bl.IsFromExternalReference == true)
                    // i_ed.WriteMessage("\n" + i_indent + "Xref path name: "
                    //                      + bl.PathName);
                    GetAllXrefNames(child, list, i_Tx);
                }
            }
        }
    }
}
