using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Data;
using System.Collections.Generic;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities
{
    public static partial class ComponentSchedule
    {
        public static string ReadBlockName(BlockReference br, System.Data.DataTable fjvTable) => br.RealName();


        public static string ReadComponentType(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "Type";

            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = ConstructStringByRegex(br, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return (br.GetDynamicPropertyByName(valueToReturn)?.Value as string ?? "");
            }
            return valueToReturn ?? "";
        }

        public static double ReadBlockRotation(BlockReference br, System.Data.DataTable fjvTable) =>
            br.Rotation * (180 / Math.PI);


        public static string ReadComponentSystem(BlockReference br, System.Data.DataTable fjvTable)
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
                else return br.GetDynamicPropertyByName(valueToReturn).Value as string ?? "";
            }
            return valueToReturn ?? "";
        }


        public static string ReadComponentDN1(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN1";

            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //if (br.RealName() == "BØJN KDLR v2") { prdDbg(br.GetDynamicPropertyByName(valueToReturn).Value.ToString()); }

                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return br.GetDynamicPropertyByName(valueToReturn).Value.ToString() ?? "";
            }
            return valueToReturn ?? "";
        }


        public static string ReadComponentDN2(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN2";

            string valueToReturn = ReadStringParameterFromDataTable(
                br.RealName(), fjvTable, propertyToExtractName, 0);

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                //If the value is a pattern to extract from string
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
                }
                //Else the value is parameter literal to read
                else return br.GetDynamicPropertyByName(valueToReturn).Value.ToString() ?? "";
            }
            return valueToReturn ?? "";
        }


        public static string ReadComponentVinkel(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "Vinkel";

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
                else
                {
                    double value = Convert.ToDouble(br.GetDynamicPropertyByName(valueToReturn).Value);
                    return (value * (180 / Math.PI)).ToString("0.##");
                }
            }
            return valueToReturn ?? "";
        }

        public static double ReadComponentWidth(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return Math.Abs(bbox.MaxPoint.X - bbox.MinPoint.X);
        }

        public static double ReadComponentHeight(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return Math.Abs(bbox.MaxPoint.Y - bbox.MinPoint.Y);
        }

        public static double ReadComponentOffsetX(BlockReference br, System.Data.DataTable fjvTable)
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
            return value;
        }

        public static double ReadComponentOffsetY(BlockReference br, System.Data.DataTable fjvTable)
        {
            Matrix3d transform = br.BlockTransform;
            Matrix3d inverseTransform = transform.Inverse();
            br.TransformBy(inverseTransform);
            Extents3d bbox = br.Bounds.GetValueOrDefault();
            br.TransformBy(transform);
            return -(bbox.MinPoint.Y + bbox.MaxPoint.Y) / 2;
        }

        internal static string ReadComponentFlipState(BlockReference br, System.Data.DataTable fjvTable)
        {
            Scale3d scale = br.ScaleFactors;
            if (scale.X < 0 && scale.Y < 0) return "_NN";
            if (scale.X > 0 && scale.Y > 0) return "_PP";
            if (scale.X > 0) return "_PN";
            if (scale.Y > 0) return "_NP";
            return "_PP";
        }

        public static void CheckIfBlockIsLatestVersion(this BlockReference br, string blockName)
        {
            Database db = br.Database;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    var fk = Csv.FjvDynamicComponents;

                    var btr = db.GetBlockTableRecordByName(blockName);

                    #region Read present block version
                    string version = "";
                    foreach (Oid oid in btr)
                    {
                        if (oid.IsDerivedFrom<AttributeDefinition>())
                        {
                            var atdef = oid.Go<AttributeDefinition>(tx);
                            if (atdef.Tag == "VERSION") { version = atdef.TextString; break; }
                        }
                    }
                    if (version.IsNoE()) version = "1";
                    else if (version.Contains("v")) version = version.Replace("v", "");
                    int blockVersion = Convert.ToInt32(version);
                    #endregion

                    #region Determine latest version
                    var query = fk.Rows
                            .Where(row => FjvDynamicComponents.Col(row, FjvDynamicComponents.Columns.Navn) == blockName)
                            .Select(row => FjvDynamicComponents.Col(row, FjvDynamicComponents.Columns.Version))
                            .Select(x => { if (string.IsNullOrEmpty(x)) return "1"; else return x; })
                            .Select(x => Convert.ToInt32(x.Replace("v", "")))
                            .OrderBy(x => x);

                    if (query.Count() == 0)
                        throw new System.Exception($"Block {blockName} is not present in FJV Dynamiske Komponenter.csv!");
                    int maxVersion = query.Max();
                    #endregion

                    if (maxVersion != blockVersion)
                        throw new System.Exception(
                            $"Block {blockName} v{blockVersion} is not latest version v{maxVersion}! " +
                            $"Update with latest version from:\n" +
                            $"X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg\n" +
                            $"WARNING! This can break existing blocks! Caution is advised!");
                }
                catch (Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    throw;
                }
                tx.Commit();
            }
        }

    }
}
