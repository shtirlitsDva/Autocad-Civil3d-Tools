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
    public interface ILerLedning
    {
        Oid DrawEntity2D(Database database);
        Oid DrawEntity3D(Database database);
    }
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class PsInclude : Attribute
    {

    }
    public partial class LedningEllerLedningstraceType
    {
        [PsInclude]
        public string Driftsstatus { get => this.driftsstatus.Value.GetXmlEnumAttributeValueFromEnum(); }
        public DriftsstatusType Driftsstatus2 { get => this.driftsstatus.Value; }
        [PsInclude]
        public string EtableringsTidspunkt { get => this.etableringstidspunkt?.Value; }
        [PsInclude]
        public string Fareklasse { get => this.fareklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? ""; }
        [PsInclude]
        public string Id { get => this.id; }
        [PsInclude]
        public string GmlId { get => this.GMLTypeID; }
        [PsInclude]
        public string IndtegningsMetode { get => this.indtegningsmetode.GetXmlEnumAttributeValueFromEnum(); }
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
    }
    public partial class LedningType
    {
        [PsInclude]
        public string Niveau { get => this.niveau.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string IndeholderLedning { get => this.indeholderLedninger?.Value == true ? "Sand" : "Falsk"; }
        [PsInclude]
        public string LedningsEtableringsMetode { get => this.ledningsetableringsmetode.GetXmlEnumAttributeValueFromEnum(); }
        [PsInclude]
        public string LiggerILedning { get => this.liggerILedning == true ? "Sand" : "Falsk"; }
        [PsInclude]
        public double UdvendigDiameter { get => this.udvendigDiameter?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public string UdvendigFarve { get => this.udvendigFarve != null ? string.Join(", ", this.udvendigFarve) : ""; }
        [PsInclude]
        public string UdvendigMateriale { get => this.udvendigMateriale ?? ""; }
        public Oid DrawPline2D(Database database)
        {
            IPointParser parser = this.geometri.AbstractCurve as IPointParser;

            Point3d[] points = parser.GetPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2D(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            return oid;
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
        public string GetTypeName()
        {
            return this.GetTypeName().Replace("Type", "");
        }
    }
    public partial class TelekommunikationsledningType : ILerLedning
    {
        [PsInclude]
        public string Type { get => this.type?.Value?.ToString() ?? ""; }
        private string DetermineLayerName(Database database)
        {
            #region Determine correct layer name
            string layerName;
            string driftsstatusSuffix = "";

            switch (this.Driftsstatus2)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    driftsstatusSuffix = "_UAD";
                    break;
                default:
                    throw new System.Exception(
                        $"Element id {this.gmlid} has invalid driftsstatus: {Driftsstatus}!");
            }

            switch (getTelekommunikationsledningType())
            {
                case TelekommunikationsledningTypeEnum.none:
                    layerName = "0-ERROR-TelekommunikationsledningType-none";
                    break;
                case TelekommunikationsledningTypeEnum.other:
                    layerName = "0-ERROR-TelekommunikationsledningType-other";
                    break;
                case TelekommunikationsledningTypeEnum.coaxkabel:
                    layerName = "TELE-Coax";
                    break;
                case TelekommunikationsledningTypeEnum.fiberkabel:
                    layerName = "TELE-Fiber";
                    break;
                case TelekommunikationsledningTypeEnum.kobberkabel:
                    layerName = "TELE-Kobber";
                    break;
                default:
                    layerName = "0-ERROR-TelekommunikationsledningType-other";
                    break;
            }

            layerName += driftsstatusSuffix;

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            #region Draw 2D polyline
            Polyline pline = DrawPline2D(database)
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            return pline.Id;
            #endregion
        }
        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
        public TelekommunikationsledningTypeEnum getTelekommunikationsledningType()
        {
            if (this.type == null)
            {
                Log.log($"WARNING! Element id {gmlid} has NO ElledningType specified! (holding type is null)");
                return TelekommunikationsledningTypeEnum.none;
            }
            if (this.type.Value.IsNoE())
            {
                Log.log($"WARNING! Element id {gmlid} has NO ElledningType specified! (string Value is NoE)");
                return TelekommunikationsledningTypeEnum.none;
            }

            TelekommunikationsledningTypeEnum type;
            if (Enum.TryParse(this.type.Value, out type)) return type;
            else
            {
                Log.log($"WARNING! Element id {gmlid} has non-standard ElledningType {this.type.Value}!");
                return TelekommunikationsledningTypeEnum.other;
            }
        }
        public enum TelekommunikationsledningTypeEnum
        {
            none,
            other,
            coaxkabel,
            fiberkabel,
            kobberkabel
        }
    }
    public partial class RoerledningType
    {
        [PsInclude]
        public string Tværsnitsform { get => this.tvaersnitsform?.Value ?? ""; }
        [PsInclude]
        public double UdvendigBredde { get => this.udvendigBredde?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public double UdvendigHøjde { get => this.udvendigHoejde?.getValueInStdUnits() ?? default; }
    }
    public partial class VandledningType : ILerLedning
    {
        public Oid DrawEntity2D(Database database)
        {
            throw new NotImplementedException();
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class TermiskLedningType : ILerLedning
    {
        public Oid DrawEntity2D(Database database)
        {
            throw new NotImplementedException();
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class GasledningType : ILerLedning
    {
        public Oid DrawEntity2D(Database database)
        {
            throw new NotImplementedException();
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class FoeringsroerType : ILerLedning
    {
        [PsInclude]
        public string Forsyningsart { get => getForsyningsart2.ToString(); }
        public ForsyningsartEnum getForsyningsart2 { get => getForsyningsart(forsyningsart); }
        private ForsyningsartEnum getForsyningsart(string[] forsyningsart)
        {
            if (forsyningsart.Length == 0) return ForsyningsartEnum.none;
            if (forsyningsart.Length > 1) Log.log($"WARNING! Flere forsyningsarter på Føringsrør {this.GMLTypeID}.");
            string art = forsyningsart[0];
            if (string.IsNullOrEmpty(art)) return ForsyningsartEnum.none;
            if (!forsyningsartDict.ContainsKey(art))
            {
                Log.log($"WARNING! Ledning id {this.id} have undefined Forsyningsart: {art}!");
                return ForsyningsartEnum.other;
            }
            return forsyningsartDict[art];
        }
        private static Dictionary<string, ForsyningsartEnum> forsyningsartDict = new Dictionary<string, ForsyningsartEnum>()
        {
            {"",ForsyningsartEnum.none},
            {"afløb",ForsyningsartEnum.afløb},
            {"el",ForsyningsartEnum.el},
            {"fjernvarme/fjernkøling",ForsyningsartEnum.fjernvarmefjernkøling},
            {"gas",ForsyningsartEnum.gas},
            {"olie",ForsyningsartEnum.olie},
            {"telekommunikation",ForsyningsartEnum.telekommunikation},
            {"vand",ForsyningsartEnum.vand}
        };
        private string DetermineLayerName(Database database)
        {
            #region Determine correct layer name
            string layerName;
            string suffix = "";

            switch (this.driftsstatus.Value)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    suffix = "_UAD";
                    break;
                default:
                    throw new System.Exception(
                        $"Element id {this.id} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            switch (this.getForsyningsart2)
            {
                case ForsyningsartEnum.none:
                    layerName = "0-ERROR-ForingsrørForsyningsArt-none";
                    break;
                case ForsyningsartEnum.other:
                    layerName = "0-ERROR-ForingsrørForsyningsArt-other";
                    break;
                case ForsyningsartEnum.afløb:
                    layerName = "Foringsrør-Afløb";
                    break;
                case ForsyningsartEnum.el:
                    layerName = "Foringsrør-EL";
                    break;
                case ForsyningsartEnum.fjernvarmefjernkøling:
                    layerName = "Foringsrør-FJV-FKØL";
                    break;
                case ForsyningsartEnum.gas:
                    layerName = "Foringsrør-Gas";
                    break;
                case ForsyningsartEnum.olie:
                    layerName = "Foringsrør-Olie";
                    break;
                case ForsyningsartEnum.telekommunikation:
                    layerName = "Foringsrør-Telekommunikation";
                    break;
                case ForsyningsartEnum.vand:
                    layerName = "Foringsrør-Vand";
                    break;
                default:
                    layerName = "0-ERROR-ForingsrørForsyningsArt-other";
                    break;
            }

            layerName += suffix;
            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Polyline pline = DrawPline2D(database).Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.UdvendigDiameter;

            return pline.ObjectId;
        }
        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
        public enum ForsyningsartEnum
        {
            none,
            other,
            afløb,
            el,
            fjernvarmefjernkøling,
            gas,
            olie,
            telekommunikation,
            vand
        }
    }
    public partial class AfloebsledningType : ILerLedning
    {
        [PsInclude]
        public bool HarFod { get => this.harFod?.Value ?? false; }
        [PsInclude]
        public LedningstransporttypeType? LedningstransportType { get => this.ledningstransporttype?.Value; }
        private string DetermineLayerName(Database database)
        {
            string layerName = "Afløbsledning";
            string driftsstatusSuffix = "";

            switch (this.Driftsstatus2)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    driftsstatusSuffix = "_UAD";
                    break;
                default:
                    throw new System.Exception(
                        $"Element id {this.id} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            if (driftsstatusSuffix.IsNotNoE())
            {
                layerName = layerName + driftsstatusSuffix;
            }

            database.CheckOrCreateLayer(layerName);
            return layerName;
        }
        public Oid DrawEntity2D(Database database)
        {
            Polyline pline = DrawPline2D(database)
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            pline.ConstantWidth = this.UdvendigDiameter != default ? this.UdvendigDiameter : 0.011;

            return pline.Id;
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class LedningUkendtForsyningsartType : ILerLedning
    {
        public Oid DrawEntity2D(Database database)
        {
            throw new NotImplementedException();
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }
    public partial class ElledningType : ILerLedning
    {
        [PsInclude]
        public string Type { get => Type2.ToString(); }
        [PsInclude]
        public string Afdækning { get => this.afdaekning; }
        [PsInclude]
        public Int32 AntalKabler { get => this.antalKabler; }
        //[XmlElement(DataType = "integer")]
        public Int32 antalKabler { get; set; }
        [PsInclude]
        public string KabelType { get => this.kabeltype; }
        [PsInclude]
        public string SpædningsNiveau { get => this.spaendingsniveau?.Value.ToString() + this.spaendingsniveau?.uom ?? ""; }
        public ElledningTypeEnum Type2 { get => getElledningTypeType(); }
        private ElledningTypeEnum getElledningTypeType()
        {
            if (this.type.Value.IsNoE())
            {
                Log.log($"WARNING! Element id {gmlid} has NO ElledningType specified!");
                return ElledningTypeEnum.none;
            }

            ElledningTypeEnum type;
            if (Enum.TryParse(this.type.Value, out type)) return type;
            else
            {
                Log.log($"WARNING! Element id {gmlid} has non-standard ElledningType {this.type.Value}!");
                return ElledningTypeEnum.other;
            }
        }
        private string DetermineLayerName(Database database)
        {
            #region Determine correct layer name
            string layerName;
            string driftsstatusSuffix = "";

            switch (this.Driftsstatus2)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    driftsstatusSuffix = "_UAD";
                    break;
                default:
                    throw new System.Exception(
                        $"Element id {this.id} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            switch (getElledningTypeType())
            {
                case ElledningTypeEnum.none:
                    layerName = "0-ERROR-ElledningType-none";
                    break;
                case ElledningTypeEnum.other:
                    layerName = "0-ERROR-ElledningType-other";
                    break;
                case ElledningTypeEnum.beskyttelsesleder:
                    layerName = "EL-Beskyttelsesleder";
                    break;
                case ElledningTypeEnum.forsyningskabel:
                    layerName = "EL-Forsyningskabel";
                    break;
                case ElledningTypeEnum.luftledning:
                    layerName = "EL-Luftledning";
                    break;
                case ElledningTypeEnum.stikkabel:
                    layerName = "EL-Stikkabel";
                    break;
                case ElledningTypeEnum.vejbelysningskabel:
                    layerName = "EL-Vejbelysningskabel";
                    break;
                default:
                    layerName = "0-ERROR-ElledningType-other";
                    break;
            }

            if (SpædningsNiveau.IsNotNoE() &&
                SpædningsNiveau != "0kV") layerName += $"-{SpædningsNiveau}";
            layerName += driftsstatusSuffix;

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            #region Draw 2D polyline
            Polyline pline = DrawPline2D(database)
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            if (this.spaendingsniveau != null && this.spaendingsniveau?.Value > 30.0)
                pline.ConstantWidth = 0.5;

            return pline.Id;
            #endregion
        }
        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
        public enum ElledningTypeEnum
        {
            none,
            other,
            beskyttelsesleder,
            forsyningskabel,
            luftledning,
            stikkabel,
            vejbelysningskabel
        }
    }
    public partial class AndenLedningType : ILerLedning
    {
        public Oid DrawEntity2D(Database database)
        {
            throw new NotImplementedException();
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
    }

    #region Special treatment for LedningsTrace
    public partial class LedningstraceType : ILerLedning
    {
        [PsInclude]
        public double Bredde { get => this.bredde?.Value ?? 0; }
        [PsInclude]
        public string Forsyningsart { get => getForsyningsart(this.forsyningsart).ToString(); }
        [PsInclude]
        public string IndeholdtLedning
        {
            get
            {
                if (this.indeholdtLedning == null) return "Ingen";
                string s = "";
                foreach (ReferenceType rt in this.indeholdtLedning)
                {
                    s += rt.owns.ToString() + " ";
                    s += rt.remoteSchema.ToString();
                }
                return s;
            }
        }
        private ForsyningsartEnum getForsyningsart(LedningstraceTypeForsyningsart[] forsyningsart)
        {
            if (forsyningsart.Length == 0) return ForsyningsartEnum.none;
            if (forsyningsart.Length > 1) Log.log($"WARNING! Flere forsyningsarter på Føringsrør {this.GMLTypeID}.");
            string art = forsyningsart[0].Value;
            if (string.IsNullOrEmpty(art)) return ForsyningsartEnum.none;
            if (!forsyningsartDict.ContainsKey(art))
            {
                Log.log($"WARNING! Ledning id {this.id} have undefined Forsyningsart: {art}!");
                return ForsyningsartEnum.other;
            }
            
            return forsyningsartDict[art];
        }

        private string DetermineLayerName(Database database)
        {
            #region Determine correct layer name
            string layerName;
            string suffix = "";

            switch (this.driftsstatus.Value)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    suffix = "_UAD";
                    break;
                default:
                    throw new System.Exception(
                        $"Element id {this.id} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            switch (this.getForsyningsart(this.forsyningsart))
            {
                case ForsyningsartEnum.none:
                    layerName = "0-ERROR-LedningstraceForsyningsArt-none";
                    break;
                case ForsyningsartEnum.other:
                    layerName = "0-ERROR-LedningstraceForsyningsArt-other";
                    break;
                case ForsyningsartEnum.afløb:
                    layerName = "Ledningstrace-Afløb";
                    break;
                case ForsyningsartEnum.el:
                    layerName = "Ledningstrace-EL";
                    break;
                case ForsyningsartEnum.fjernvarmefjernkøling:
                    layerName = "Ledningstrace-FJV-FKØL";
                    break;
                case ForsyningsartEnum.gas:
                    layerName = "Ledningstrace-Gas";
                    break;
                case ForsyningsartEnum.olie:
                    layerName = "Ledningstrace-Olie";
                    break;
                case ForsyningsartEnum.telekommunikation:
                    layerName = "Ledningstrace-Telekommunikation";
                    break;
                case ForsyningsartEnum.vand:
                    layerName = "Ledningstrace-Vand";
                    break;
                default:
                    layerName = "0-ERROR-LedningstraceForsyningsArt-other";
                    break;
            }

            layerName += suffix;
            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }

        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Polyline pline = DrawPline2D(database).Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.Bredde;

            return pline.ObjectId;
        }

        public Oid DrawPline2D(Database database)
        {
            IPointParser parser = this.geometri.MultiCurve as IPointParser;

            Point3d[] points = parser.GetPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2D(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            return oid;
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
        }
        private static Dictionary<string, ForsyningsartEnum> forsyningsartDict = new Dictionary<string, ForsyningsartEnum>()
        {
            {"",ForsyningsartEnum.none},
            {"afløb",ForsyningsartEnum.afløb},
            {"el",ForsyningsartEnum.el},
            {"fjernvarme/fjernkøling",ForsyningsartEnum.fjernvarmefjernkøling},
            {"gas",ForsyningsartEnum.gas},
            {"olie",ForsyningsartEnum.olie},
            {"telekommunikation",ForsyningsartEnum.telekommunikation},
            {"vand",ForsyningsartEnum.vand}
        };
        public enum ForsyningsartEnum
        {
            none,
            other,
            afløb,
            el,
            fjernvarmefjernkøling,
            gas,
            olie,
            telekommunikation,
            vand
        }
    }
    #endregion
}
