using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Interop;

using static AutoCadCom.Enums;

namespace AutoCadCom
{
    public static class Enums
    {
        public enum AcadStateEnum
        {
            Busy,
            Idle
        }
    }

    public static class Tools
    {
        public static AcadStateEnum GetAcadState(AcadApplication app)
        {
            try
            {
                if (app.GetAcadState().IsQuiescent) return AcadStateEnum.Idle;
                else return AcadStateEnum.Busy;
            }
            catch (COMException ex)
            {
                //Console.WriteLine($"0x{ex.ErrorCode:X}");
                //Console.WriteLine($"0x{ex.HResult:X}");
                //Console.WriteLine(ex.Message);
                return AcadStateEnum.Busy;
            }
        }
    }
}
