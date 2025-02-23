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
        public IEnumerable<string> ListSettings() => _dictionary.Keys;
        internal PipeSettings DetermineSettingsForPipe(Polyline pline)
        {
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
            PipeSettingsForm form = new PipeSettingsForm();
            form.CreatePipeSettingsGrid(this);
            form.ShowDialog();
            form.Close();
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
