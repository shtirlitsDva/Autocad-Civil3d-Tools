using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

using AcadOverrules.ViewFrameGripOverrule;

namespace AcadOverrules
{
    public class Commands
    {
        private static FjvPolylineLabel _fjvPolylineLabelOverrule;

        [CommandMethod("TOGGLEFJVLABEL")]
        public static void togglefjvlabeloverrule()
        {
            if (_fjvPolylineLabelOverrule == null)
            {
                _fjvPolylineLabelOverrule = new FjvPolylineLabel();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule);
                _fjvPolylineLabelOverrule.Dispose();
                _fjvPolylineLabelOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }                                                         
        
        private static GasPolylineLabel _GasPolylineLabelOverrule;

        [CommandMethod("TOGGLEGASLABEL")]
        public static void togglegaslabeloverrule()
        {
            if (_GasPolylineLabelOverrule == null)
            {
                _GasPolylineLabelOverrule = new GasPolylineLabel();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _GasPolylineLabelOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _GasPolylineLabelOverrule);
                _GasPolylineLabelOverrule.Dispose();
                _GasPolylineLabelOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static AlignmentNaMark _AlignmentNaMark;

        //Not working, development suspended
        //[CommandMethod("TOGGLEALNA")]
        public static void togglealnaoverrule()
        {
            if (_AlignmentNaMark == null)
            {
                _AlignmentNaMark = new AlignmentNaMark();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _AlignmentNaMark, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _AlignmentNaMark);
                _AlignmentNaMark.Dispose();
                _AlignmentNaMark = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static PolylineDirection _polylineDirection;

        [CommandMethod("TOGGLEPOLYDIR")]
        public static void togglepolydiroverrule()
        {
            if (_polylineDirection == null)
            {
                _polylineDirection = new PolylineDirection();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirection, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirection);
                _polylineDirection.Dispose();
                _polylineDirection = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static PolylineDirFjv _polylineDirFjv;

        [CommandMethod("TOGGLEFJVDIR")]
        public static void togglefjvdiroverrule()
        {
            if (_polylineDirFjv == null)
            {
                _polylineDirFjv = new PolylineDirFjv();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirFjv, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirFjv);
                _polylineDirFjv.Dispose();
                _polylineDirFjv = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static GripVectorOverrule _gripVectorOverrule;

        [CommandMethod("TOGGLEGRIPOR")]
        public static void togglegripoverrule()
        {
            if (_gripVectorOverrule == null)
            {
                _gripVectorOverrule = new GripVectorOverrule();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _gripVectorOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _gripVectorOverrule);
                _gripVectorOverrule.Dispose();
                _gripVectorOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static ViewFrameCentreGripOverrule _viewFrameCentreGripOverrule;

        [CommandMethod("TOGGLEVIEWFRAMESOVERRULE")]
        public static void toggleviewframeoverrule()
        {
            if (_viewFrameCentreGripOverrule == null)
            {
                _viewFrameCentreGripOverrule = new ViewFrameCentreGripOverrule();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _viewFrameCentreGripOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _viewFrameCentreGripOverrule);
                _viewFrameCentreGripOverrule.Dispose();
                _viewFrameCentreGripOverrule = null;
            }
        }
    }
}
