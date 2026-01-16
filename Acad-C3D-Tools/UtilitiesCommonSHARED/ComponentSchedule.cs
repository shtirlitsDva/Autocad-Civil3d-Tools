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
                            return angular.ToDeg().ToString("0.##");
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
            if (valueToProcess == "200 Logstor") {; }

            //Extract property name
            Regex regex = new Regex(@"(?<Name>^[\w\s]+)");
            string propName = "";
            //if (!regex.IsMatch(valueToProcess)) throw new System.Exception("Property name not found!");
            if (!regex.IsMatch(valueToProcess)) return valueToProcess;
            propName = regex.Match(valueToProcess).Groups["Name"].Value;
            //Read raw data from block
            //Debug 
            //if (br.RealName() == "BØJN KDLR v2")
            //{
            //    prdDbg(br.GetDynamicPropertyByName(propName).UnitsType.ToString());
            //}

            var prop = br.GetDynamicPropertyByName(propName);
            if (prop == null) return valueToProcess;
            string rawContents = prop.Value as string;
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
            //Example definition r1 matches: $Præisoleret bøjning, 90gr, L {$L1}x{$L2} m
            //Example definition r2 matches: $System <- this is used to read a dynmic property value directly
            Regex r1 = new Regex(@"{\$(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");
            Regex r2 = new Regex(@"\$(?<Parameter>[a-zA-Z]*)$");
            Regex r3 = new Regex(@"{#(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");

            //Test if a pattern matches in the input string
            if (r1.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = r1.Match(stringToProcess);
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
            if (r2.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = r2.Match(stringToProcess);
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
            if (r3.IsMatch(stringToProcess))
            {
                //Get the first match
                Match match = r3.Match(stringToProcess);
                //Get the first capture
                string capture = match.Captures[0].Value;
                //Get the parameter name from the regex match
                string parameterName = match.Groups["Parameter"].Value;
                //Convert parameter name to csv table column name
                DynamicProperty prop;
                if (!Enum.TryParse(parameterName, out prop))
                    throw new Exception($"Parameter {parameterName} is not a valid DynamicProperty!");
                //Read the value specified and parse it
                string parameterValue = br.ReadDynamicCsvProperty(prop);
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
        public static string ReadBlockName(BlockReference br, FjvDynamicComponents fjvComponents) => br.RealName();

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

        public static string ReadComponentType(BlockReference br, FjvDynamicComponents fjvComponents)
        {
            string valueToReturn = fjvComponents.Type(br.RealName()) ?? "";

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
        public static double ReadBlockRotation(BlockReference br, FjvDynamicComponents fjvComponents) =>
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
        public static string ReadComponentSystem(BlockReference br, FjvDynamicComponents fjvComponents)
        {
            string valueToReturn = fjvComponents.System(br.RealName()) ?? "";

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, "System", valueToReturn);
                }
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
        public static string ReadComponentDN1(BlockReference br, FjvDynamicComponents fjvComponents)
        {
            string valueToReturn = fjvComponents.DN1(br.RealName()) ?? "";

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, "DN1", valueToReturn);
                }
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
        public static string ReadComponentDN2(BlockReference br, FjvDynamicComponents fjvComponents)
        {
            string valueToReturn = fjvComponents.DN2(br.RealName()) ?? "";

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, "DN2", valueToReturn);
                }
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
        public static string ReadComponentVinkel(BlockReference br, FjvDynamicComponents fjvComponents)
        {
            string valueToReturn = fjvComponents.Vinkel(br.RealName()) ?? "";

            if (valueToReturn.StartsWith("$"))
            {
                valueToReturn = valueToReturn.Substring(1);
                if (valueToReturn.Contains("{"))
                {
                    valueToReturn = GetValueByRegex(br, "Vinkel", valueToReturn);
                }
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
        public static string ReadDynamicCsvProperty(
            this BlockReference br, DynamicProperty prop, bool parseProperty = true)
        {
            var fk = Csv.FjvDynamicComponents;

            string key = br.RealName();
            string parameter = prop.ToString();
            string version = br.GetAttributeStringValue("VERSION");

            // Get column index from parameter name
            if (!Enum.TryParse<FjvDynamicComponents.Columns>(parameter, out var columnEnum))
                return "";

            int columnIndex = (int)columnEnum;
            int versionIndex = (int)FjvDynamicComponents.Columns.Version;

            if (fk.HasNavn(key))
            {
                var matchingRow = fk.Rows
                    .Where(row => 
                        FjvDynamicComponents.Col(row, FjvDynamicComponents.Columns.Navn) == key &&
                        FjvDynamicComponents.Col(row, FjvDynamicComponents.Columns.Version) == version)
                    .FirstOrDefault();

                if (matchingRow == null || matchingRow.Length <= columnIndex)
                    return "";

                string value = matchingRow[columnIndex] ?? "";
                if (parseProperty)
                {
                    //Catch ordinary block attributes
                    //This is a quick fix!
                    //TODO: Refactor attributes reading!!!!

                    if (value.StartsWith("#"))
                    {
                        value = value.Substring(1);

                        value = br.GetAttributeStringValue(value);
                    }

                    //Continue with old dynamic attributes logic

                    value = ConstructStringByRegex(br, value);
                    if (value.StartsWith("$"))
                    {
                        value = value.Substring(1);

                        //If the value is a pattern to extract from string
                        if (value.Contains("{"))
                        {
                            value = GetValueByRegex(br, parameter, value);
                        }
                        //Else the value is parameter literal to read
                        else
                        {
                            var result = br.GetDynamicPropertyByName(value)?.Value?.ToString() ?? "";
                            if (result == "") 
                            {
                                //If the value is not found within dynamic properties,
                                //try to read it from the block attributes
                                result = br.GetAttributeStringValue(value);
                                return result;
                            }

                            return result;
                        }
                    }
                    return value;
                }
                else return value;
            }
            else return "";
        }
        public static PipelineElementType GetPipelineType(this BlockReference br)
        {
            string typeString = br.ReadDynamicCsvProperty(DynamicProperty.Type, false);
            if (PipelineElementTypeDict.ContainsKey(typeString))
                return PipelineElementTypeDict[typeString];
            else
            {
                HashSet<string> missing = new HashSet<string>();
                var fk = Csv.FjvDynamicComponents;
                foreach (var row in fk.Rows)
                {
                    var type = FjvDynamicComponents.Col(row, FjvDynamicComponents.Columns.Type);
                    if (!string.IsNullOrEmpty(type) && !PipelineElementTypeDict.ContainsKey(type))
                        missing.Add(type);
                }

                prdDbg(string.Join("\n", missing.OrderBy(x => x)));

                throw new Exception($"PipelineType {typeString} not found in dictionary!\n" +
                $"Add this element to PipelineElementType enum");
            }
        }
        /// <summary>
        /// For historical reasons, type (Enkelt, Twin) for blocks is stored in the System column
        /// which causes confusion with pipelineschedule systemenum.
        /// </summary>
        public static PipeTypeEnum GetPipeTypeEnum(this BlockReference br, bool parse = true)
        {
            string typeString = br.ReadDynamicCsvProperty(DynamicProperty.System, parse);
            object pipeTypeEnum;
            if (Enum.TryParse(typeof(PipeTypeEnum), typeString, out pipeTypeEnum)) return (PipeTypeEnum)pipeTypeEnum;
            else return PipeTypeEnum.Ukendt;
        }
        public static PipeSeriesEnum GetPipeSeriesEnum(this BlockReference br, bool parse = true)
        {
            string pipeSeriesString = br.ReadDynamicCsvProperty(DynamicProperty.Serie, parse);
            object pipeSeriesEnum;
            if (Enum.TryParse(typeof(PipeSeriesEnum), pipeSeriesString, out pipeSeriesEnum)) return (PipeSeriesEnum)pipeSeriesEnum;
            else return PipeSeriesEnum.Undefined;
        }
        public static PipeSystemEnum GetPipeSystemEnum(this BlockReference br, bool parse = true)
        {
            string pipeSystemString = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, parse);
            object pipeSystemEnum;
            if (Enum.TryParse(typeof(PipeSystemEnum), pipeSystemString, out pipeSystemEnum)) return (PipeSystemEnum)pipeSystemEnum;
            else return PipeSystemEnum.Ukendt;
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
