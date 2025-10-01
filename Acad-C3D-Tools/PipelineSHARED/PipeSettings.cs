using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipeScheduleV2;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;

using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.Forms.PipeSettingsWpf.Views;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipeSettingsCollection : IDictionary<string, PipeSettings>
    {
        //Common settings
        public static string SettingsLayerName = "0-PIPESETTINGS";
        private static string PipeSettingsFileName = "_PipeSettings.json";
        public static string GetSettingsFileNameWithPath()
        {
            Database localDb = Application.DocumentManager.MdiActiveDocument.Database;
            string dbFilenameWithPath = localDb.OriginalFileName;
            string path = Path.GetDirectoryName(dbFilenameWithPath);
            string dbFileName = Path.GetFileNameWithoutExtension(dbFilenameWithPath);
            string settingsFileName = Path.Combine(path, dbFileName + PipeSettingsFileName);
            return settingsFileName;
        }

        [JsonInclude]
        public Dictionary<string, PipeSettings> _dictionary;
        public PipeSettingsCollection()
        {
            _dictionary = new Dictionary<string, PipeSettings>();
            _dictionary.Add("Default", new PipeSettings("Default"));
        }
        internal void Save(string settingsFileName)
        {
            string jsonString = JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(settingsFileName, jsonString);
        }
        public static PipeSettingsCollection Load()
        {
            string f = GetSettingsFileNameWithPath();
            return Load(f);
        }
        public static PipeSettingsCollection Load(string settingsFileName)
        {
            if (string.IsNullOrEmpty(settingsFileName)) throw new Exception("Settings file name is empty!");
            if (File.Exists(settingsFileName))
            {
                string jsonString = System.IO.File.ReadAllText(settingsFileName);
                PipeSettingsCollection settings =
                    JsonSerializer.Deserialize<PipeSettingsCollection>(jsonString);

                return settings;
            }
            else
            {
                prdDbg("Settings file missing! Creating...");
                var defaultSettingsCollection = new PipeSettingsCollection();
                var defaultSettings = defaultSettingsCollection["Default"];
                defaultSettings.EditSettings();
                defaultSettingsCollection.Save(settingsFileName);
                prdDbg($"Created default settings file:\n\"{settingsFileName}\".");
                return defaultSettingsCollection;
            }
        }
        public static PipeSettingsCollection LoadWithValidation(Database db)
        {
            string f = GetSettingsFileNameWithPath();
            return LoadWithValidation(f, db);
        }
        public static PipeSettingsCollection LoadWithValidation(string settingsFileName, Database db)
        {
            if (string.IsNullOrEmpty(settingsFileName)) throw new Exception("Settings file name is empty!");

            if (File.Exists(settingsFileName))
            {
                string jsonString = System.IO.File.ReadAllText(settingsFileName);
                PipeSettingsCollection settings =
                    JsonSerializer.Deserialize<PipeSettingsCollection>(jsonString);

                var result = settings.ValidateSettings(db);

                if (result.valid == false)
                {
                    prdDbg("Settings are invalid!");
                    prdDbg(result.message);
                    settings.Save(settingsFileName);
                    throw new Exception("Settings are invalid!");
                }

                return settings;
            }
            else
            {
                prdDbg("Settings file missing! Creating...");
                var defaultSettingsCollection = new PipeSettingsCollection();
                var defaultSettings = defaultSettingsCollection["Default"];
                defaultSettings.EditSettings();
                defaultSettingsCollection.Save(settingsFileName);
                prdDbg($"Created default settings file:\n\"{settingsFileName}\".");
                return defaultSettingsCollection;
            }
        }
        public IEnumerable<string> ListSettings() => _dictionary.Keys;
        private bool _settingsMPolygonsLoaded = false;
        private Dictionary<Polyline, MPolygon> _settingsMPolygons = new();
        internal PipeSettings DetermineSettingsForPipe(Polyline pline)
        {
            if (!_settingsMPolygonsLoaded)
            {
                _settingsMPolygonsLoaded = true;

                if (pline.Database.TransactionManager.TopTransaction == null) throw new Exception("Transaction is null!");
                Database db = pline.Database;
                Transaction tx = pline.Database.TransactionManager.TopTransaction;

                var splines = db.ListOfType<Polyline>(tx)
                    .Where(x => x.Layer == SettingsLayerName).ToList();

                if (splines.Count == 0) return _dictionary["Default"];

                foreach (var spline in splines)
                {
                    
                    MPolygon mpg = new MPolygon();
                    mpg.AppendLoopFromBoundary(spline, true, Tolerance.Global.EqualPoint);
                    _settingsMPolygons.Add(spline, mpg);
                }
            }

            foreach (var kvp in _settingsMPolygons)
            {
                Point3d[] points =
                    [pline.StartPoint, pline.GetPointAtDist(pline.Length / 2), pline.EndPoint];

                bool[] bools =
                    points.Select(x => kvp.Value.IsPointInsideMPolygon(
                        x, Tolerance.Global.EqualPoint).Count == 1).ToArray();

                if (bools.All(x => x == true)) return _dictionary[kvp.Key.Handle.ToString()];

                //Detect mixed bools, which means a point is inside and outside the MPolygon
                //This is not allowed, so throw an exception
                if (bools.All(x => x == false) == false && bools.All(x => x == true) == false)
                    throw new Exception(
                        $"Pipe settings polyline {kvp.Key.Handle} crosses pipe polyline {pline.Handle}!\n" +
                        $"This is not allowed! Pipe polylines may not cross pipe settings polylines!");

                if (bools.All(x => x == false)) continue;
            }

            return _dictionary["Default"];
        }
        internal double GetSettingsLength(Polyline pline)
        {
            PipeSettings pss = DetermineSettingsForPipe(pline);
            var system = "PipeType" + 
                PipeScheduleV2.PipeScheduleV2.GetSystemString(
                PipeScheduleV2.PipeScheduleV2.GetPipeSystem(pline));
            var typeSettings = pss.Settings[system];
            var type = PipeScheduleV2.PipeScheduleV2.GetPipeType(pline, true);
            var sizeSettings = typeSettings.Settings[type];
            var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pline);
            var length = sizeSettings.Settings[dn];
            return length;
        }

        internal (bool valid, string message) ValidateSettings(Database localDb)
        {
            if (localDb == null) return (false, "Database is null!");
            if (localDb.TransactionManager.TopTransaction == null) return (false, "Transaction is null!");
            var tx = localDb.TransactionManager.TopTransaction;
            var plines = localDb.HashSetOfType<Polyline>(tx);

            var settingPlines = plines.Where(p => p.Layer == SettingsLayerName);

            foreach (var pline in settingPlines)
                if (pline.Closed == false)
                    return (false, $"Polyline {pline.Handle} is not closed!");

            if (ContainsKey("Default") == false) return (false, "Default settings missing!");

            var plinesHandles = settingPlines.Select(p => p.Handle.ToString()).ToHashSet();
            var settingNames = ListSettings().Where(x => x != "Default").ToHashSet();

            if (plinesHandles.SetEquals(settingNames) == false)
            {
                var missingSettings = plinesHandles.Except(settingNames).ToList();
                if (missingSettings.Count > 0)
                    return (false, $"Settings missing for polylines: {string.Join(", ", missingSettings)}\n" +
                        $"Either delete these polylines or use PSADD to create settings for them.");

                var extraSettings = settingNames.Except(plinesHandles).ToList();
                if (extraSettings.Count > 0)
                {
                    prdDbg($"Extra settings found for plines: \n{string.Join(", ", extraSettings)}\n" +
                        $"But no polylines with these handles found.\n" +
                        $"Settings deleted!");

                    foreach (var setting in extraSettings) Remove(setting);
                    Save(GetSettingsFileNameWithPath());
                    //Validate settings again
                    return ValidateSettings(localDb);
                }
            }

            return (true, "Settings are valid.");
        }

        #region Interface Members
        public PipeSettings this[string key] { get => _dictionary[key]; set => _dictionary[key] = value; }
        public ICollection<string> Keys => _dictionary.Keys;
        public ICollection<PipeSettings> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;
        public void Add(string key, PipeSettings value) => _dictionary.Add(key, value);
        public void Add(KeyValuePair<string, PipeSettings> item) => _dictionary.Add(item.Key, item.Value);
        public void Clear() => _dictionary.Clear();
        public bool Contains(KeyValuePair<string, PipeSettings> item) => _dictionary.ContainsKey(item.Key) && _dictionary[item.Key].Equals(item.Value);
        public bool ContainsKey(string key) => _dictionary.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, PipeSettings>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, PipeSettings>>)_dictionary).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, PipeSettings>> GetEnumerator() => _dictionary.GetEnumerator();
        public bool Remove(string key) => _dictionary.Remove(key);
        public bool Remove(KeyValuePair<string, PipeSettings> item) => _dictionary.Remove(item.Key);
        public bool TryGetValue(string key, out PipeSettings value) => _dictionary.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion
    }
    public class PipeSettings
    {
        /// <summary>
        /// 'Default' or value of ents' Handle.
        /// The handle corresponds to the entity that the setting is applied to.
        /// </summary>
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public Dictionary<string, PipeSettingSystem> Settings = 
            new Dictionary<string, PipeSettingSystem>();
        public PipeSettings() { }
        public PipeSettings(string name)
        {
            Name = name;
            foreach (IPipeType pt in PipeScheduleV2.PipeScheduleV2.GetPipeTypes())
            {
                Settings.Add(pt.Name, new PipeSettingSystem(pt));
            }
        }
        internal void EditSettings()
        {
            //PipeSettingsForm form = new PipeSettingsForm();
            //form.CreatePipeSettingsGrid(this);
            //form.ShowDialog();
            //form.Close();

            PipeSettingsWindow wnd = new PipeSettingsWindow();
            wnd.LoadPipeSettinngs(this);
            wnd.ShowDialog();
        }
    }
    public class PipeSettingSystem
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public PipeSystemEnum PipeTypeSystem { get; set; }
        [JsonInclude]
        public Dictionary<PipeTypeEnum, PipeSettingType> Settings = new Dictionary<PipeTypeEnum, PipeSettingType>();
        public PipeSettingSystem() { }
        public PipeSettingSystem(IPipeType pt)
        {
            Name = pt.Name;
            PipeTypeSystem = pt.System;
            foreach (PipeTypeEnum type in pt.GetAvailableTypes())
            {
                Settings.Add(type, new PipeSettingType(type, pt));
            }
        }
    }
    public class PipeSettingType
    {
        [JsonInclude]
        public PipeTypeEnum Name { get; set; }
        [JsonInclude]
        public Dictionary<int, double> Settings = new();
        public PipeSettingType() { }
        public PipeSettingType(PipeTypeEnum type, IPipeType pt)
        {
            Name = type;
            foreach (int size in pt.ListAllDnsForPipeType(type))
            {
                Settings.Add(size, pt.GetDefaultLengthForDnAndType(size, type));
            }
        }
    }
}
