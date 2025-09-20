using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Interop;

using static AutoCadCom.Tools;

namespace AutoCadCom
{
    internal class Program
    {
        static AcadApplication acadApp;
        static void Main(string[] args)
        {
            try
            {
                acadApp = (AcadApplication)Marshal.GetActiveObject("AutoCAD.Application");
            }
            catch (COMException ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }

            while (true)
            {
                Console.WriteLine(GetAcadState(acadApp));
                Thread.Sleep(1000);
            }
        }
    }
}
