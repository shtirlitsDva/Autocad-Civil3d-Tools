using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipeScheduleV2
{
    public class PipeTypeRepository : IPipeTypeRepository
    {
        private Dictionary<string, IPipeType> _pipeTypeDictionary = new Dictionary<string, IPipeType>();
        public IPipeType GetPipeType(string type)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentNullException($"PipeType is Null or Empty!");

            if (_pipeTypeDictionary.ContainsKey(type)) return _pipeTypeDictionary[type];
            else throw new ArgumentNullException($"PipeType {type} does not exist!");
        }
        public void Initialize(Dictionary<string, IPipeType> pipeTypeDict)
        {
            _pipeTypeDictionary = pipeTypeDict;
        }
        public IEnumerable<string> ListAllPipeTypes()
        {
            foreach (var k in _pipeTypeDictionary) yield return k.Key;
        }
    }
}
