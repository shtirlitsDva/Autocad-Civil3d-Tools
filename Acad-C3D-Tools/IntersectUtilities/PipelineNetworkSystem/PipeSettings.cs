using IntersectUtilities.PipeScheduleV2;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipeSettingsCollection : IDictionary<string, PipeSettings>
    {
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
        public static PipeSettingsCollection Load(string settingsFileName)
        {
            string jsonString = System.IO.File.ReadAllText(settingsFileName);
            PipeSettingsCollection settings = 
                JsonSerializer.Deserialize<PipeSettingsCollection>(jsonString);
            return settings;
        }
        public IEnumerable<string> ListSettings() => _dictionary.Keys;
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
        public Dictionary<string, PipeSettingSystem> Settings = new Dictionary<string, PipeSettingSystem>();
        public PipeSettings() { }
        public PipeSettings(string name)
        {
            Name = name;
            foreach (IPipeType pt in PipeScheduleV2.PipeScheduleV2.GetPipeTypes())
            {
                Settings.Add(pt.Name, new PipeSettingSystem(pt));
            }
        }
        internal void UpdateSettings()
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
        public Dictionary<int, int> Settings = new Dictionary<int, int>();
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
