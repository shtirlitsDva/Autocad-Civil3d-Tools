using Autodesk.AutoCAD.ApplicationServices;

using utils = IntersectUtilities.UtilsCommon.Utils;

using Dreambuild.AutoCAD;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DimensioneringV2.AutoCAD
{
    internal static class HydraulicSettingsSerializer
    {
        public static void Save(string path, HydraulicSettings settings)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(path, json);
        }
        public static void Save(Document doc, HydraulicSettings settings)
        {
            if (doc == null) return;
            using (var docLock = doc.LockDocument())
            {
                var db = doc.Database;
                var store = FlexDataStoreExtensions.FlexDataStore(db);
                store.SetObject("HydraulicSettings", settings);
            }
        }
        public static HydraulicSettings Load(Document doc)
        {
            if (doc == null) return new HydraulicSettings();
            using (var docLock = doc.LockDocument())
            {
                var db = doc.Database;
                var store = FlexDataStoreExtensions.FlexDataStore(db);
                if (store.Has("HydraulicSettings"))
                {
                    try
                    {
                        return store.GetObject<HydraulicSettings>("HydraulicSettings");
                    }
                    catch (System.Exception ex)
                    {
                        Utils.prtDbg("Error loading HydraulicSettings from FlexDataStore.");
                        Utils.prtDbg(ex);
                        throw;
                    }
                }
                Utils.prtDbg($"Store does not have requested key: \"HydraulicSettings\"");
                return new HydraulicSettings();
            }
        }
    }
}
