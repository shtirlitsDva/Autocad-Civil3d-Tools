using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Svg;
using Svg.Transforms;
using static IntersectUtilities.Utils;

namespace ExportBlocksToSvg
{
    public static class SvgUtils
    {
        public static void AddTransforms(SvgGroup group, Flip flip)
        {
            if (group.Bounds != default)
            {
                System.Drawing.RectangleF bounds = group.Bounds;

                switch (flip)
                {
                    case Flip.PN: //(1,-1,1) Vertical
                        group.Transforms = new SvgTransformCollection();
                        group.Transforms.Add(new SvgTranslate(0, 2 * bounds.Y + bounds.Height));
                        group.Transforms.Add(new SvgScale(1, -1));
                        break;
                    case Flip.NN: //(-1,-1,1) V and H, same as rotate 180°
                        group.Transforms = new SvgTransformCollection();
                        group.Transforms.Add(new SvgTranslate(2 * bounds.X + bounds.Width, 2 * bounds.Y + bounds.Height));
                        group.Transforms.Add(new SvgScale(-1, -1));
                        break;
                    case Flip.NP: //(-1,1,1) Horizontal
                        group.Transforms = new SvgTransformCollection();
                        group.Transforms.Add(new SvgTranslate(2 * bounds.X + bounds.Width));
                        group.Transforms.Add(new SvgScale(-1, 1));
                        break;
                    case Flip.None:
                    case Flip.PP:
                    default:
                        break;
                }
            }
        }
    }
}
