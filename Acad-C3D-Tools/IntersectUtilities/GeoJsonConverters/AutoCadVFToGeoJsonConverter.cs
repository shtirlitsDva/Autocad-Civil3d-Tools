using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;
using FolderSelect;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities.DynamicBlocks;

using Autodesk.Aec.DatabaseServices;

namespace IntersectUtilities
{
    public interface IAutoCadVFToGeoJsonConverter
    {
        IEnumerable<GeoJsonFeature> Convert(Entity entity);
    }

    public class ViewFrameToGeoJsonLineStringConverter : IAutoCadVFToGeoJsonConverter
    {
        public IEnumerable<GeoJsonFeature> Convert(Entity entity)
        {
            if (!(entity is ViewFrame vf))
                throw new ArgumentException($"Entity {entity.Handle} is not a polyline!");

            var feature = new GeoJsonFeature
            {
                Properties = new Dictionary<string, object>
                {
                    { "DwgNumber", vf.Name },
                },

                Geometry = new GeoJsonGeometryLineString() { },
            };

            DBObjectCollection dboc1 = new DBObjectCollection();
            vf.Explode(dboc1);
            foreach (var item in dboc1)
            {
                if (item is BlockReference bref)
                {
                    DBObjectCollection dboc2 = new DBObjectCollection();
                    bref.Explode(dboc2);

                    foreach (var item2 in dboc2)
                    {
                        if (item2 is Polyline pline)
                        {
                            ((GeoJsonGeometryLineString)feature.Geometry).Coordinates
                                = new double[5][];
                            Point3d p;
                            for (int i = 0; i < pline.NumberOfVertices + 1; i++)
                            {
                                switch (i)
                                {
                                    case 4:
                                        p = pline.GetPoint3dAt(0);
                                        break;
                                    default:
                                        p = pline.GetPoint3dAt(i);
                                        break;
                                }
                                ((GeoJsonGeometryLineString)feature.Geometry).Coordinates[i]
                                    = new double[] { p.X, p.Y };
                            }
                        }
                    }
                }
            }
            yield return feature;
        }
    }

    public static class ViewFrameToGeoJsonConverterFactory
    {
        public static IAutoCadVFToGeoJsonConverter CreateConverter(Entity entity)
        {
            switch (entity)
            {
                case ViewFrame _:
                    return new ViewFrameToGeoJsonLineStringConverter();
                default:
                    prdDbg($"Unsupported AutoCAD entity type {entity.GetType()} encountered.");
                    return null;
                    //throw new NotSupportedException("Unsupported AutoCAD entity type.");
            }
        }
    }
}
