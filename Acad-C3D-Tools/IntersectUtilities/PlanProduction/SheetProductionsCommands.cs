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
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using System.Text.Json;
using IntersectUtilities.DynamicBlocks;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Plane = Autodesk.AutoCAD.Geometry.Plane;
using NetTopologySuite.Triangulate;
using IntersectUtilities.LongitudinalProfiles;
using NetTopologySuite.Algorithm;
using IntersectUtilities.UtilsCommon.DataManager;
using Microsoft.Win32;
using IntersectUtilities.PlanProduction;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>VIEWFRAMESCREATEALLDRAWINGS</command>
        /// <summary>
        /// Creates all drawings that is needed for the view frames for each alignment.
        /// The command uses shortcuts to read the alignments and profiles,
        /// and thus C3D must be set to correct shortcuts folder.
        /// </summary>
        /// <category>Profile Views</category>
        [CommandMethod("VIEWFRAMESCREATEALLDRAWINGS")]
        public void viewframescreatealldrawings()
        {
            prdDbg("!!!REMEMBER TO SET CORRECT SHORTCUT FOLDER BEFORE RUNNING THIS COMMAND!!!\n");
            var reply = Interaction.GetKeywords("Have you remembered to set the correct shortcut folder?",
                ["No", "Yes"]);
            if (reply == "No") return;

            string pathToFolder;

            OpenFolderDialog ofd = new OpenFolderDialog()
            {
                Title = "Select the folder where to create view frames: ",
            };
            if (ofd.ShowDialog() == true)
            {
                pathToFolder = ofd.FolderName;
            }
            else return;

            //Collect information about the state of the folder
            bool isValidCreation = false;
            DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);
            if (isValidCreation != true)
            {
                prdDbg("DataShortcutManager failed to be created!");
                return;
            }

            var alignments = new List<(int idx, string alname, string number, string fileName, bool dwgExists)> { };
            var profiles = new List<(int idx, string alname)> { };

            int publishedCount = sm.GetPublishedItemsCount();
            if (publishedCount == 0)
            {
                prdDbg("No published items found!");
                return;
            }

            #region Read the state of the folder
            //Loop to determine the alignments
            for (int i = 0; i < publishedCount; i++)
            {
                DataShortcuts.DataShortcutManager.PublishedItem item = sm.GetPublishedItemAt(i);

                if (item.DSEntityType == DataShortcutEntityType.Alignment)
                {
                    string newFileName = Path.Combine(pathToFolder, item.Name) + "_VF.dwg";

                    //Determine the pipeline number
                    Regex regexOld = new Regex(@"(?<number>\d{2,3}\s)");
                    Regex regexNew = new Regex(@"(?<number>\d{2,3})");

                    string number = "";
                    if (regexNew.IsMatch(item.Name))
                        number = regexNew.Match(item.Name).Groups["number"].Value;
                    else if (regexOld.IsMatch(item.Name))
                        number = regexOld.Match(item.Name).Groups["number"].Value;

                    if (!number.IsNoE())
                    {
                        prdDbg($"Strækning navn: {item.Name} -> Number: {number}");
                        number = number.Trim();
                    }
                    else
                    {
                        prdDbg($"Name {item.Name} does not contain pipeline number!");
                        continue;
                    }

                    if (File.Exists(newFileName)) alignments.Add((i, item.Name, number, item.Name + "_VF.dwg", true));
                    else alignments.Add((i, item.Name, number, item.Name + "_VF.dwg", false));
                }
            }
            //Loop to determine the profiles
            #region Gather info on profiles
            for (int j = 0; j < publishedCount; j++)
            {
                DataShortcuts.DataShortcutManager.PublishedItem candidate = sm.GetPublishedItemAt(j);
                if (candidate.DSEntityType == DataShortcutEntityType.Profile)
                {
                    if (alignments.Any(x => candidate.Name.StartsWith(x.number)))
                    {
                        prdDbg(candidate.Name);
                        profiles.Add((j, alignments.Where(
                            x => candidate.Name.StartsWith(x.number))
                            .First().alname));
                    }
                }
            }
            #endregion
            #endregion

            #region Determine to overwrite or append
            bool overWrite = true;
            if (alignments.Count != 0 && alignments.Any(x => x.dwgExists))
            {
                prdDbg("\nExisting ViewFrame drawings detected! ");
                var reply3 = Interaction.GetKeywords(
                    "Do you want to: ",
                    ["Cancel", "Append", "Overwrite"]);
                if (reply3.IsNoE() || reply3 == "Cancel") { prdDbg("*Cancel*"); return; }
                if (reply3 == "Append") overWrite = false;
            }
            #endregion

            try
            {
                ViewFramesCollection vfc;
                if (File.Exists(Path.Combine(pathToFolder, "ViewFrameCollection.json")))
                {
                    vfc = ViewFramesCollection.Load(Path.Combine(pathToFolder, "ViewFrameCollection.json"));
                    if (overWrite)
                    {
                        vfc.Clear();
                    }
                }
                else
                {
                    vfc = new ViewFramesCollection(pathToFolder);
                }

                foreach (var al in alignments)
                {
                    if (!overWrite && al.dwgExists)
                    {
                        //Check if vfc contains the drawing reference and continue
                        if (!vfc.ViewFrames.Any(x => x.FileName == al.fileName)) continue;
                        else
                        {
                            ViewFrameDrawing vfd = new ViewFrameDrawing(al.alname, al.fileName);
                            vfc.Add(vfd);
                        }
                        continue;
                    }

                    //Create the drawing
                    var item = sm.GetPublishedItemAt(al.idx);

                    if (item.DSEntityType == DataShortcutEntityType.Alignment)
                    {
                        string newFileName = Path.Combine(pathToFolder, item.Name) + "_VF.dwg";

                        using (Database alDb = new Database(false, true))
                        {
                            alDb.ReadDwgFile(
                                @"X:\AutoCAD DRI - SETUP\Templates\NS_ViewFrame.dwt",
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
                                    prdDbg("");
                                    prdDbg("Alignment detected! Creating reference to shortcut...");
                                    ObjectIdCollection ids = sm.CreateReference(al.idx, alDb);
                                    CivilDocument civilDoc = CivilDocument.GetCivilDocument(alDb);
                                    foreach (Oid oid in ids)
                                    {
                                        Alignment alignment = oid.Go<Alignment>(alTx, OpenMode.ForWrite);
                                        try
                                        {
                                            alignment.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                                            Oid labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                                            alignment.ImportLabelSet(labelSetId);
                                        }
                                        catch (System.Exception)
                                        {
                                            prdDbg("Styles for alignment or labels are missing!");
                                            throw;
                                        }
                                    }

                                    //Create reference to profiles
                                    foreach (var prof in profiles.Where(x => x.alname == al.alname))
                                    {
                                        var candidate = sm.GetPublishedItemAt(prof.idx);
                                        if (candidate.DSEntityType == DataShortcutEntityType.Profile) sm.CreateReference(prof.idx, alDb);
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
                            vfc.Add(new ViewFrameDrawing(al.alname, al.fileName));
                        }
                    }
                }

                vfc.Save();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                throw;
            }
        }

#if DEBUG
        //[CommandMethod("VIEWFRAMESCREATEALLVIEWFRAMES")]
        public void viewframescreateallviewframes()
        {
            #region Ask for VFC and dwt template
            System.Windows.Forms.OpenFileDialog ofd = new()
            {
                Title = "Select the ViewFrameCollection.json: ",
            };

            string pathToCollection;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pathToCollection = ofd.FileName;
            }
            else return;

            ViewFramesCollection vfc = ViewFramesCollection.Load(pathToCollection);

            ofd = new()
            {
                Title = "Select the template for sheets: ",
            };

            string pathToTemplate;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pathToTemplate = ofd.FileName;
            }
            else return;

            vfc.Template = pathToTemplate;
            #endregion

            #region Determine the dimensions of the viewframe
            using (Database db = new Database(false, true))
            {
                using (Transaction tx = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        db.ReadDwgFile(pathToTemplate, FileOpenMode.OpenForReadAndWriteNoShare,
                            false, string.Empty);


                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        throw new System.Exception(ex.Message);
                    }
                    tx.Commit();
                }
                #endregion
            }
        }
#endif
    }
}