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

                        try
                        {
                            al.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                            al.ImportLabelSet("STD 20-5");
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Styles for alignment or labels are missing!");
                            throw;
                        }
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

        [CommandMethod("CREATEALIGNMENTDRAWINGS")]
        public void createalignmentdrawings()
        {
            DocumentCollection docCol = Application.DocumentManager;
            //Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

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

                /////////////////////////////////////////////////////////////////////////////////////////////
                string pathToFolderToSave = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\" +
                                            @"01 Intern\02 Tegninger\01 Autocad - xxx\Etape 1.2\ViewFrames\";
                /////////////////////////////////////////////////////////////////////////////////////////////

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
                        string newFileName = pathToFolderToSave + item.Name + ".dwg";
                        using (Database alDb = new Database(false, true))
                        {
                            alDb.ReadDwgFile(
                                @"X:\AutoCAD DRI - 01 Civil 3D\Templates\Alignment_til_viewframes.dwt",
                                System.IO.FileShare.Read, false, string.Empty);
                            alDb.SaveAs(newFileName, true, DwgVersion.Newest, null);
                        }
                        using (Database alDb = new Database(false, true))
                        {
                            using (Transaction alTx = alDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    alDb.ReadDwgFile(newFileName, FileOpenMode.OpenForReadAndWriteNoShare,
                                        false, string.Empty);
                                    prdDbg("Alignment detected! Creating reference to shortcut...");
                                    ObjectIdCollection ids = sm.CreateReference(i, alDb);

                                    //HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                                    editor.WriteMessage($"\nNr. of alignments: {ids.Count}");
                                    CivilDocument civilDoc = CivilDocument.GetCivilDocument(alDb);
                                    foreach (oid Oid in ids)
                                    {
                                        Alignment al = Oid.Go<Alignment>(alTx, OpenMode.ForWrite);
                                        try
                                        {
                                            al.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                                            oid labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                                            al.ImportLabelSet(labelSetId);
                                        }
                                        catch (System.Exception)
                                        {
                                            prdDbg("Styles for alignment or labels are missing!");
                                            throw;
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    alTx.Abort();
                                    throw new System.Exception(ex.Message);
                                }
                                alTx.Commit();
                            }
                            alDb.SaveAs(newFileName, true, DwgVersion.Newest, null);
                        }
                    }
                }

                System.Windows.Forms.Application.DoEvents();

                #endregion
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.Message);
                return;
            }
            finally
            {
                sm.Dispose();
            }
        }

        [CommandMethod("LISTNUMBEROFVIEWFRAMES")]
        public void listnumberofviewframes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region List number of Vframes
                    HashSet<ViewFrame> vfs = localDb.HashSetOfType<ViewFrame>(tx);
                    prdDbg($"Number_of_VFs: {{{vfs.Count}}}");
                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                finally
                {
                }
                tx.Abort();
            }
        }
    }
}
