using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
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
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IsoTools
{
    internal class IsogenPopulateAttributes
    {
        public static void WriteIsogenAttrubutesToDwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //Instantiate serializer
            XmlSerializer serializer = new XmlSerializer(typeof(BackingSheetData));

            // Declare an object variable of the type to be deserialized.
            BackingSheetData bsd;

            using (StreamReader reader = new StreamReader(
                new FileStream(
                    @"X:\AC - Iso\IsoDir\DRI\DRI-CUTLIST\DRI-CUTLIST.BDF", FileMode.Open),
                Encoding.Default))
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
            else { throw new System.Exception("Cannot find PCF file!"); }
            #endregion

            string[] pcf = File.ReadAllLines(fileName, Encoding.Default);

            var pcfPipeLineData = IsogenPopulateAttributes.CollectPipeLineData(pcf);

            string kwd = StringGridFormCaller.Call(
                pcfPipeLineData.Select(x => x.Key), "Select pipeline to populate: ");

            if (kwd.IsNoE()) return;

            Dictionary<string, string> data = pcfPipeLineData[kwd];

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var tb = localDb.ListOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == "Title Block")
                        .FirstOrDefault();

                    if (tb == null) { prdDbg("Title Block not found!"); tx.Abort(); return; }

                    foreach (var item in bsd.AttributeMapping)
                    {
                        if (!data.ContainsKey(item.IsogenAttribute)) continue;

                        prdDbg($"{item.BlockAttribute} > {data[item.IsogenAttribute]}");
                        tb.SetAttributeStringValue(item.BlockAttribute, data[item.IsogenAttribute]);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        private static Dictionary<string, Dictionary<string, string>> CollectPipeLineData(string[] pcf)
        {
            Dictionary<string, Dictionary<string, string>> col = new Dictionary<string, Dictionary<string, string>>();

            bool keepElement = false;
            string curElement = "";

            HashSet<string> elementsToKeep = new HashSet<string>()
        {
            "PIPELINE-REFERENCE"
        };

            //The idea is to determine if element is kept
            //Then populate the dict with it's properties
            //If the element is not kept, the lines are skipped
            //Until next element

            Dictionary<string, string> dict = default;
            for (int i = 0; i < pcf.Length; i++)
            {
                string curLine = pcf[i];

                if (!curLine.StartsWith("    "))
                {
                    if (elementsToKeep.Any(x => curLine.StartsWith(x)))
                    {
                        if (dict != default) col.Add(dict[curElement].Replace(' ', '_'), dict);
                        dict = new Dictionary<string, string>();
                        var data = ExtractAttributeAndValue(curLine);
                        //prdDbg($"{data.Key} > {data.Value}");
                        dict.Add(data.Key, data.Value);
                        curElement = data.Key;
                        keepElement = true;
                    }
                    else keepElement = false;
                }
                else if (curLine.StartsWith("    "))
                {
                    if (!keepElement) continue;

                    var data = ExtractAttributeAndValue(curLine.TrimStart());
                    dict.Add(data.Key, data.Value);
                }
            }
            col.Add(dict[curElement].Replace(' ', '_'), dict);
            return col;

            KeyValuePair<string, string> ExtractAttributeAndValue(string input)
            {
                // Split the string based on the first space character
                string[] parts = input.Split(new char[] { ' ' }, 2);

                string attributeName = parts[0];
                string value;

                // If there's a second part, assign it to the value variable
                if (parts.Length > 1)
                {
                    value = parts[1].Trim(); // Remove any additional whitespace
                }
                else
                {
                    value = "";
                }

                return new KeyValuePair<string, string>(attributeName, value);
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