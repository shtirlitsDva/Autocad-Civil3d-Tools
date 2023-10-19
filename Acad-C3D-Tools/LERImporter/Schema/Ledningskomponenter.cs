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
        public string Driftsstatus { get => this.driftsstatus?.Value.GetXmlEnumAttributeValueFromEnum() ?? "i drift"; }
        [PsInclude]
        public string EtableringsTidspunkt { get => this.etableringstidspunkt?.Value; }
        [PsInclude]
        public string Fareklasse { get => this.fareklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? "ukendt"; }
        [PsInclude]
        public string LerId { get => this.lerid; }
        public string lerid { get; set; }
        [PsInclude]
        public string GmlId { get => this.GMLTypeID; }
        [PsInclude]
        public string Materiale { get => this.materiale; }
        [PsInclude]
        public string Nøjagtighedsklasse
        {
            get => this.noejagtighedsklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? "ukendt";
        }
        [PsInclude]
        public string RegistreringFra { get => this.registreringFra.ToString() ?? string.Empty; }
        [PsInclude]
        public string Sikkerhedshensyn { get => this.sikkerhedshensyn; }
        [PsInclude]
        public double VejledendeDybde { get => this.vejledendeDybde?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public string Niveau { get => this.niveau.GetXmlEnumAttributeValueFromEnum(); }
    }
    public partial class VandkomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        //public string Vandkomponent { get => this.type.GetXmlEnumAttributeValueFromEnum(); }
        public string Vandkomponent { get => this.type; }
        [PsInclude]
        public string Tapsted { get => this?.tapstedstype?.Value.ToString() ?? ""; }
        [PsInclude]
        public string Anborsted { get => this?.anborsted?.Value.ToString() ?? ""; }
        [PsInclude]
        public string Bundkote { get => this?.bundkote?.GetDouble().ToString("0.##") ?? ""; }
        [PsInclude]
        public string Stutskote { get => this?.stutskote?.GetDouble().ToString("0.##") ?? ""; }
        [PsInclude]
        public string Topkote { get => this?.topkote?.GetDouble().ToString("0.##") ?? ""; }
        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
    public partial class TermiskKomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        public string KomponentType { get => GetTermiskKomponenttypeType().ToString(); }

        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }

        public TermiskkomponenttypeType GetTermiskKomponenttypeType() => this.type;
    }
    public partial class TelekommunikationskomponentType : LedningskomponentType, ILerKomponent
    {
        public string Telekommunikationskomponenttype { get => this.type.ToString(); }
        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
    public partial class OliekomponentType : LedningskomponentType { }
    public partial class GaskomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        public string Type { get => this.type.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public double Topkote { get => this.topkote?.GetDouble() ?? default; }

        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
    public partial class ElkomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        public string Type { get => this.type.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string RelativNiveau { get => this.relativNiveau.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string SpædningsNiveau { get => this.spaendingsniveau?.Value.ToString() + this.spaendingsniveau?.uom ?? ""; }
        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
    public partial class AndenKomponentType : LedningskomponentType, ILerKomponent
    {
        [PsInclude]
        public string Forsyningsart { get => this?.forsyningsart ?? ""; }
        [PsInclude]
        public string Type { get => this?.type ?? ""; }
        public Oid DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
    public partial class AfloebskomponentType : LedningskomponentType, ILerKomponent
    {
        #region Properties to include
        [PsInclude]
        public string Type { get => this.type.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string Brøndform { get => this.broendform?.Value.ToString() ?? ""; }
        [PsInclude]
        public double Bunddiameterbredde { get => this.bunddiameterbredde?.getValueInStdUnits() ?? 0.0; }
        [PsInclude]
        public double Bundlængde { get => this.bundlaengde?.getValueInStdUnits() ?? 0.0; }
        [PsInclude]
        public double Bundkote { get => this.bundkote?.GetDouble() ?? 0.0; }
        [PsInclude]
        public double Topkote { get => this.topkote?.GetDouble() ?? 0.0; }
        [PsInclude]
        public string MedieType { get => this.getMedietype().ToString() ?? string.Empty; }
        public string medietype { get; set; }
        private MedietypeEnum getMedietype()
        {
            if (this.medietype.IsNoE())
            {
                //Log.log($"WARNING! Element id {gmlid} has NO Medietype specified!");
                return MedietypeEnum.ukendt;
            }

            MedietypeEnum type;
            if (Enum.TryParse(this.medietype, out type)) return type;
            else
            {
                Log.log($"WARNING! Element id {gmlid} has non-standard Medietype {this.medietype}!");
                return MedietypeEnum.ukendt;
            }
        }
        public enum MedietypeEnum
        {
            ukendt,
            drænvand,
            fællesvand,
            [XmlEnumAttribute("industri/procesvand")]
            industriprocesvand,
            [XmlEnumAttribute("intet medie")]
            intetmedie,
            perkolat,
            regnvand,
            spildevand,
            [XmlEnumAttribute("vand uden rensekrav")]
            vandudenrensekrav,
        }
        #endregion
        public ObjectId DrawComponent(Database database)
        {
            IEntityCreator creator = this.geometri.Item as IEntityCreator;
            return creator.CreateEntity(database);
        }
    }
}
