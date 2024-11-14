﻿using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal abstract class ProfileViewSymbol : IProfileViewSymbol
    {
        protected string BlockName { get; private set; }

        public abstract void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation, 
            double dia, string layer, string distance, double kappeOd);
        public abstract void CreateSymbol(
            BlockTable bt, BlockTableRecord detailingBlock, Point3d location,
            double dia, string layer);
    }
}