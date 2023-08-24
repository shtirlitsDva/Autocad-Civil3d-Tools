using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LER2
{
    public class Ler2MergeValidator
    {

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
