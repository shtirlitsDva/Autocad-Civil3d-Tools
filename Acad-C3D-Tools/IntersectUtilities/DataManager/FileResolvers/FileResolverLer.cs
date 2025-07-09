using System;
using System.Collections.Generic;
using System.IO;

namespace IntersectUtilities.DataManager.FileResolvers
{
    internal class FileResolverLer : MultipleFileResolverBase
    {
        public FileResolverLer() : base("*_3DLER.dwg") { }                
    }
}
