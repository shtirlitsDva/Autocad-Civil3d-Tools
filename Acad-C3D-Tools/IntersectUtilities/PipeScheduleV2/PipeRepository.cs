using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeRepository //: IPipeRepository
    {
        private Dictionary<string, DataTable> _pipeDictionary = new Dictionary<string, DataTable>();

        public void Initialize(string type, DataTable pipeType)
        {
            _pipeDictionary[type] = pipeType;
        }

        //public PipeType GetPipeType(string layerName)
        //{
        //    string[] layerInfo = layerName.Split('_');
        //    if (layerInfo.Length != 3) return null;

        //    string type = layerInfo[0];
        //    string system = layerInfo[1];
        //    int dn = int.Parse(layerInfo[2]);

        //    List<PipeType> candidates;
        //    if (!_pipeDictionary.TryGetValue(type, out candidates)) return null;

        //    return candidates.Find(p => p.System == system && p.DN == dn);
        //}
    }

}
