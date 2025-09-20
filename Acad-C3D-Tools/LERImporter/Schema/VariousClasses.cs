using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Log = LERImporter.SimpleLogger;

namespace LERImporter.Schema
{
    public partial class MeasureType
    {
        /// <summary>
        /// Should always return value in standard units.
        /// Standard units are:
        /// Meters, degrees, (add others as they come)
        /// </summary>
        public double getValueInStdUnits()
        {
            if (string.IsNullOrEmpty(this.uom))
            {
                throw new Exception("No units specified in MeasureType: uom is NoE!");
            }

            UnitsEnum units;
            if (Enum.TryParse(this.uom, out units))
            {
                switch (units)
                {
                    case UnitsEnum.None:
                        throw new Exception($"Non defined units in MeasureType: uom = {this.uom}!");
                    case UnitsEnum.mm:
                        return this.Value < 0 ? -this.Value / 1000 : this.Value / 1000;
                    case UnitsEnum.m:
                    case UnitsEnum.bar:
                        return this.Value < 0 ? -this.Value : this.Value;
                    default:
                        throw new Exception($"Non defined units in MeasureType: uom = {this.uom}!");
                }
            }
            else
            {
                throw new Exception($"Non defined units in MeasureType: uom = {this.uom}!");
            }
        }
        /// <summary>
        /// Returns value as stored in gml.
        /// </summary>
        public double getValue() => this.Value;
        public string getMeasureUnitName()
        {
            if (string.IsNullOrEmpty(this.uom))
            {
                throw new Exception("No units specified in MeasureType: uom is NoE!");
            }
            return this.uom;
        }
    }
    public enum UnitsEnum
    {
        None,
        mm,
        m,
        bar
    }
    [XmlRootAttribute("AbstractGML", Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public abstract partial class AbstractGMLType
    {
        public string GMLTypeID { get => this.gmlid; }
        [XmlAttribute(
            Form = System.Xml.Schema.XmlSchemaForm.Qualified,
            AttributeName = "id",
            DataType = "ID",
            Namespace = "http://www.opengis.net/gml/3.2"
            )]
        public string gmlid { get; set; }
    }
}
