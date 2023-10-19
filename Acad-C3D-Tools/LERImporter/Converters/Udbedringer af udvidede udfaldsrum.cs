using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

using LERImporter.Schema;

namespace LERImporter.Converters
{
    internal static class Converter_Ledningstrace_TDC500mmTraceAsZeroWidth
    {
        /// <summary>
        /// Sets any Ledningstrace found with 500 mm width to 0.
        /// </summary>
        internal static XDocument Convert(XDocument doc)
        {
            var ler = XNamespace.Get("http://data.gov.dk/schemas/LER/2/gml");

            var items = doc.Descendants(ler + "Ledningstrace").ToList();

            foreach (var item in items)
            {
                var breddeElement = item.Element(ler + "bredde");
                if (breddeElement != null && breddeElement.Value == "500")
                {
                    breddeElement.Value = "0";
                }
            }

            return doc;
        }
    }
    internal static class Converter_TermiskKomponent_HandleNonStandardValuesForEnums
    {
        internal static XDocument Convert(XDocument doc)
        {
            var gml = XNamespace.Get("http://www.opengis.net/gml/3.2");
            var ler = XNamespace.Get("http://data.gov.dk/schemas/LER/2/gml");

            var items = doc.Descendants(ler + "TermiskKomponent").ToList();

            foreach ( var item in items )
            {
                //Fix non-existing enum values, MUST RUN AFTER OTHER CONVERTERS
                if (!Enum.IsDefined(typeof(TermiskkomponenttypeType), item.Element(ler + "type").Value))
                {
                    item.Element(ler + "type").Value = "custom";
                }
            }

            return doc;
        }
    }
    internal static class Converter_Cerius_ElkomponentToFoeringsroer
    {
        internal static XDocument Convert(XDocument doc)
        {
            var gml = XNamespace.Get("http://www.opengis.net/gml/3.2");
            var ler = XNamespace.Get("http://data.gov.dk/schemas/LER/2/gml");

            // Find the offending elements
            var elkomponents = doc.Descendants(ler + "Elkomponent").ToList();

            var deserializer = new XmlSerializer(typeof(ElkomponentType));
            var serializer = new XmlSerializer(typeof(FoeringsroerType));

            ElkomponentType elkomponentInstance;
            foreach (var elkomponentXml in elkomponents)
            {
                // Check if this elkomponent is Cerius føringsrør
                if (elkomponentXml.Element(ler + "type").Value == "other: føringsrør")
                {
                    elkomponentXml.Element(ler + "type").Value = "muffe";

                    using (var reader = elkomponentXml.CreateReader())
                    {
                        elkomponentInstance = (ElkomponentType)deserializer.Deserialize(reader);
                    }

                    if (elkomponentInstance == null)
                        throw new System.Exception("Micro deserialization of Elkomponent failed!");
                    IntersectUtilities.UtilsCommon.Utils.prdDbg(elkomponentInstance.GmlId);

                    var foeringsroerInstance = new FoeringsroerType
                    {
                        forsyningsart = new string[] { "el" },
                        ledningsejer = elkomponentInstance.ledningsejer,
                        indberetningsNr = elkomponentInstance.indberetningsNr,
                        driftsstatus = elkomponentInstance.driftsstatus,
                        etableringstidspunkt = elkomponentInstance.etableringstidspunkt,
                        fareklasse = elkomponentInstance.fareklasse,
                        lerid = elkomponentInstance.lerid,
                        gmlid = elkomponentInstance.gmlid,
                        noejagtighedsklasse = elkomponentInstance.noejagtighedsklasse,
                    };

                    if (elkomponentInstance.geometri.Item is LineStringType lst)
                    {
                        foeringsroerInstance.geometri = new GeometryPropertyType
                        {
                            Item = lst
                        };
                    }
                    else if (elkomponentInstance.geometri.Item is PolygonType pt)
                    {
                        foeringsroerInstance.geometri = new GeometryPropertyType
                        {
                            Item = pt
                        };
                    }

                    XElement newFoeringsroer;
                    using (var memStream = new MemoryStream())
                    {
                        serializer.Serialize(memStream, foeringsroerInstance);
                        memStream.Position = 0;
                        newFoeringsroer = XElement.Load(memStream);
                    }

                    elkomponentXml.ReplaceWith(newFoeringsroer);
                }

                //Fix Cerius non-existing enum values, MUST RUN AFTER OTHER CONVERTERS
                else if (!Enum.IsDefined(typeof(ElkomponenttypeType), elkomponentXml.Element(ler + "type").Value))
                {
                    elkomponentXml.Element(ler + "type").Value = "custom";
                }
            }

            return doc;
        }
    }
}
