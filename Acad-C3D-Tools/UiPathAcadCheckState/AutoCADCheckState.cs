using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Activities;
using System.ComponentModel;
using Autodesk.AutoCAD.Interop.Common;
using Autodesk.AutoCAD.Interop;
using System.Runtime.InteropServices;
using System.Threading;

namespace UiPathAcadCheckState
{
    [Category("App Integration.AutoCAD")]
    [DisplayName("ACAD Check state")]
    [Description("Checks state of AutoCAD Application")]
    public class AutoCadCheckState : CodeActivity
    {
        [Category("Output")]
        public OutArgument<bool> Idle { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            try
            {
                AcadApplication acadApp =
                    (AcadApplication)Marshal.GetActiveObject("AutoCAD.Application");

                if (acadApp.GetAcadState().IsQuiescent) Idle.Set(context, true);
                else Idle.Set(context, false);
            }
            catch (COMException ex)
            {
                Idle.Set(context, false);
                return;
            }
        }
    }
}
