using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.DataManager.FileResolvers
{
    internal class FileResolverSingle : IFileResolver
    {
        public IEnumerable<string> ResolveFiles(string input)
        {
            if (File.Exists(input)) yield return input;            
        }
    }
}
