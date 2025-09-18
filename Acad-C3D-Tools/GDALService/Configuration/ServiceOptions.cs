using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Configuration
{
    internal class ServiceOptions
    {
        public int? Threads { get; init; } = null;
        public int ProgressEvery { get; init; } = 500;
    }
}
