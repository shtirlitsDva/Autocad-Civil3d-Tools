using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Text.RegularExpressions;
using IntersectUtilities.UtilsCommon;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public static class ComponentSchedule
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
        private static string ReadDynamicPropertyValue(this BlockReference br, string propertyName)
        {
            DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty property in props)
            {
                //prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                if (property.PropertyName == propertyName)
                {
                    switch (property.UnitsType)
                    {
                        case DynamicBlockReferencePropertyUnitsType.NoUnits:
                            return property.Value.ToString();
                        case DynamicBlockReferencePropertyUnitsType.Angular:
                            double angular = Convert.ToDouble(property.Value);
                            return angular.ToDegrees().ToString("0.##");
                        case DynamicBlockReferencePropertyUnitsType.Distance:
                            double distance = Convert.ToDouble(property.Value);
                            return distance.ToString("0.##");
                        case DynamicBlockReferencePropertyUnitsType.Area:
                            double area = Convert.ToDouble(property.Value);
                            return area.ToString("0.00");
                        default:
                            return "";
                    }
                }
            }
            return "";
        }
        private static string RealName(this BlockReference br)
        {
            if (br.IsDynamicBlock)
            {
                Transaction tx = br.Database.TransactionManager.TopTransaction;
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
            //Debug 
            if (br.RealName() == "BØJN KDLR v2")
            {
                prdDbg(br.GetDynamicPropertyByName(propName).UnitsType.ToString());
            }

            string rawContents = br.GetDynamicPropertyByName(propName).Value as string;
            //Safeguard against value not being set -> CUSTOM
            if (rawContents == "Custom" || rawContents == "ÆNDR MIG")
                throw new System.Exception($"Parameter {propName} is not set for block handle {br.Handle}!");
            //Extract regex def from the table
            Regex regxExtract = new Regex(@"{(?<Regx>[^}]+)}");
            if (!regxExtract.IsMatch(valueToProcess)) throw new System.Exception("Regex definition is incorrect!");
            string extractedRegx = regxExtract.Match(valueToProcess).Groups["Regx"].Value;
            //extract needed value from the rawContents by using the extracted regex
            Regex finalValueRegex = new Regex(extractedRegx);
            if (!finalValueRegex.IsMatch(rawContents))
            {
                prdDbg($"Extracted Regex failed to match Raw Value for block {br.RealName()}, handle {br.Handle}!");
                prdDbg($"Returning instead: {finalValueRegex.Match(rawContents).Groups[propertyToExtractName].Value}");
                return finalValueRegex.Match(rawContents).Groups[propertyToExtractName].Value;
            }
            return finalValueRegex.Match(rawContents).Groups[propertyToExtractName].Value;
        }
        private static string ConstructStringByRegex(BlockReference br, string stringToProcess)
        {
            //Construct pattern which matches the parameter definition
            Regex variablePattern = new Regex(@"{\$(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");

            //Test if a pattern matches in the input string
            if (variablePattern.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = variablePattern.Match(stringToProcess);
                //Get the first capture
                string capture = match.Captures[0].Value;
                //Get the parameter name from the regex match
                string parameterName = match.Groups["Parameter"].Value;
                //Read the parameter value from BR
                string parameterValue = br.ReadDynamicPropertyValue(parameterName);
                //Replace the captured group in original string with the parameter value
                stringToProcess = stringToProcess.Replace(capture, parameterValue);
                //Recursively call current function
                //It runs on the string until no more captures remain
                //Then it returns
                stringToProcess = ConstructStringByRegex(br, stringToProcess);
            }
            
            return stringToProcess;
        }
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
}
