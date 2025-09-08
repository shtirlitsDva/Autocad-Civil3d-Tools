using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Common
{
    public interface IProgressReporter { void MaybeReport(int done, int total); }
}
