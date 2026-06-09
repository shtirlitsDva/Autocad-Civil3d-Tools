using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <summary>
        /// Ghost-text preview jig for pipe labels: the MText follows the cursor and
        /// keeps itself rotated tangent to the source polyline. The "Flip" keyword
        /// rotates the label 180° (handled by the caller via <see cref="ToggleFlip"/>).
        /// </summary>
        private sealed class LabelJig : EntityJig
        {
            private readonly Polyline _pline;
            private Point3d _cursor;
            private double _flipOffset;

            public LabelJig(MText label, Polyline pline) : base(label)
            {
                _pline = pline;
                _cursor = pline.StartPoint;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                JigPromptPointOptions opts = new JigPromptPointOptions(
                    "\nChoose location of label [Flip]: ");
                opts.Keywords.Add("Flip");
                opts.UserInputControls = UserInputControls.AcceptOtherInputString;

                PromptPointResult res = prompts.AcquirePoint(opts);

                if (res.Status == PromptStatus.Keyword)
                    return SamplerStatus.OK;

                if (res.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;

                if (_cursor.IsEqualTo(res.Value, Tolerance.Global))
                    return SamplerStatus.NoChange;

                _cursor = res.Value;
                return SamplerStatus.OK;
            }

            protected override bool Update()
            {
                MText lbl = (MText)Entity;
                Point3d closest = _pline.GetClosestPointTo(_cursor, true);
                Vector3d d = _pline.GetFirstDerivative(closest);
                lbl.Location = new Point3d(_cursor.X, _cursor.Y, 0);
                lbl.Rotation = Math.Atan2(d.Y, d.X) + _flipOffset;
                return true;
            }

            public void ToggleFlip() => _flipOffset = (_flipOffset == 0.0) ? Math.PI : 0.0;
        }
    }
}
