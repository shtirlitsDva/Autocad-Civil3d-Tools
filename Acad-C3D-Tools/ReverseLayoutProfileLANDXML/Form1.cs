using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Xml;
using System.Xml.Serialization;
using System.Globalization;

namespace ReverseLayoutProfileLANDXML
{
    public partial class Form1 : Form
    {
        string fileName = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\10 Actionlist";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                fileName = dialog.FileName;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            if (!File.Exists(fileName)) return;

            XmlSerializer serializer = new XmlSerializer(typeof(LandXML));
            LandXML c3dXML;

            using (Stream reader = new FileStream(fileName, FileMode.Open))
            {
                c3dXML = (LandXML)serializer.Deserialize(reader);
            }

            decimal alLength = c3dXML.Alignments.Alignment.length;

            foreach (LandXMLAlignmentsAlignmentProfileProfAlign profile in c3dXML.Alignments.Alignment.Profile.ProfAlign)
            {
                int piL = profile.Items.Length;
                object[] reversedArray = new object[piL];

                for (int i = 0; i < profile.Items.Length; i++)
                {
                    switch (profile.Items[i])
                    {
                        case string str:
                            reversedArray[piL - 1 - i] = ParseAndModifyValueString(alLength, str);
                            break;
                        case LandXMLAlignmentsAlignmentProfileProfAlignCircCurve circCurve:
                            circCurve.Value = ParseAndModifyValueString(alLength, circCurve.Value);
                            reversedArray[piL - 1 - i] = circCurve;
                            break;
                        case LandXMLAlignmentsAlignmentProfileProfAlignParaCurve paraCurve:
                            paraCurve.Value = ParseAndModifyValueString(alLength, paraCurve.Value);
                            reversedArray[piL - 1 - i] = paraCurve;
                            break;
                        default:
                            break;
                    }
                }
                profile.Items = reversedArray;
            }

            string exportFileName = $"{fileName.Substring(0, fileName.Length - 4)}_reversed.xml";
            Stream fs = new FileStream(exportFileName, FileMode.Create);
            XmlWriter writer = new XmlTextWriter(fs, Encoding.Unicode) {Formatting = Formatting.Indented};
            
            // Serialize using the XmlTextWriter.
            serializer.Serialize(writer, c3dXML);
            writer.Close();

            string ParseAndModifyValueString(decimal length, string str)
            {
                string[] output = str.Split((char[])null); //Splits by whitespace
                decimal originalStation;
                if (decimal.TryParse(output[0], NumberStyles.Any, CultureInfo.InvariantCulture, out originalStation))
                {
                    decimal newStation = length - originalStation;
                    return $"{newStation.ToString(CultureInfo.InvariantCulture)} {output[1]}";
                }
                else throw new Exception($"TryParse failed for: {str}.");
            }
        }
    }
}
