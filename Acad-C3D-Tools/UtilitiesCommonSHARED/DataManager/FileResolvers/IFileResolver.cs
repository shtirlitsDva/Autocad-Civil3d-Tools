﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.DataManager.FileResolvers
{
    internal interface IFileResolver
    {
        IEnumerable<string> ResolveFiles(string input);
    }
}
