﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.ProfileViewSymbol
{
    internal interface IProfileViewSymbol
    {
        void CreateSymbol(
            BlockTable bt, BlockTableRecord detailingBlock, Point3d location,
            double dia, string layer);
        void CreateDistances(
            BlockTableRecord btr, Matrix3d transform, Point3d labelLocation,
            double dia, string layer, string distance, double kappeOd);
    }
}