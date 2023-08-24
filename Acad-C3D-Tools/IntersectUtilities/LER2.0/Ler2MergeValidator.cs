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
        public HashSet<HashSet<SerializablePolyline3d>> Validate(
            HashSet<HashSet<SerializablePolyline3d>> plines,
            StringBuilder log)
        {
            HashSet<HashSet<SerializablePolyline3d>> result = 
                new HashSet<HashSet<SerializablePolyline3d>>();

            foreach (var group in plines)
            {
                bool canMerge = true;

                // Compare all objects with the first one in the subcollection
                var robj = group.First();
                string ler2Type = robj.Properties["Ler2Type"].ToString();
                MergeRules mergeRules = _mergeRules[ler2Type];
                SerializablePolyline3d[] pl3ds = group.ToArray();
                for (int i = 1; i < pl3ds.Length; i++)
                {
                    foreach (var rule in mergeRules.PropertyRules)
                    {
                        var propertyName = rule.Key;
                        var mergeRule = rule.Value;

                        if (mergeRule == MergeRuleType.MustMatch)
                        {
                            object referenceValue;
                            object compareValue;

                            robj.TryGetValue(propertyName, out referenceValue);
                            subCollection[i].TryGetValue(propertyName, out compareValue);

                            if (!object.Equals(referenceValue, compareValue))
                            {
                                canMerge = false;
                                log.AppendLine($"Skipping merge: Property '{propertyName}' does not match.");
                                break;
                            }
                        }
                    }

                    if (!canMerge) break;
                }

                if (canMerge)
                {
                    // Perform the merge here
                }
                else
                {
                    // Log the reason for skipping
                    Console.WriteLine(log.ToString());
                }
            }
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
