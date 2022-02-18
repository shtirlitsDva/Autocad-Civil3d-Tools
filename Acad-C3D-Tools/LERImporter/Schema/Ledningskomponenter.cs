using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon;

namespace LERImporter.Schema
{
    public abstract partial class LedningskomponentType 
    {
        [PsInclude]
        public string Driftsstatus { get => this.driftsstatus.Value.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string EtableringsTidspunkt { get => this.etableringstidspunkt?.Value; }
        [PsInclude]
        public string Fareklasse { get => this.fareklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? "ukendt"; }
        [PsInclude]
        public string Id { get => this.id; }
        [PsInclude]
        public string GmlId { get => this.GMLTypeID; }
        [PsInclude]
        public string Materiale { get => this.materiale; }
        [PsInclude]
        public string Nøjagtighedsklasse { 
            get => this.noejagtighedsklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? "ukendt"; }
        [PsInclude]
        public string RegistreringFra { get => this.registreringFra.ToString() ?? string.Empty; }
        [PsInclude]
        public string Sikkerhedshensyn { get => this.sikkerhedshensyn; }
        [PsInclude]
        public double VejledendeDybde { get => this.vejledendeDybde?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public string Niveau { get => this.niveau.GetXmlEnumAttributeValueFromEnum(); }
    }
    public partial class VandkomponentType : LedningskomponentType { }
    public partial class TermiskKomponentType : LedningskomponentType { }
    public partial class TelekommunikationskomponentType : LedningskomponentType { }
    public partial class OliekomponentType : LedningskomponentType { }
    public partial class GaskomponentType : LedningskomponentType { }
    public partial class ElkomponentType : LedningskomponentType { }
    public partial class AndenKomponentType : LedningskomponentType { }
    public partial class AfloebskomponentType : LedningskomponentType { }
}
