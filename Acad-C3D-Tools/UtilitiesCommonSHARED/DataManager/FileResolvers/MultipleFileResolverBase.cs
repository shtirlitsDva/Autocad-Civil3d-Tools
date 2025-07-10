using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.DataManager.FileResolvers
{
    internal abstract class MultipleFileResolverBase : IFileResolver
    {
        private string _mask;

        public MultipleFileResolverBase(string mask) { _mask = mask; }

        public IEnumerable<string> ResolveFiles(string input)
        {
            var isFile = File.Exists(input);
            var isDirectory = Directory.Exists(input);

            if (isFile) return [input];
            if (isDirectory) return ResolveMultipleFiles(input);
            return Enumerable.Empty<string>();
        }
        private IEnumerable<string> ResolveMultipleFiles(string input) =>
            Directory.EnumerateFiles(input, _mask, SearchOption.TopDirectoryOnly);
    }
}
