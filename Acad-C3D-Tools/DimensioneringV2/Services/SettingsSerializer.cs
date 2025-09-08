using Autodesk.AutoCAD.ApplicationServices;

using Dreambuild.AutoCAD;

using System;
using System.IO;
using System.Text.Json;

namespace DimensioneringV2.Services
{
    internal class SettingsSerializer<T> where T : class, new()
    {
        private static readonly string name = typeof(T).Name;        
        public static void Save(Document doc, T settings)
        {
            if (doc == null) return;
            using (var docLock = doc.LockDocument())
            {
                var db = doc.Database;
                FlexDataStore store = FlexDataStoreExtensions.FlexDataStore(db);
                store.SetObject(name, settings);
            }
        }
        public static T Load(Document doc)
        {
            if (doc == null) return new T();
            using (var docLock = doc.LockDocument())
            {
                var db = doc.Database;
                var store = FlexDataStoreExtensions.FlexDataStore(db);
                if (store.Has(name))
                {
                    try
                    {
                        return store.GetObject<T>(name);
                    }
                    catch (System.Exception ex)
                    {
                        Utils.prtDbg($"Error loading {name} from FlexDataStore.");
                        Utils.prtDbg(ex);
                        return new T();
                    }
                }
                Utils.prtDbg($"Store does not have requested key: \"{name}\"");
                return new T();
            }
        }
    }
}