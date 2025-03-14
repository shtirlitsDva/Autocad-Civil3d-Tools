using NorsynHydraulicShared;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DimensioneringV2.AutoCAD
{
    internal class Logger : ILog
    {
        public void Log(object obj)
        {
            throw new NotImplementedException();
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        public void Report(object obj)
        {
            DimensioneringV2.Utils.prtDbg(obj);
        }

        public void Report(string message)
        {
            DimensioneringV2.Utils.prtDbg(message);
        }

        public void Report()
        {
            DimensioneringV2.Utils.prtDbg();
        }
    }
}
