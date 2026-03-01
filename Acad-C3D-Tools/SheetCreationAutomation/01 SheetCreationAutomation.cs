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
using System.Text.RegularExpressions;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using SheetCreationAutomation.UI;
using static IntersectUtilities.UtilsCommon.Utils;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using Microsoft.Win32;

[assembly: CommandClass(typeof(SheetCreationAutomation.NoCommands))]

namespace SheetCreationAutomation
{
    public class Commands : IExtensionApplication
    {
        private static SheetAutomationPaletteSet? _paletteSet;

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

        /// <command>CREATEREFERENCETOALLALIGNMENTSHORTCUTS</command>
        /// <summary>
        /// Creates references to all published Alignment data shortcuts in the current drawing and
        /// applies styles. Enumerates published items via DataShortcutManager, creates references for
        /// items of type Alignment, then assigns the alignment style "FJV TRACÉ SHOW" and imports the
        /// label set "STD 20-5". Intended to prepare the model with referenced alignments before view
        /// frame and sheet creation.
        /// </summary>
        /// <category>Sheet Production</category>
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

        /// <command>LISTNUMBEROFVIEWFRAMES</command>
        /// <summary>
        /// Counts Civil 3D ViewFrames in the current drawing and writes the count to the command line.
        /// </summary>
        /// <category>Sheet Production</category>
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
                    prdDbg($"Number of VFs: {{{vfs.Count}}}");

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
                }
                tx.Abort();
            }
        }
        
        /// <command>CREATEREFERENCETOPROFILES</command>
        /// <summary>
        /// Creates references to published Profile data shortcuts. Detects a pipeline/segment number
        /// from the first alignment’s name (for logging), initializes DataShortcutManager, iterates
        /// published items, and creates references for items of type Profile. Intended to bring required
        /// longitudinal profiles into the drawing prior to sheet creation workflows.
        /// </summary>
        /// <category>Sheet Production</category>
        [CommandMethod("CREATEREFERENCETOPROFILES")]
        public void createreferencetoprofiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Determine the pipeline number
                Alignment al = localDb.ListOfType<Alignment>(tx).FirstOrDefault();
                if (al == null) { prdDbg("No alignment found in drawing!"); tx.Abort(); return; }

                Regex regex = new Regex(@"(?<number>\d{2,3}?\s)");

                string strNumber = "";
                if (regex.IsMatch(al.Name))
                {
                    Match match = regex.Match(al.Name);
                    strNumber = match.Groups["number"].Value;
                    prdDbg($"Strækning nr: {strNumber}");
                }
                else
                {
                    prdDbg("Name of the alignment does not contain pipeline number!");
                    tx.Abort();
                    return;
                }

                if (strNumber.IsNoE()) { tx.Abort(); return; }

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
                        //prdDbg("");
                        DataShortcuts.DataShortcutManager.PublishedItem item =
                            sm.GetPublishedItemAt(i);
                        //prdDbg($"Name: {item.Name}");
                        //prdDbg($"Description: {item.Description}");
                        //prdDbg($"DSEntityType: {item.DSEntityType.ToString()}");

                        if (item.DSEntityType == DataShortcutEntityType.Profile)
                        {
                            //if (item.Name.StartsWith(strNumber))
                            //{
                            //    sm.CreateReference(i, localDb);
                            //}
                            prdDbg(item.Name);
                            sm.CreateReference(i, localDb);
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

        /// <command>SCAUI</command>
        /// <summary>
        /// Opens the Sheet Creation Automation palette for native View Frame automation.
        /// </summary>
        /// <category>Sheet Production</category>
        [CommandMethod("SCAUI", CommandFlags.Session)]
        public void openSheetCreationAutomationUi()
        {
            try
            {
                AcContext.Current = System.Threading.SynchronizationContext.Current;
                _paletteSet ??= new SheetAutomationPaletteSet();
                _paletteSet.Visible = true;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }
    }

    public class NoCommands { }
}
