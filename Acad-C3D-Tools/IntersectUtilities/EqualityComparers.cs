using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using AcRx = Autodesk.AutoCAD.Runtime;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Autodesk.AutoCAD.Colors;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;

namespace IntersectUtilities
{
    public class PropertySetNameComparer : IEqualityComparer<PropertySet>
    {
        public bool Equals(PropertySet x, PropertySet y)
            => x.PropertySetDefinitionName == y.PropertySetDefinitionName;
        public int GetHashCode(PropertySet obj)
            => obj.PropertySetDefinitionName.GetHashCode();
    }
    public class PointDBHorizontalComparer : IEqualityComparer<DBPoint>
    {
        double Tol;

        public PointDBHorizontalComparer(double tol = 0.001)
        {
            Tol = tol;
        }

        public bool Equals(DBPoint a, DBPoint b) => null != a && null != b &&
            a.Position.HorizontalEqualz(b.Position, Tol);

        public int GetHashCode(DBPoint a) => Tuple.Create(
        Math.Round(a.Position.X, 3), Math.Round(a.Position.Y, 3)).GetHashCode();
    }

    public class Point3dHorizontalComparer : IEqualityComparer<Point3d>
    {
        double Tol;

        public Point3dHorizontalComparer(double tol = 0.001)
        {
            Tol = tol;
        }

        public bool Equals(Point3d a, Point3d b) => null != a && null != b &&
            a.HorizontalEqualz(b, Tol);

        public int GetHashCode(Point3d a) => Tuple.Create(
        Math.Round(a.X, 3), Math.Round(a.Y, 3)).GetHashCode();
    }
}

