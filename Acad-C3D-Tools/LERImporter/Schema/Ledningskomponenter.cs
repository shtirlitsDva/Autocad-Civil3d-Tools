using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
using Autodesk.Civil.DataShortcuts;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using PsDataType = Autodesk.Aec.PropertyData.DataType;
using Log = LERImporter.SimpleLogger;

namespace LERImporter.Schema
{
    public interface ILerKomponent
    {
        Oid DrawComponent(Database database);
    }
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
    public partial class ElkomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        public string Type { get => this.type.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string RelativNiveau { get => this.relativNiveau.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string SpædningsNiveau { get => this.spaendingsniveau.Value.ToString() + this.spaendingsniveau.uom; }

        public Oid DrawComponent(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class AndenKomponentType : LedningskomponentType { }
    public partial class AfloebskomponentType : LedningskomponentType { }
}
