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
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;
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
using IntersectUtilities.PipelineNetworkSystem;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("PSTS")]
        [CommandMethod("PIPESETTINGS")]
        public void pipesettings()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = "0-PIPESETTINGS";
            string pipeSettingsFileName = "_PipeSettings.json";

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Check or create layer
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    if (!lt.Has(settingsLayerName))
                    {
                        prdDbg("Settings layer missing! Creating...");
                        localDb.CheckOrCreateLayer(settingsLayerName);
                        prdDbg($"Created layer \"0-PIPESETTINGS\".");
                        prdDbg(
                            $"Run PIPESETTINGS to create general settings " +
                            $"and/or draw closed polylines in the settings layer " +
                            $"to create areas for different length settings.");
                        prdDbg("Exiting...");
                        tx.Commit();
                        return;
                    }
                    #endregion

                    #region Manage pipe settings
                    //First check if settings exist
                    //If not, create them and edit immidiately
                    string dbFilenameWithPath = localDb.OriginalFileName;
                    string path = Path.GetDirectoryName(dbFilenameWithPath);
                    string dbFileName = Path.GetFileNameWithoutExtension(dbFilenameWithPath);
                    string settingsFileName = Path.Combine(path, dbFileName + pipeSettingsFileName);

                    //Check if settings file exists
                    if (!File.Exists(settingsFileName))
                    {
                        prdDbg("Settings file missing! Creating...");
                        var defaultSettingsCollection = new PipeSettingsCollection();
                        var defaultSettings = defaultSettingsCollection["Default"];
                        defaultSettings.UpdateSettings();
                        defaultSettingsCollection.Save(settingsFileName);

                        prdDbg($"Created default settings file:\n\"{settingsFileName}\".");
                        prdDbg("Exiting...");
                        tx.Abort();
                        return;
                    }

                    //Load settings
                    var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

                    prdDbg($"Following pipe settings detected: \n" +
                        $"{string.Join(", ", settingsCollection.ListSettings())}");

                    string settingsToEdit = 
                        StringGridFormCaller.Call(
                            settingsCollection.ListSettings(), "Choose settings to edit: ");

                    if (settingsToEdit.IsNoE()) { tx.Abort(); return; }

                    var settings = settingsCollection[settingsToEdit];
                    settings.UpdateSettings();
                    settingsCollection.Save(settingsFileName);

                    //HashSet<Polyline> settingsPlines = localDb
                    //    .HashSetOfType<Polyline>(tx)
                    //    .Where(p => p.Layer == settingsLayerName)
                    //    .ToHashSet();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
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