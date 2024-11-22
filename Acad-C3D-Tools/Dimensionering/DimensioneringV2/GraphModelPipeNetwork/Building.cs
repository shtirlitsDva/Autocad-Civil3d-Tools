using Autodesk.AutoCAD.DatabaseServices;
using Dimensionering.DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dimensionering.DimensioneringV2.GraphModelPipeNetwork
{
    internal class Building
    {
        public Point2D Location { get; }
        public Building(BlockReference br)
        {
            Location = new Point2D(br.Position.X, br.Position.Y);
        }
    }
}
