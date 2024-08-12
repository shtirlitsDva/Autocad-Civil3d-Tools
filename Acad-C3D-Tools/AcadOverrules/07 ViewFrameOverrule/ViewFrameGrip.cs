﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace AcadOverrules.ViewFrameGripOverrule
{
    internal class ViewFrameGrip : GripData
    {
        public ViewFrameGrip()
        {
            ForcedPickOn = false;
            GizmosEnabled = false;
            DrawAtDragImageGripPoint = false;
            IsPerViewport = false;
            ModeKeywordsDisabled = true;
            RubberBandLineDisabled = true;
            TriggerGrip = true;
            HotGripInvokesRightClick = true;
            HotGripInvokesRightClick = false;
        }
        public Oid EntityId { get; set; } = Oid.Null;

        public override bool ViewportDraw(
            ViewportDraw worldDraw,
            ObjectId entityId,
            DrawType type,
            Point3d? imageGripPoint,
            int gripSizeInPixels)
        {
            var unit = worldDraw.Viewport.GetNumPixelsInUnitSquare(GripPoint);
            var gripHeight = 2.5 * gripSizeInPixels / unit.X;
            var points = new Point3dCollection();

            var pl = entityId.Open(OpenMode.ForRead) as Polyline;
            
            var verts = 

            //var x = GripPoint.X;
            //var y = GripPoint.Y;
            //var offset = gripHeight / 2.0;
            //points.Add(new Point3d(x - offset, y, 0.0));
            //points.Add(new Point3d(x, y - offset, 0.0));
            //points.Add(new Point3d(x + offset, y, 0.0));
            //points.Add(new Point3d(x, y + offset, 0.0));
            //Point3d center = new Point3d(x, y, 0.0);
            //var radius = offset;



            worldDraw.SubEntityTraits.FillType = FillType.FillAlways;
            worldDraw.SubEntityTraits.Color = 30;
            worldDraw.Geometry.Circle(center, radius, Vector3d.ZAxis);
            return true;
        }

        public override ReturnValue OnHotGrip(Oid entityId, Context contextFlags)
        {
            return ReturnValue.Ok;
        }
    }
}
