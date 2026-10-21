#if BRICSCAD
using Bricscad.ApplicationServices;
using Teigha.Colors;
using Teigha.DatabaseServices;
using Bricscad.EditorInput;
using Teigha.Geometry;
using Teigha.Runtime;
using BlockReference = Teigha.DatabaseServices.BlockReference;
using Entity = Teigha.DatabaseServices.Entity;
using ObjectIdCollection = Teigha.DatabaseServices.ObjectIdCollection;
using oid = Teigha.DatabaseServices.ObjectId;
using OpenMode = Teigha.DatabaseServices.OpenMode;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
#endif

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.DynamicBlocks
{
    public static partial class PropertyReader
    {
        public static object GetDynamicPropertyByName(this BlockReference br, string name)
        {
            DynamicBlockReferencePropertyCollection pc = br.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty property in pc)
            {
                if (property.PropertyName == name) return property.Value;
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
            string rawContents = br.GetDynamicPropertyByName(propName).ToString();
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

        public static string ReadComponentSystem(BlockReference br, FjvDynamicComponents fjv, EndType endType = default)
        {
            try
            {
                string version = br.GetAttributeStringValue("VERSION");
                string valueToReturn = fjv.System(br.RealName(), version) ?? "";

                // Special care for F-Model og Y-Rør
                string type = fjv.Type(br.RealName(), version) ?? "";

                if (endType != default && (type == "F-Model" || type == "Y-Model" || type == "H-Model"))
                {
                    if (endType == EndType.Main) return "Twin";
                    else if (endType == EndType.Branch) return "Enkelt";
                    else return "Enkelt";
                }

                if (valueToReturn.StartsWith("$"))
                {
                    valueToReturn = valueToReturn.Substring(1);
                    if (valueToReturn.Contains("{"))
                        valueToReturn = GetValueByRegex(br, "System", valueToReturn);
                    else
                        return br.GetDynamicPropertyByName(valueToReturn)?.ToString() ?? "";
                }
                return valueToReturn;
            }
            catch (System.Exception ex)
            {
                prdDbg(br.Handle);
                prdDbg(ex);
                throw;
            }
        }

    }
}
