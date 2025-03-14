using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicShared
{
    public interface ILog
    {
        void Log(object obj);
        void Log(string message);
        void Report(object obj);
        void Report(string message);
        void Report();
    }
}
