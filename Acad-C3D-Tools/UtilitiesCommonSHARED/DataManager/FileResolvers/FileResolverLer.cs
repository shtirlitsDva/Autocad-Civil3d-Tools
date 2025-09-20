using System;
using System.Collections.Generic;
using System.IO;

namespace IntersectUtilities.UtilsCommon.DataManager.FileResolvers
{
    internal class FileResolverLer : MultipleFileResolverBase
    {
        public FileResolverLer() : base("*_3DLER.dwg") { }                
    }
}
