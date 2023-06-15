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
                // Check if this elkomponent has the offending enum value
                if (elkomponentXml.Element(ler + "type").Value == "other: føringsrør")
                {
                    elkomponentXml.Element(ler + "type").Value = "muffe";

                    using (var reader = elkomponentXml.CreateReader())
                    {
                        elkomponentInstance = (ElkomponentType)deserializer.Deserialize(reader);
                    }

                    if (elkomponentInstance == null)
                        throw new System.Exception("Micro deserialization of Elkomponent failed!");

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
                        geometri = new CurvePropertyType
                        {
                            AbstractCurve =
                             (LineStringType)elkomponentInstance.geometri.Item,
                        },
                    };

                    XElement newFoeringsroer;
                    using (var memStream = new MemoryStream())
                    {
                        serializer.Serialize(memStream, foeringsroerInstance);
                        memStream.Position = 0;
                        newFoeringsroer = XElement.Load(memStream);
                    }

                    elkomponentXml.ReplaceWith(newFoeringsroer);
                    IntersectUtilities.UtilsCommon.Utils.prdDbg(foeringsroerInstance.GmlId);
                }
            }

            return doc;
        }
    }
}
