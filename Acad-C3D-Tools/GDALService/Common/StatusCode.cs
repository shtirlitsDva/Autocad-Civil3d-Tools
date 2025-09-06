using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Common
{
    internal enum StatusCode
    {
        Success = 0,
        Error = 1,
        InvalidArgs = 2,
        NotFound = 3,
        NotInitialized = 4
    }
}
