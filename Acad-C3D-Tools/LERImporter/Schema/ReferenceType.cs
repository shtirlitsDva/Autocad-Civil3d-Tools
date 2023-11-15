using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace LERImporter.Schema
{
    public partial class ReferenceType
    {
        [XmlAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.w3.org/1999/xlink", DataType = "anyURI")]
        public string href { get; set; }
    }
}
