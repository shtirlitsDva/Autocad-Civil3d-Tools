using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.DataManager.FileResolvers
{
    internal class FileResolverLængdeprofiler : MultipleFileResolverBase
    {
        public FileResolverLængdeprofiler() : base("Længdeprofiler*.dwg") { }        
    }
}
