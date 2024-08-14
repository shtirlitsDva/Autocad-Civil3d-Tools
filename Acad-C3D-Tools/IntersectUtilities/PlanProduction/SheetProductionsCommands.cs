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
using IntersectUtilities.DataManager;
using Microsoft.Win32;
using System.Security.Cryptography;
using IntersectUtilities.PlanProduction;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("VIEWFRAMESCREATEALLDRAWINGS")]
        public void viewframescreatealldrawings()
        {
            prdDbg("!!!REMEMBER TO SET CORRECT SHORTCUT FOLDER BEFORE RUNNING THIS COMMAND!!!");
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

            //Create the view frames
            bool isValidCreation = false;
            DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);
            if (isValidCreation != true)
            {
                prdDbg("DataShortcutManager failed to be created!");
                return;
            }

            try
            {
                int publishedCount = sm.GetPublishedItemsCount();
                if (publishedCount == 0)
                {
                    prdDbg("No published items found!");
                    return;
                }

                ViewFramesCollection vfc = new ViewFramesCollection(pathToFolder);

                for (int i = 0; i < publishedCount; i++)
                {
                    DataShortcuts.DataShortcutManager.PublishedItem item = sm.GetPublishedItemAt(i);

                    if (item.DSEntityType == DataShortcutEntityType.Alignment)
                    {
                        string newFileName = Path.Combine(pathToFolder, item.Name) + "_VF.dwg";

                        vfc.Add(new ViewFrameDrawing(item.Name, newFileName));

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
                                    prdDbg("");
                                    prdDbg("Alignment detected! Creating reference to shortcut...");
                                    ObjectIdCollection ids = sm.CreateReference(i, alDb);
                                    CivilDocument civilDoc = CivilDocument.GetCivilDocument(alDb);
                                    foreach (Oid oid in ids)
                                    {
                                        Alignment al = oid.Go<Alignment>(alTx, OpenMode.ForWrite);
                                        try
                                        {
                                            al.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                                            Oid labelSetId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                                            al.ImportLabelSet(labelSetId);
                                        }
                                        catch (System.Exception)
                                        {
                                            prdDbg("Styles for alignment or labels are missing!");
                                            throw;
                                        }
                                    }

                                    //Create reference to profiles
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

                                        for (int j = 0; j < publishedCount; j++)
                                        {
                                            DataShortcuts.DataShortcutManager.PublishedItem candidate = sm.GetPublishedItemAt(j);
                                            if (candidate.DSEntityType == DataShortcutEntityType.Profile)
                                            {
                                                if (candidate.Name.StartsWith(number))
                                                {
                                                    prdDbg(candidate.Name);
                                                    sm.CreateReference(j, alDb);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        prdDbg($"Name {item.Name} does not contain pipeline number!");
                                        continue;
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    alTx.Abort();
                                    prdDbg(ex);
                                    throw;
                                }
                                alTx.Commit();
                            }
                            alDb.SaveAs(newFileName, true, DwgVersion.Newest, null);
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

        [CommandMethod("VIEWFRAMESCREATEALLVIEWFRAMES")]
        public void viewframescreateallviewframes()
        {
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
        }
    }
}