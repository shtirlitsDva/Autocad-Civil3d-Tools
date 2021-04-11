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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

namespace IntersectUtilities.ODDataReader
{
    public static class Komponenter
    {
        public static MapValue ReadBlockName(BlockReference br, System.Data.DataTable fjvTable) => new MapValue(br.Name);
        public static MapValue ReadComponentType(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "Type", 0));
        public static MapValue ReadBlockRotation(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(br.Rotation * (180/Math.PI));
        public static MapValue ReadComponentSystem(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "System", 0));
        public static MapValue ReadComponentDN1(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "DN1", 0));
        public static MapValue ReadComponentDN2(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "DN2", 0));
        public static MapValue ReadComponentSeries(BlockReference br, System.Data.DataTable fjvTable) => new MapValue("S3");
    }
    public static class Pipes
    {
        public static MapValue ReadPipeDimension(Entity ent) 
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                    return new MapValue(32);
                case "FJV-TWIN-DN40":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                    return new MapValue(40);
                case "FJV-TWIN-DN50":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                    return new MapValue(50);
                case "FJV-TWIN-DN65":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                    return new MapValue(65);
                case "FJV-TWIN-DN80":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                    return new MapValue(80);
                case "FJV-TWIN-DN100":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                    return new MapValue(100);
                case "FJV-TWIN-DN125":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                    return new MapValue(125);
                case "FJV-TWIN-DN150":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                    return new MapValue(150);
                case "FJV-TWIN-DN200":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                    return new MapValue(200);
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                    return new MapValue(250);
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                    return new MapValue(300);
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                    return new MapValue(350);
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                    return new MapValue(400);
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                    return new MapValue(450);
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                    return new MapValue(500);
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return new MapValue(600);
                default:
                    prdDbg("For entity: " + ent.Handle.ToString() + " no pipe dimension could be determined!");
                    return new MapValue(999);
            }
        }
        public static MapValue ReadPipeLength(Entity ent)
        {
            switch (ent)
            {
                case Polyline pline:
                    return new MapValue(pline.Length);
                case Line line:
                    return new MapValue(line.Length);
                case Arc arc:
                    return new MapValue(arc.Length);
                default:
                    return new MapValue(0);
            }
        }
        public static MapValue ReadPipeSystem(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                    return new MapValue("Twin");
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return new MapValue("Enkelt");
                default:
                    prdDbg("For entity: " + ent.Handle.ToString() + " no system could be determined!");
                    return new MapValue("Unknown");
            }
        }
        public static MapValue ReadPipeSeries(Entity ent) => new MapValue("S3");
    }
}
