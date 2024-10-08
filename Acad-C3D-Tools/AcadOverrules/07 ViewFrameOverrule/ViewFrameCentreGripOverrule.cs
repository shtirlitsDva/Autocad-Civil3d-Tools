﻿using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcadOverrules.ViewFrameGripOverrule
{
    internal class ViewFrameCentreGripOverrule : GripOverrule
    {
        private bool _enabled = false;
        private bool _originalOverruling = false;
        private static ViewFrameCentreGripOverrule _instance = null;
        private static string _layerName = "zNSViewFrames_DO_NOT_USE";
        public bool HideOriginals { get; set; } = true;
        public void EnableOverrule(bool enable)
        {
            if (enable)
            {
                if (_enabled) return;
                _originalOverruling = Overrule.Overruling;
                AddOverrule(RXClass.GetClass(typeof(Polyline)), this, false);
                SetCustomFilter();
                Overrule.Overruling = true;
                _enabled = true;
            }
            else
            {
                if (!_enabled) return;
                RemoveOverrule(RXClass.GetClass(typeof(Polyline)), this);
                Overrule.Overruling = _originalOverruling;
                _enabled = false;
            }
        }
        public override bool IsApplicable(RXObject overruledSubject)
        {
            var poly = overruledSubject as Polyline;
            if (poly != null)
            {
                bool polyClosed = poly.Closed;
                bool polyOnLayer = poly.Layer == _layerName;
                bool numberOfVertices = poly.NumberOfVertices == 4;

                return polyClosed && polyOnLayer;
            }
            return false;
        }
        public override void GetGripPoints(
        Entity entity,
        GripDataCollection grips,
        double curViewUnitSize,
        int gripSize,
        Vector3d curViewDir,
        GetGripPointsFlags bitFlags)
        {
            var poly = entity as Polyline;
            if (poly != null && poly.Closed)
            {
                using (var tran = entity.Database.TransactionManager.StartTransaction())
                {
                    var pt = poly.StartPoint;
                    var grip = new ViewFrameGrip()
                    {
                        GripPoint = pt,
                        EntityId = entity.ObjectId
                    };
                    grips.Add(grip);
                    tran.Commit();
                }
                if (!HideOriginals)
                {
                    base.GetGripPoints(
                    entity, grips, curViewUnitSize, gripSize, curViewDir, bitFlags);
                }
                return;
            }
            base.GetGripPoints(entity, grips, curViewUnitSize, gripSize, curViewDir, bitFlags);
        }
    }
}