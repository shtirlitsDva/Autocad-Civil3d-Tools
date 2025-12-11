using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

using AcadOverrules.ViewFrameGripOverrule;
using System;

using static IntersectUtilities.UtilsCommon.Utils;

namespace AcadOverrules
{
    public class Commands : IExtensionApplication
    {
        #region Interface memebers
        public void Initialize()
        {
            prdDbg("AcadOverrules Initializing!");
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(
                IntersectUtilities.EventHandlers.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {

        }
        #endregion

        private static FjvPolylineLabel _fjvPolylineLabelOverrule;
        /// <command>TOGGLEFJVLABEL</command>
        /// <summary>
        /// Labels pipe with system prefix, size and type, ie. DN50-T.
        /// T - Twin, E - Enkelt.
        /// Labels arcs with radius and marking for buerør, in-situ buk
        /// and impossible radius.
        /// Marks small angle deviations between pl segments.
        /// </summary>
        /// <category>Overrules</category>
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

        private static PolylineDirection _polylineDirection;
        /// <command>TOGGLEPOLYDIR</command>
        /// <summary>
        /// Creates arrows for all polylines that visualise the direction of the polyline.
        /// </summary>
        /// <category>Overrules</category>
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
        /// <command>TOGGLEFJVDIR</command>
        /// <summary>
        /// This applies only to polylines that resides in layers that correspond to pipe systems.
        /// Creates arrows for all these polylines that visualise the direction of the polyline.
        /// </summary>
        /// <category>Overrules</category>
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

        private static PolylineArcHighlight _polylineArcHighlight;
        /// <command>TOGGLEARCHIGHLIGHT</command>
        /// <summary>
        /// Highlights arc segments of polylines with cyan color overlay.
        /// Straight segments are not affected.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEPOLYARCS")]
        public static void togglearchighlightoverrule()
        {
            if (_polylineArcHighlight == null)
            {
                _polylineArcHighlight = new PolylineArcHighlight();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _polylineArcHighlight, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _polylineArcHighlight);
                _polylineArcHighlight.Dispose();
                _polylineArcHighlight = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }
#if DEBUG
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
#endif

#if DEBUG
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
#endif
    }
}
