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
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
//using MoreLinq;
using System.Text;
using IntersectUtilities;
using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;

namespace SheetCreationAutomation
{
    public class SheetCreationAutomation : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nLoaded Sheet Creation Automation commands.");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("CREATEREFERENCETOALLALIGNMENTSHORTCUTS")]
        public void createreferencetoallalignmentshortcuts()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                bool isValidCreation = false;
                DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);

                if (isValidCreation != true)
                {
                    prdDbg("DataShortcutManager failed to be created!");
                    return;
                }

                try
                {
                    #region
                    int publishedCount = sm.GetPublishedItemsCount();
                    prdDbg($"publishedCount = {publishedCount}");

                    for (int i = 0; i < publishedCount; i++)
                    {
                        prdDbg("");
                        DataShortcuts.DataShortcutManager.PublishedItem item =
                            sm.GetPublishedItemAt(i);
                        prdDbg($"Name: {item.Name}");
                        prdDbg($"Description: {item.Description}");
                        prdDbg($"DSEntityType: {item.DSEntityType.ToString()}");

                        if (item.DSEntityType == DataShortcutEntityType.Alignment)
                        {
                            prdDbg("Alignment detected! Creating reference to shortcut...");
                            sm.CreateReference(i, localDb);
                        }
                    }

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.ImportLabelSet("STD 20-5");
                    }

                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                finally
                {
                    sm.Dispose();
                }
                tx.Commit();
            }
        }

    }
}
