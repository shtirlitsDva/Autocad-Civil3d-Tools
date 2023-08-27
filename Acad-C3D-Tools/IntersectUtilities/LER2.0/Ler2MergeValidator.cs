using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LER2
{
    public class Ler2MergeValidator
    {
        private Dictionary<string, MergeRules> _mergeRules = new Dictionary<string, MergeRules>();
        public void LoadRule(string ler2Type, string basePath)
        {
            string csvPath = Path.Combine(basePath, $"{ler2Type}.csv");
            if (!File.Exists(csvPath))
                throw new Exception($"Merge rule for Ler2Type {ler2Type} does not exist!");

            MergeRules rules = new MergeRules();

            foreach (string line in File.ReadAllLines(csvPath).Skip(1))
            {
                if (line.Trim() == "") continue;
                var parts = line.Split(';');
                if (parts.Length != 2) throw new Exception($"Invalid rule: {line} in {csvPath}!");
                MergeRuleType rule;
                if (Enum.TryParse(parts[1], out rule))
                {
                    rules.PropertyRules.Add(parts[0], rule);
                }
                else
                {
                    throw new Exception($"Invalid rule: {line} in {csvPath}!");
                }
            }

            _mergeRules.Add(ler2Type, rules);
        }
        public (HashSet<HashSet<SerializablePolyline3d>> Unchanged,
        HashSet<HashSet<SerializablePolyline3d>> Changed) Validate(
            HashSet<HashSet<SerializablePolyline3d>> plines,
            StringBuilder log)
        {
            if (_mergeRules.Count == 0)
                throw new Exception("No merge rules loaded!");

            HashSet<HashSet<SerializablePolyline3d>> unchanged = new HashSet<HashSet<SerializablePolyline3d>>();
            HashSet<HashSet<SerializablePolyline3d>> changed = new HashSet<HashSet<SerializablePolyline3d>>();

            foreach (var group in plines)
            {
                Dictionary<string, HashSet<SerializablePolyline3d>> partitionedGroups = 
                    new Dictionary<string, HashSet<SerializablePolyline3d>>();

                foreach (var obj in group)
                {
                    StringBuilder keyBuilder = new StringBuilder();
                    string ler2Type = obj.Properties["Ler2Type"].ToString();
                    MergeRules mergeRules = _mergeRules[ler2Type];

                    foreach (var rule in mergeRules.PropertyRules)
                    {
                        if (rule.Value == MergeRuleType.MustMatch)
                        {
                            object value;
                            obj.Properties.TryGetValue(rule.Key, out value);
                            keyBuilder.Append(Convert.ToString(value));
                            keyBuilder.Append("|");
                        }
                    }

                    string key = keyBuilder.ToString();
                    if (!partitionedGroups.ContainsKey(key))
                    {
                        partitionedGroups[key] = new HashSet<SerializablePolyline3d>();
                    }

                    partitionedGroups[key].Add(obj);
                }

                if (partitionedGroups.Count == 1 && partitionedGroups.First().Value.Count == group.Count)
                {
                    // The group has not changed, add it to 'unchanged'
                    unchanged.Add(group);
                }
                else
                {
                    // The group has changed, add its sub-groups to 'changed'
                    foreach (var newGroup in partitionedGroups.Values)
                    {
                        if (newGroup.Count > 1)
                        {
                            changed.Add(newGroup);
                        }
                        else
                        {
                            log.AppendLine($"Group with key {newGroup.First().Properties["Ler2Type"]} discarded due to single member.");
                        }
                    }
                }
            }

            return (unchanged, changed);
        }
    }

    public enum MergeRuleType
    {
        MustMatch,
        Ignore
    }

    public class MergeRules
    {
        public Dictionary<string, MergeRuleType> PropertyRules { get; set; }
            = new Dictionary<string, MergeRuleType>();
    }
}
