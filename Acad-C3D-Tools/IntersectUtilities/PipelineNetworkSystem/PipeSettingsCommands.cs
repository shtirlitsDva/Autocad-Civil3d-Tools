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
        /// <command>PIPESETTINGSCREATE, PSCREATE</command>
        /// <summary>
        /// Creates and initializes default pipe settings.
        /// The settings are used for setting default values for standard pipe lengths.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("PSCREATE")]
        [CommandMethod("PIPESETTINGSCREATE")]
        public void pipesettingscreate()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = PipeSettingsCollection.SettingsLayerName;

            #region Check or create layer
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    if (!lt.Has(settingsLayerName))
                    {
                        prdDbg("Settings layer missing! Creating...");
                        localDb.CheckOrCreateLayer(settingsLayerName, 2, false);
                        prdDbg($"Created layer \"0-PIPESETTINGS\".");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
            #endregion

            #region Manage pipe settings
            //First check if settings exist
            //If not, create them and edit immidiately

            var settingsFileName =
                PipeSettingsCollection.GetSettingsFileNameWithPath();

            //Check if settings file exists
            if (!File.Exists(settingsFileName))
            {
                prdDbg("Settings file missing! Creating...");
                var defaultSettingsCollection = new PipeSettingsCollection();
                var defaultSettings = defaultSettingsCollection["Default"];
                defaultSettings.EditSettings();
                defaultSettingsCollection.Save(settingsFileName);

                prdDbg($"Created default settings file:\n\"{settingsFileName}\".");
                prdDbg("Exiting...");
                return;
            }

            //Load settings
            var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

            prdDbg($"Following pipe settings detected: \n" +
                $"{string.Join(", ", settingsCollection.ListSettings())}");

            bool editDefault =
                StringGridFormCaller.YesNo(
                    "Edit default settings?: ");

            if (editDefault)
            {
                var settings = settingsCollection["Default"];
                settings.EditSettings();
            }

            settingsCollection.Save(settingsFileName);
            #endregion
        }
        /// <command>PIPESETTINGSADD, PSADD</command>
        /// <summary>
        /// Adds a new pipe setting using a selected polyline.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("PSADD")]
        [CommandMethod("PIPESETTINGSADD")]
        public void pipesettingsadd()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = PipeSettingsCollection.SettingsLayerName;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Manage pipe settings
                    //First check if settings exist
                    //If not, create them and edit immidiately

                    var settingsFileName =
                        PipeSettingsCollection.GetSettingsFileNameWithPath();

                    //Check if settings file exists
                    if (!File.Exists(settingsFileName))
                    {
                        prdDbg("Settings file missing! Creating...");
                        var defaultSettingsCollection = new PipeSettingsCollection();
                        
                        var defaultSettings = defaultSettingsCollection["Default"];
                        defaultSettings.EditSettings();
                        
                        defaultSettingsCollection.Save(settingsFileName);

                        prdDbg($"Created default settings file:\n\"{settingsFileName}\".");
                    }

                    //Load settings
                    var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

                    prdDbg($"Following pipe settings detected: \n" +
                        $"{string.Join(", ", settingsCollection.ListSettings())}");

                    Oid oid = Interaction.GetEntity(
                        "Select polyline marking area for special pipe settings to add: ",
                        typeof(Polyline), true);
                    if (oid == Oid.Null) { prdDbg("Abort..."); tx.Abort(); return; }
                    var pline = oid.Go<Polyline>(tx);
                    if (pline == null) { prdDbg("Abort..."); tx.Abort(); return; }

                    if (pline.Layer != settingsLayerName)
                    {
                        prdDbg("Selected polyline is not on the settings layer!");
                        tx.Abort();
                        return;
                    }

                    //The handle of the polyline shall be the name of the settings
                    string psName = pline.Handle.ToString();

                    #region If settings already exist
                    //Check if collection already has the settings
                    if (settingsCollection.ContainsKey(psName))
                    {
                        prdDbg($"Settings for \"{psName}\" already exists!");

                        bool editExisting = StringGridFormCaller.YesNo(
                            "Edit existing settings?: ");

                        if (editExisting)
                        {
                            var es = settingsCollection[psName];
                            es.EditSettings();
                        }
                        else
                        {
                            prdDbg("Exiting...");
                            tx.Abort();
                            return;
                        }

                        tx.Abort();
                        return;
                    } 
                    #endregion

                    //Instantiate new pipe settings
                    var newPipeSettings = new PipeSettings(pline.Handle.ToString());
                    newPipeSettings.EditSettings();
                    settingsCollection.Add(psName, newPipeSettings);
                    settingsCollection.Save(settingsFileName);
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
        /// <command>PIPESETTINGSREMOVE, PSREMOVE</command>
        /// <summary>
        /// Removes a pipe setting using a selected polyline.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("PSREMOVE")]
        [CommandMethod("PIPESETTINGSREMOVE")]
        public void pipesettingsremove()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = PipeSettingsCollection.SettingsLayerName;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var settingsFileName =
                        PipeSettingsCollection.GetSettingsFileNameWithPath();

                    //Check if settings file exists
                    if (!File.Exists(settingsFileName))
                    {
                        prdDbg("Settings file does not exist!"); tx.Abort(); return;
                    }

                    //Load settings
                    var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

                    prdDbg($"Following pipe settings detected: \n" +
                        $"{string.Join(", ", settingsCollection.ListSettings())}");

                    Oid oid = Interaction.GetEntity(
                        "Select polyline marking area for special pipe settings to remove: ",
                        typeof(Polyline), true);
                    if (oid == Oid.Null) { prdDbg("Abort..."); tx.Abort(); return; }
                    var pline = oid.Go<Polyline>(tx);
                    if (pline == null) { prdDbg("Abort..."); tx.Abort(); return; }

                    if (pline.Layer != settingsLayerName)
                    {
                        prdDbg("Selected polyline is not on the settings layer!");
                        tx.Abort();
                        return;
                    }

                    //The handle of the polyline shall be the name of the settings
                    string psName = pline.Handle.ToString();

                    #region If settings already exist
                    //Check if collection already has the settings
                    if (settingsCollection.ContainsKey(psName))
                    {
                        settingsCollection.Remove(psName);
                        settingsCollection.Save(settingsFileName);

                        prdDbg(
                            $"Settings for \"{psName}\" removed!\n" +
                            $"Remember to delete polyline or add new settings!");

                        tx.Abort();
                        return;
                    }
                    else
                    {
                        prdDbg($"Settings for \"{psName}\" does not exist!");
                        //Fall through
                    }
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
        /// <command>PIPESETTINGSEDIT, PSEDIT</command>
        /// <summary>
        /// Prompts user to select a for which to edit settings.
        /// Adds new settings if missing.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("PSEDIT")]
        [CommandMethod("PIPESETTINGSEDIT")]
        public void pipesettingsedit()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = PipeSettingsCollection.SettingsLayerName;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var settingsFileName =
                        PipeSettingsCollection.GetSettingsFileNameWithPath();

                    //Check if settings file exists
                    if (!File.Exists(settingsFileName))
                    {
                        prdDbg("Settings file does not exist! Run PSCREATE."); tx.Abort(); return;
                    }

                    //Load settings
                    var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

                    prdDbg($"Following pipe settings detected: \n" +
                        $"{string.Join(", ", settingsCollection.ListSettings())}");

                    var editDefault = StringGridFormCaller.YesNo(
                        "Edit DEFAULT settings?: ");

                    if (editDefault)
                    {
                        Oid oid = Interaction.GetEntity(
                            "Select polyline marking area for special pipe settings to edit the settings: ",
                        typeof(Polyline), true);
                        if (oid == Oid.Null) { prdDbg("Abort..."); tx.Abort(); return; }
                        var pline = oid.Go<Polyline>(tx);
                        if (pline == null) { prdDbg("Abort..."); tx.Abort(); return; }

                        if (pline.Layer != settingsLayerName)
                        {
                            prdDbg("Selected polyline is not on the settings layer!");
                            tx.Abort();
                            return;
                        }
                        //The handle of the polyline shall be the name of the settings
                        string psName = pline.Handle.ToString();

                        //Check if collection already has the settings
                        if (settingsCollection.ContainsKey(psName))
                        {
                            var settings = settingsCollection[psName];
                            settings.EditSettings();
                            settingsCollection.Save(settingsFileName);

                            prdDbg(
                                $"Settings for \"{psName}\" edited and saved!"
                                );

                            tx.Abort();
                            return;
                        }
                        else
                        {
                            prdDbg($"Settings for \"{psName}\" does not exist! Creating...");

                            var newPipeSettings = new PipeSettings(pline.Handle.ToString());
                            newPipeSettings.EditSettings();
                            settingsCollection.Add(psName, newPipeSettings);
                            settingsCollection.Save(settingsFileName);

                            tx.Abort();
                            return;
                        }
                    }
                    else
                    {
                        var settings = settingsCollection["Default"];
                        settings.EditSettings();
                        settingsCollection.Save(settingsFileName);

                        prdDbg(
                            $"Settings for \"Default\" edited and saved!"
                            );

                        tx.Abort();
                        return;
                    }
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
        /// <command>PIPESETTINGSVALIDATE, PSVALIDATE</command>
        /// <summary>
        /// Validates pipe setting to make sure that the number of polylines
        /// correspond to the number of pipe settings.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("PSVALIDATE")]
        [CommandMethod("PIPESETTINGSVALIDATE")]
        public void pipesettingsvalidate()
        {
            prdDbg("Dette skal køres i FJV Fremtid!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string settingsLayerName = PipeSettingsCollection.SettingsLayerName;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var settingsFileName =
                        PipeSettingsCollection.GetSettingsFileNameWithPath();

                    //Check if settings file exists
                    if (!File.Exists(settingsFileName))
                    {
                        prdDbg("Settings file does not exist!"); tx.Abort(); return;
                    }

                    //Load settings
                    var settingsCollection = PipeSettingsCollection.Load(settingsFileName);

                    prdDbg($"Following pipe settings detected: \n" +
                        $"{string.Join(", ", settingsCollection.ListSettings())}");

                    var result = settingsCollection.ValidateSettings(localDb);

                    prdDbg(
                        $"Settings are valid: {result.Item1}\n" +
                        $"Message: \n{result.Item2}");
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