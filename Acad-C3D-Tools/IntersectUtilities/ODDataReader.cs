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
using System.Text.RegularExpressions;
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
            new MapValue(br.Rotation * (180 / Math.PI));
        public static MapValue ReadComponentSystem(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "System", 0));
        public static MapValue ReadComponentDN1(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "DN1", 0));
        public static MapValue ReadComponentDN2(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(ReadStringParameterFromDataTable(br.Name, fjvTable, "DN2", 0));
        public static MapValue ReadComponentSeries(BlockReference br, System.Data.DataTable fjvTable) => new MapValue("S3");
        public static MapValue ReadComponentWidth(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(Math.Abs(bbox.MaxPoint.X - bbox.MinPoint.X));
        }
        public static MapValue ReadComponentHeight(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(Math.Abs(bbox.MaxPoint.Y - bbox.MinPoint.Y));
        }
        public static MapValue ReadComponentOffsetX(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            double value = (bbox.MinPoint.X + bbox.MaxPoint.X) / 2;
            //Debug
            if (ReadComponentFlipState(br) != "_PP") prdDbg(br.Handle.ToString() + ": " + ReadComponentFlipState(br));
            //Debug
            switch (ReadComponentFlipState(br))
            {
                case "_NP":
                    value = value * -1;
                    break;
                default:
                    break;
            }
            return new MapValue(value);
        }
        public static MapValue ReadComponentOffsetY(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(-(bbox.MinPoint.Y + bbox.MaxPoint.Y) / 2);
        }
        internal static MapValue ReadComponentFlipState(BlockReference br, System.Data.DataTable fjvTable)
        {
            Scale3d scale = br.ScaleFactors;
            if (scale.X < 0 && scale.Y < 0) return new MapValue("_NN");
            if (scale.X > 0 && scale.Y > 0) return new MapValue("_PP");
            if (scale.X > 0) return new MapValue("_PN");
            if (scale.Y > 0) return new MapValue("_NP");
            return new MapValue("_PP");
        }
        internal static string ReadComponentFlipState(BlockReference br)
        {
            Scale3d scale = br.ScaleFactors;
            if (scale.X < 0 && scale.Y < 0) return "_NN";
            if (scale.X > 0 && scale.Y > 0) return "_PP";
            if (scale.X > 0) return "_PN";
            if (scale.Y > 0) return "_NP";
            return "_PP";
        }
    }

    public static class DynKomponenter
    {
        private static DynamicBlockReferenceProperty GetDynamicPropertyByName(this BlockReference br, string name)
        {
            DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty property in pc)
            {
                if (property.PropertyName == name) return property;
            }
            return null;
        }
        private static string RealName(this BlockReference br)
        {
            if (br.IsDynamicBlock)
            {
                Transaction tx = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction;
                return ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
            }
            else return br.Name;
        }
        private static string GetValueByRegex(BlockReference br, string propertyToExtractName, string valueToProcess)
        {
            //Extract property name
            Regex regex = new Regex(@"(?<Name>^[\w\s]+)");
            string propName = "";
            if (!regex.IsMatch(valueToProcess)) throw new System.Exception("Property name not found!");
            propName = regex.Match(valueToProcess).Groups["Name"].Value;
            //Read raw data from block
            string rawContents = br.GetDynamicPropertyByName(propName).Value as string;
            //Safeguard against value not being set -> CUSTOM
            if (rawContents == "Custom") throw new System.Exception($"Parameter {propName} is not set for block handle {br.Handle}!");
            //Extract regex def from the table
            Regex regxExtract = new Regex(@"{(?<Regx>[^}]+)}");
            if (!regxExtract.IsMatch(valueToProcess)) throw new System.Exception("Regex definition is incorrect!");
            string extractedRegx = regxExtract.Match(valueToProcess).Groups["Regx"].Value;
            //extract needed value from the rawContents by using the extracted regex
            Regex finalValueRegex = new Regex(extractedRegx);
            if (!finalValueRegex.IsMatch(rawContents)) throw new System.Exception($"Extracted Regex failed to match Raw Value for block {br.Name}, handle {br.Handle.ToString()}!");
            return finalValueRegex.Match(rawContents).Groups[propertyToExtractName].Value;
        }
        public static MapValue ReadBlockName(BlockReference br, System.Data.DataTable fjvTable)
        {
            Transaction tx = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction;
            string realName = ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
            return new MapValue(realName);
        }
        public static MapValue ReadComponentType(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(br.GetDynamicPropertyByName("Betegnelse").Value as string ?? "");
        public static MapValue ReadBlockRotation(BlockReference br, System.Data.DataTable fjvTable) =>
            new MapValue(br.Rotation * (180 / Math.PI));
        public static MapValue ReadComponentSystem(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "System";

            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return new MapValue(br.GetDynamicPropertyByName(valueToReturn).Value as string ?? "");
            }
            return new MapValue(valueToReturn ?? "");
        }
        public static MapValue ReadComponentDN1(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN1";

            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return new MapValue(br.GetDynamicPropertyByName(valueToReturn).Value as string ?? "");
            }
            return new MapValue(valueToReturn ?? "");
        }
        public static MapValue ReadComponentDN2(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN2";

            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return new MapValue(br.GetDynamicPropertyByName(valueToReturn).Value as string ?? "");
            }
            return new MapValue(valueToReturn ?? "");
        }
        public static MapValue ReadComponentSeries(BlockReference br, System.Data.DataTable fjvTable) => new MapValue("S3");
        public static MapValue ReadComponentWidth(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(Math.Abs(bbox.MaxPoint.X - bbox.MinPoint.X));
        }
        public static MapValue ReadComponentHeight(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(Math.Abs(bbox.MaxPoint.Y - bbox.MinPoint.Y));
        }
        public static MapValue ReadComponentOffsetX(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            double value = (bbox.MinPoint.X + bbox.MaxPoint.X) / 2;
            //Debug
            if (ReadComponentFlipState(br) != "_PP") prdDbg(br.Handle.ToString() + ": " + ReadComponentFlipState(br));
            //Debug
            switch (ReadComponentFlipState(br))
            {
                case "_NP":
                    value = value * -1;
                    break;
                default:
                    break;
            }
            return new MapValue(value);
        }
        public static MapValue ReadComponentOffsetY(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return new MapValue(-(bbox.MinPoint.Y + bbox.MaxPoint.Y) / 2);
        }
        internal static MapValue ReadComponentFlipState(BlockReference br, System.Data.DataTable fjvTable)
        {
            Scale3d scale = br.ScaleFactors;
            if (scale.X < 0 && scale.Y < 0) return new MapValue("_NN");
            if (scale.X > 0 && scale.Y > 0) return new MapValue("_PP");
            if (scale.X > 0) return new MapValue("_PN");
            if (scale.Y > 0) return new MapValue("_NP");
            return new MapValue("_PP");
        }
        internal static string ReadComponentFlipState(BlockReference br)
        {
            Scale3d scale = br.ScaleFactors;
            if (scale.X < 0 && scale.Y < 0) return "_NN";
            if (scale.X > 0 && scale.Y > 0) return "_PP";
            if (scale.X > 0) return "_PN";
            if (scale.Y > 0) return "_NP";
            return "_PP";
        }
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
