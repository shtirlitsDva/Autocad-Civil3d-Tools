﻿using Autodesk.AutoCAD.ApplicationServices;
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

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


namespace IntersectUtilities
{
    internal class IsogenPopulateAttributes
    {
        public static void WriteIsogenAttrubutesToDwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Instantiate serializer
            XmlSerializer serializer = new XmlSerializer(typeof(BackingSheetData));

            // Declare an object variable of the type to be deserialized.
            BackingSheetData bsd;

            using (Stream reader = new FileStream(@"X:\AC - Iso\IsoDir\1189\DRI_VEKS_ASBUILT\DRI_VEKS_ASBUILT.BDF", FileMode.Open))
            {
                // Call the Deserialize method to restore the object's state.
                bsd = (BackingSheetData)serializer.Deserialize(reader);
            }

            #region Dialog box for selecting the PCF file
            string fileName;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Choose PCF file:",
                DefaultExt = "pcf",
                Filter = "PCF files (*.pcf)|*.pcf|All files (*.*)|*.*",
                FilterIndex = 0
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                fileName = dialog.FileName;
            }
            else { throw new System.Exception("Cannot find BBR file!"); }
            #endregion

            string[] pcf = File.ReadAllLines(fileName);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockReference tb = localDb.GetBlockReferenceByName("Title Block").FirstOrDefault();

                    if (tb == null) { prdDbg("Title Block not found!"); tx.Abort(); return; }

                    bsd.ParseData(pcf);

                    foreach (var item in bsd.AttributeMapping)
                    {
                        if (item.FinalData.IsNoE()) continue;
                        prdDbg($"{item.BlockAttribute} > {item.FinalData}");
                        tb.SetAttributeStringValue(item.BlockAttribute, item.FinalData);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
    }


    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class BackingSheetData
    {
        [System.Xml.Serialization.XmlArrayItemAttribute("Attribute", IsNullable = false)]
        public BackingSheetDataAttribute[] AttributeMapping { get; set; }

        internal void ParseData(string[] pcf)
        {
            foreach (var attributeMap in AttributeMapping)
            {
                if (attributeMap.IsogenAttribute != "PIPELINE-REFERENCE")
                    attributeMap.IsogenAttribute = "    " + attributeMap.IsogenAttribute + " ";
                else attributeMap.IsogenAttribute += " ";

                foreach (string data in pcf)
                    if (data.StartsWith(attributeMap.IsogenAttribute))
                        attributeMap.FinalData = data.Replace(attributeMap.IsogenAttribute, "");
            }
        }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class BackingSheetDataAttribute
    {

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string IsogenAttribute { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string OutputAttribute { get; set; }
        public string BlockAttribute { get => OutputAttribute.Split('»')[1]; }
        public string FinalData { get; set; }
    }


}
