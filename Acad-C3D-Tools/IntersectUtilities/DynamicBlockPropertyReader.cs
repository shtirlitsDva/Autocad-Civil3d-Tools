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
using System.Text;
using System.Text.RegularExpressions;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities;
using static IntersectUtilities.PipeSchedule;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

namespace IntersectUtilities.DynamicBlocks
{
    public static partial class PropertyReader
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
        #region Old methods, uncomment and modify as needed
        //public static MapValue ReadBlockName(BlockReference br, System.Data.DataTable fjvTable)
        //{
        //    Transaction tx = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction;
        //    string realName = ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
        //    return new MapValue(realName);
        //}
        //public static MapValue ReadComponentType(BlockReference br, System.Data.DataTable fjvTable) =>
        //    new MapValue(br.GetDynamicPropertyByName("Betegnelse").Value as string ?? "");
        ////{
        ////    string propertyToExtractName = "Type";

        ////    string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0);

        ////    if (valueToReturn.StartsWith("$"))
        ////    {
        ////        valueToReturn = valueToReturn.Substring(1);
        ////        //If the value is a pattern to extract from string
        ////        if (valueToReturn.Contains("{"))
        ////        {
        ////            valueToReturn = GetValueByRegex(br, propertyToExtractName, valueToReturn);
        ////        }
        ////        //Else the value is parameter literal to read
        ////        else return new MapValue(br.GetDynamicPropertyByName(valueToReturn).Value as string ?? "");
        ////    }
        ////    return new MapValue(valueToReturn ?? "");
        ////}
        //public static MapValue ReadBlockRotation(BlockReference br, System.Data.DataTable fjvTable) =>
        //    new MapValue(br.Rotation * (180 / Math.PI));
        #endregion
        public static string ReadComponentDN1Str(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN1";
            //Versioning
            string version = br.GetAttributeStringValue("VERSION");
            string valueToReturn = ReadStringParameterFromDataTable(
                br.RealName(), fjvTable, propertyToExtractName, 0, version);

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
        public static int ReadComponentDN1Int(BlockReference br, System.Data.DataTable fjvTable)
        {
            string value = ReadComponentDN1Str(br, fjvTable);
            if (value.IsNoE()) return 0;
            int result;
            if (int.TryParse(value, out result)) return result;
            else return 0;
        }
        public static string ReadComponentDN2Str(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "DN2";
            //Versioning
            string version = br.GetAttributeStringValue("VERSION");
            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0, version);

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
        public static int ReadComponentDN2Int(BlockReference br, System.Data.DataTable fjvTable)
        {
            string value = ReadComponentDN2Str(br, fjvTable);
            if (value.IsNoE()) return 0;
            int result;
            if (int.TryParse(value, out result)) return result;
            else return 0;
        }
        public static string ReadComponentSeries(BlockReference br, System.Data.DataTable fjvTable)
        {
            string propertyToExtractName = "Serie";
            //Versioning
            string version = br.GetAttributeStringValue("VERSION");
            string valueToReturn = ReadStringParameterFromDataTable(br.RealName(), fjvTable, propertyToExtractName, 0, version);

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
        public static string ReadComponentSystem(
            BlockReference br, System.Data.DataTable fjvTable, EndType endType = default)
        {
            string propertyToExtractName = "System";
            //Versioning
            string version = br.GetAttributeStringValue("VERSION");
            string valueToReturn = ReadStringParameterFromDataTable(
                br.RealName(), fjvTable, propertyToExtractName, 0, version);

            //Special care for F-Model og Y-Rør
            string type = ReadStringParameterFromDataTable(
                br.RealName(), fjvTable, "Type", 0, version);

            //Write if here!

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
        public static double ReadComponentDN1KodDouble(BlockReference br, System.Data.DataTable fjvTable)
        {
            //There's a confusion about PipeTypeEnum and System in FJV DynamiskeKomponenter
            //In PipeSchedule PipeTypeEnum tells Twin, Bonded, etc. But in FJV DK the name of the column is System.
            //In PipeSchedule PipeSystemEnum tells what material the pipe is: steel etc.
            //Possible solution: Rename the column in FJV DK to PipeType -> requires that ALL block's property renamed also.
            string systemString = ReadComponentSystem(br, fjvTable);
            PipeTypeEnum system;
            if (Enum.TryParse(systemString, out system))
            {
                int dn = ReadComponentDN1Int(br, fjvTable);
                if (dn == 0 || dn == 999) throw new System.Exception($"{br.RealName()} failed to read DN1!");

                string seriesString = ReadComponentSeries(br, fjvTable);
                PipeSeriesEnum series;
                if (Enum.TryParse(seriesString, out series))
                {
                    switch (system)
                    {
                        case PipeTypeEnum.Twin:
                            return PipeSchedule.GetTwinPipeKOd(dn, series);
                        case PipeTypeEnum.Frem:
                        case PipeTypeEnum.Retur:
                        case PipeTypeEnum.Enkelt:
                            return PipeSchedule.GetBondedPipeKOd(dn, series);
                        case PipeTypeEnum.Ukendt:
                        default:
                            throw new System.Exception($"{br.RealName()} returned non-standard \"System\": (PipeTypeEnum) {systemString}!");
                    }
                }
                else throw new System.Exception($"{br.RealName()} returned non-standard \"Serie\": {seriesString}!");
            }
            else throw new System.Exception($"{br.RealName()} returned non-standard \"System\": {systemString}!");
        }
        public static double ReadComponentDN2KodDouble(BlockReference br, System.Data.DataTable fjvTable)
        {
            //There's a confusion about PipeTypeEnum and System in FJV DynamiskeKomponenter
            //In PipeSchedule PipeTypeEnum tells Twin, Bonded, etc. But in FJV DK the name of the column is System.
            //In PipeSchedule PipeSystemEnum tells what material the pipe is: steel etc.
            //Possible solution: Rename the column in FJV DK to PipeType -> requires that ALL block's property renamed also.

            string systemString = ReadComponentSystem(br, fjvTable);
            PipeTypeEnum system;
            if (Enum.TryParse(systemString, out system))
            {
                int dn = ReadComponentDN2Int(br, fjvTable);
                if (dn == 0 || dn == 999) throw new System.Exception($"{br.RealName()} failed to read DN1!");

                string seriesString = ReadComponentSeries(br, fjvTable);
                PipeSeriesEnum series;
                if (Enum.TryParse(seriesString, out series))
                {
                    switch (system)
                    {
                        case PipeTypeEnum.Twin:
                            return PipeSchedule.GetTwinPipeKOd(dn, series);
                        case PipeTypeEnum.Frem:
                        case PipeTypeEnum.Retur:
                        case PipeTypeEnum.Enkelt:
                            return PipeSchedule.GetBondedPipeKOd(dn, series);
                        case PipeTypeEnum.Ukendt:
                        default:
                            throw new System.Exception($"{br.RealName()} returned non-standard \"System\": (PipeTypeEnum) {systemString}!");
                    }
                }
                else throw new System.Exception($"{br.RealName()} returned non-standard \"Serie\": {seriesString}!");
            }
            else throw new System.Exception($"{br.RealName()} returned non-standard \"System\": {systemString}!");
        }
    }
}
