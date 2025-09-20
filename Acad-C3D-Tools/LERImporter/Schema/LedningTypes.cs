﻿using System;
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
//using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

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
using static LERImporter.Schema.ElledningType;
using static LERImporter.Schema.TermiskLedningType;

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
        public string Driftsstatus { get => this.driftsstatus?.Value.GetXmlEnumAttributeValueFromEnum() ?? "i drift"; }
        public DriftsstatusType Driftsstatus2 { get => this.driftsstatus?.Value ?? DriftsstatusType.idrift; }
        [PsInclude]
        public string EtableringsTidspunkt { get => this.etableringstidspunkt?.Value; }
        [PsInclude]
        public string Fareklasse { get => this.fareklasse?.Value.GetXmlEnumAttributeValueFromEnum() ?? ""; }
        [PsInclude]
        public string GmlId { get => this.GMLTypeID; }
        [PsInclude]
        public string LerId { get => this.lerid; }
        public string lerid { get; set; }
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
        public double UdvendigDiameter { get => this.udvendigDiameter?.getValue() ?? default; }
        [PsInclude]
        public string UdvendigDiameterUnits { get => this.udvendigDiameter?.getMeasureUnitName() ?? default; }
        public double UdvendigDiameterInStdUnits { get => this.udvendigDiameter?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public string UdvendigFarve { get => this.udvendigFarve != null ? string.Join(", ", this.udvendigFarve) : ""; }
        [PsInclude]
        public string UdvendigMateriale { get => this.udvendigMateriale ?? ""; }
        public Oid DrawPline2D(Database database)
        {
            if (this.geometri == null)
            {
                Log.log($"ADVARSEL! Element id {this.LerId} har geometri null! Springer over!");
                return Oid.Null;
            }

            //IPointParser parser = this.geometri.AbstractCurve as IPointParser;
            IPointParser parser = this.geometri.Item as IPointParser;

            Point3d[] points = parser.Get3DPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2d(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            return oid;
        }
        public Oid DrawPline3D(Database database)
        {
            //Logging is already made in the 2D part of the program
            if (this.geometri == null) return Oid.Null;

            //IPointParser parser = this.geometri.AbstractCurve as IPointParser;
            IPointParser parser = this.geometri.Item as IPointParser;

            Point3d[] points = parser.Get3DPoints();
            Point3dCollection col = new Point3dCollection(points);

            Polyline3d p3d = new Polyline3d(Poly3dType.SimplePoly, col, false);

            Oid oid = p3d.AddEntityToDbModelSpace(database);

            return oid;
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
        private string DetermineLayerName(Database database, bool _3D = false)
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
                case TelekommunikationsledningTypeEnum.ukendt:
                    layerName = "TELE-Ukendt";
                    break;
                case TelekommunikationsledningTypeEnum.andet:
                    layerName = "TELE-Andet";
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
                    layerName = "0-ERROR-TelekommunikationsledningType";
                    break;
            }

            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            #region Draw 2D polyline
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id.Go<Polyline>(
                database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            return pline.Id;
            #endregion
        }
        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;

            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
        public TelekommunikationsledningTypeEnum getTelekommunikationsledningType()
        {
            if (this.type == null)
            {
                //Log.log($"WARNING! Element id {gmlid} has NO TelekommunikationsledningType specified! (holding type is null)\n" +
                //    $"Typen angives som UKENDT!");
                return TelekommunikationsledningTypeEnum.ukendt;
            }
            if (this.type.Value.IsNoE())
            {
                //Log.log($"WARNING! Element id {gmlid} has NO TelekommunikationsledningType specified! (string Value is NoE)\n" +
                //    $"Typen angives som UKENDT!");
                return TelekommunikationsledningTypeEnum.ukendt;
            }

            TelekommunikationsledningTypeEnum type;
            if (Enum.TryParse(this.type.Value, out type)) return type;
            else
            {
                Log.log($"WARNING! Element id {gmlid} has non-standard TelekommunikationsledningType {this.type.Value}!");
                return TelekommunikationsledningTypeEnum.andet;
            }
        }
        public enum TelekommunikationsledningTypeEnum
        {
            ukendt,
            andet,
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
        private string DetermineLayerName(Database database, bool _3D = false)
        {
            #region Determine correct layer name
            string layerName = "Vandledning_L2";
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
                    Log.log($"Vandledning {this.GmlId} har driftsstatus lig med null! Sætter til \"i drift\".");
                    break;
            }

            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id.Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits;

            return pline.ObjectId;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }
    public partial class TermiskLedningType : ILerLedning
    {
        [PsInclude]
        public string Termiskledningstype { get => GetTermiskledningType().ToString(); }
        [PsInclude]
        public string Indhold { get => GetTermiskledningsindholdstypeType().ToString(); }
        [PsInclude]
        public string Konstruktion { get => GetTermiskledningkonstruktiontypeType().ToString(); }
        [PsInclude]
        public string Temperatur { get => this.temperatur?.Value + this.temperatur?.uom ?? "Ukendt"; }
        [PsInclude]
        public string Tryk { get => this.tryk?.getValueInStdUnits() + this.tryk?.uom ?? "Ukendt"; }

        TermiskledningstypeType GetTermiskledningType() => this.type?.Value ?? TermiskledningstypeType.produktionsledning;
        TermiskledningsindholdstypeType GetTermiskledningsindholdstypeType() => this.indhold;
        TermiskledningkonstruktiontypeType GetTermiskledningkonstruktiontypeType() => this.konstruktion;
        private string DetermineLayerName(Database database, bool _3D = false)
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            switch (GetTermiskledningsindholdstypeType())
            {
                case TermiskledningsindholdstypeType.damp:
                    layerName = "Damp";
                    break;
                case TermiskledningsindholdstypeType.koldtvand:
                    layerName = "Køl";
                    break;
                case TermiskledningsindholdstypeType.varmtvand:
                    layerName = "Fjv";
                    break;
                default:
                    layerName = "TermiskLedn";
                    break;
            }

            switch (GetTermiskledningType())
            {
                case TermiskledningstypeType.produktionsledning:
                    layerName += "-Prod";
                    break;
                case TermiskledningstypeType.transmissionsledning:
                    layerName += "-Trans";
                    break;
                case TermiskledningstypeType.distributionsledning:
                    layerName += "-Distr";
                    break;
                case TermiskledningstypeType.stikledning:
                    layerName += "-Stik";
                    break;
                default:
                    layerName += "-Ukendt";
                    break;
            }

            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits;

            return pline.ObjectId;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }

        /// <summary>
        /// inddeling af indhold i termiske ledninger
        /// </summary>
        [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8.4084.0")]
        [Serializable]
        [XmlTypeAttribute(Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        public enum TermiskledningsindholdstypeType
        {
            /// <summary>
            /// fjernvarmevand
            /// </summary>
            [XmlEnumAttribute("varmt vand")]
            varmtvand,
            /// <summary>
            /// varmt vand på dampform
            /// </summary>
            damp,
            /// <summary>
            /// vand til fjernkøling
            /// </summary>
            [XmlEnumAttribute("koldt vand")]
            koldtvand,
            glykol,
            sprit
        }
    }
    public partial class GasledningType : ILerLedning
    {
        [PsInclude]
        public string Type { get => this.type?.Value ?? "Ukendt"; }
        [PsInclude]
        public string Tryk { get => this.tryk?.getValueInStdUnits() + this.tryk?.uom ?? "Ukendt"; }

        private string DetermineLayerName(Database database, bool _3D = false)
        {
            #region Determine correct layer name
            string layerName = "GAS-";
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus}!");
            }

            switch (Type)
            {
                case "transmissionsledning":
                    layerName += "Transmissionsledning";
                    break;
                case "distributionsledning":
                    layerName += "Distributionsledning";
                    break;
                case "fordelingsledning":
                    layerName += "Fordelingsledning";
                    break;
                case "stikledning":
                    layerName += "Stikledning";
                    break;
                case "Ukendt":
                    layerName += "Ukendt";
                    break;
                default:
                    layerName += "Ukendt";
                    break;
            }

            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }

        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits;

            return pline.ObjectId;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }
    public partial class FoeringsroerType : ILerLedning
    {
        [PsInclude]
        public string Forsyningsart { get => getForsyningsart2.ToString(); }
        public ForsyningsartEnum getForsyningsart2 { get => getForsyningsart(forsyningsart); }
        private ForsyningsartEnum getForsyningsart(string[] forsyningsart)
        {
            if (forsyningsart == null) return ForsyningsartEnum.none;
            if (forsyningsart.Length == 0) return ForsyningsartEnum.none;
            if (forsyningsart.Length > 1) Log.log($"WARNING! Flere forsyningsarter på Føringsrør {this.GMLTypeID}.");
            string art = forsyningsart[0];
            if (string.IsNullOrEmpty(art)) return ForsyningsartEnum.none;
            if (!forsyningsartDict.ContainsKey(art))
            {
                Log.log($"WARNING! Ledning id {this.GmlId} have undefined Forsyningsart: {art}!");
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
        private string DetermineLayerName(Database database, bool _3D = false)
        {
            #region Determine correct layer name
            string layerName;
            string suffix = "";

            if (this.driftsstatus != null)
            {
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
                            $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus}!");
                }
            }

            switch (this.getForsyningsart2)
            {
                case ForsyningsartEnum.none:
                    layerName = "Foringsrør-none";
                    break;
                case ForsyningsartEnum.other:
                    layerName = "Foringsrør-other";
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
                    layerName = "0-ERROR-ForingsrørForsyningsArt-undefined";
                    break;
            }

            layerName += suffix;
            if (_3D) layerName += "-3D";
            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            try
            {
                pline.ConstantWidth = this.UdvendigDiameterInStdUnits;
            }
            catch (System.Exception)
            {
                prdDbg($"Setting of ConstantWidt failed for {this.gmlid}!");
                throw;
            }

            return pline.ObjectId;
        }
        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
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
    public partial class OlieledningType : ILerLedning
    {
        [PsInclude]
        public double Tryk { get => this.tryk?.getValue() ?? default; }
        [PsInclude]
        public string Type { get => this.type ?? ""; }
        private string DetermineLayerName(Database database, bool _3D = false)
        {
            string layerName = "Olieledning";
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            if (driftsstatusSuffix.IsNotNoE())
            {
                layerName = layerName + driftsstatusSuffix;
            }

            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
        }
        public Oid DrawEntity2D(Database database)
        {
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits != default ? this.UdvendigDiameterInStdUnits : 0.011;

            return pline.Id;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }
    public partial class AfloebsledningType : ILerLedning
    {
        [PsInclude]
        public bool HarFod { get => this.harFod?.Value ?? false; }
        [PsInclude]
        public string LedningstransportType { get => this.ledningstransporttype?.Value.ToString() ?? ""; }
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
        private string DetermineLayerName(Database database, bool _3D = false)
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            if (driftsstatusSuffix.IsNotNoE())
            {
                layerName = layerName + driftsstatusSuffix;
            }

            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
        }
        public Oid DrawEntity2D(Database database)
        {
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits != default ? this.UdvendigDiameterInStdUnits : 0.011;

            return pline.Id;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }
    public partial class LedningUkendtForsyningsartType : ILerLedning
    {
        [PsInclude]
        public string SupplerendeInfo { get => this.supplerendeInfo ?? ""; }
        private string DetermineLayerName(Database database, bool _3D = false)
        {
            string layerName = "UkendtLedning";
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            if (driftsstatusSuffix.IsNotNoE())
            {
                layerName = layerName + driftsstatusSuffix;
            }

            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
        }
        public Oid DrawEntity2D(Database database)
        {
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database);

            pline.Layer = layerName;

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits != default ? this.UdvendigDiameterInStdUnits : 0.011;

            return pline.Id;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }
    public partial class ElledningType : ILerLedning
    {
        [PsInclude]
        public string Type { get => getLedningsType(); }
        [PsInclude]
        public string Afdækning { get => this.afdaekning; }
        [PsInclude]
        public Int32 AntalKabler { get => this.antalKabler; }
        //[XmlElement(DataType = "integer")]
        public Int32 antalKabler { get; set; }
        [PsInclude]
        public string KabelType { get => this.kabeltype; }
        [PsInclude]
        public string SpændingsNiveau { get => this.spaendingsniveau?.Value.ToString() + this.spaendingsniveau?.uom ?? ""; }
        private string getLedningsType()
        {
            if (this.type.IsNoE()) return "";
            if (this.type.StartsWith("other:"))
            {
                string tmp = this.type.Replace("other:", "");
                tmp = tmp.Trim();
                return tmp;
            }
            else return this.type;
        }
        /// <summary>
        /// kategori
        /// </summary>
        [XmlElement(IsNullable = true)]
        public string type { get; set; }
        private string DetermineLayerName(Database database, bool _3D = false)
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
            }

            ElledningTypeEnum type;
            if (Enum.TryParse(this.getLedningsType(), out type)) { }
            else { type = ElledningTypeEnum.ukendt; }


            switch (type)
            {
                case ElledningTypeEnum.none:
                    layerName = "0-ERROR-ElledningType-none";
                    break;
                case ElledningTypeEnum.ukendt:
                    layerName = "EL-Elledning-Ukendt";
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
                case ElledningTypeEnum.KBkabel:
                    layerName = "EL-KBkabel";
                    break;
                case ElledningTypeEnum.signalkabel:
                    layerName = "EL-Signalkabel";
                    break;
                default:
                    layerName = "0-ERROR-ElledningType-other";
                    break;
            }

            if (SpændingsNiveau.IsNotNoE() &&
                SpændingsNiveau != "0kV") layerName += $"-{SpændingsNiveau}";
            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            #region Draw 2D polyline
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
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
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            //prdDbg(this.SpændingsNiveau);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
        public enum ElledningTypeEnum
        {
            none,
            ukendt,
            beskyttelsesleder,
            forsyningskabel,
            luftledning,
            stikkabel,
            vejbelysningskabel,
            [XmlEnumAttribute("KB-kabel")]
            KBkabel,
            signalkabel
        }
    }
    public partial class AndenLedningType : ILerLedning
    {
        [PsInclude]
        public string Forsyningsart { get => getForsyningsart(); }
        private string getForsyningsart()
        {
            if (this.forsyningsart == null) return "UKENDT";
            if (this.forsyningsart.IsNoE()) return "UKENDT";
            if (this.forsyningsart.StartsWith("other:"))
            {
                string tmp = this.forsyningsart.Replace("other:", "");
                tmp = tmp.Trim();
                return tmp;
            }
            else return this.forsyningsart;
        }
        [PsInclude]
        public string Tværsnitsform { get => this.getAndenLedningTværsnitsform().ToString() ?? string.Empty; }
        private AndenLedningTværsnitsform getAndenLedningTværsnitsform()
        {
            if (this.tvaersnitsform == null) return AndenLedningTværsnitsform.ukendt;
            AndenLedningTværsnitsform form;
            if (Enum.TryParse(this.tvaersnitsform.Value, out form)) return form;
            else
            {
                Log.log($"WARNING! Element id {gmlid} has non-standard Tværsnitsform {this.tvaersnitsform.Value}");
                return AndenLedningTværsnitsform.ukendt;
            }
        }
        public enum AndenLedningTværsnitsform
        {
            ukendt,
            brilleformet,
            cirkulær,
            kvadratisk,
            rektangulær,
            sektorformet,
            spidsbundet,
            trapezformet,
            trekantet,
            tunnelformet,
            ægformet,
            øjestensprofil
        }
        [PsInclude]
        public string Type { get => this.type ?? ""; }
        [PsInclude]
        public double UdvendigBredde { get => this.udvendigBredde?.getValueInStdUnits() ?? default; }
        [PsInclude]
        public double UdvendigHøjde { get => this.udvendigHoejde?.getValueInStdUnits() ?? default; }
        private string DetermineLayerName(Database database, bool _3D = false)
        {
            #region Determine correct layer name
            string layerName = "AndenLedning";
            string driftsstatusSuffix = "";

            layerName += "-" + Forsyningsart;

            switch (this.Driftsstatus2)
            {
                case DriftsstatusType.underetablering:
                case DriftsstatusType.idrift:
                    break;
                case DriftsstatusType.permanentudeafdrift:
                    driftsstatusSuffix = "_UAD";
                    break;
                default:
                    Log.log($"AndenLedning {this.GmlId} har driftsstatus lig med null! Sætter til \"i drift\".");
                    break;
            }

            layerName += driftsstatusSuffix;
            if (_3D) layerName += "-3D";

            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }
        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id.Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            pline.ConstantWidth = this.UdvendigDiameterInStdUnits;

            return pline.ObjectId;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
    }

    #region Special treatment for LedningsTrace
    public partial class LedningstraceType : ILerLedning
    {
        [PsInclude]
        public double Bredde { get => this.bredde?.getValueInStdUnits() ?? 0; }
        [PsInclude]
        public string BreddeUnits { get => this.bredde?.uom ?? ""; }
        [PsInclude]
        public string Forsyningsart { get => getForsyningsart(this.forsyningsart).ToString(); }
        [PsInclude]
        public string IndeholdtLedning
        {
            get
            {
                if (this?.indeholdtLedning == null) return "Ingen";
                string s = "";
                if (this.indeholdtLedning.Length > 0)
                    s = string.Join(";", this.indeholdtLedning.Select(x => x.href));
                return s;
            }
        }
        private ForsyningsartEnum getForsyningsart(LedningstraceTypeForsyningsart[] forsyningsart)
        {
            if (forsyningsart.Length == 0) return ForsyningsartEnum.none;
            if (forsyningsart.Length > 1) Log.log($"WARNING! Flere forsyningsarter på Ledningstrace {this.GMLTypeID}.");
            string art = forsyningsart[0].Value;
            if (string.IsNullOrEmpty(art)) return ForsyningsartEnum.none;
            if (!forsyningsartDict.ContainsKey(art))
            {
                Log.log($"WARNING! Ledning id {this.GmlId} have undefined Forsyningsart: {art}!");
                return ForsyningsartEnum.other;
            }

            return forsyningsartDict[art];
        }

        private string DetermineLayerName(Database database, bool _3D = false)
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
                        $"Element id {this.GmlId} has invalid driftsstatus: {Driftsstatus.ToString()}!");
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

            string fareklasse = "";
            if (this.fareklasse != null)
            {
                if (this.fareklasse.Value == FareklasseType.megetfarlig)
                    fareklasse = "_MEGET_FARLIG";
                else if (this.fareklasse.Value == FareklasseType.farlig)
                    fareklasse = "_FARLIG";
            }
            layerName += fareklasse;

            layerName += suffix;
            if (_3D) layerName += "-3D";
            database.CheckOrCreateLayer(layerName);
            return layerName;
            #endregion
        }

        public Oid DrawEntity2D(Database database)
        {
            //Create new polyline in the base class
            Oid id = DrawPline2D(database);
            if (id.IsNull) return id;
            Polyline pline = id
                .Go<Polyline>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            pline.Layer = DetermineLayerName(database);

            //Handle different units

            pline.ConstantWidth = this.Bredde;

            return pline.ObjectId;
        }

        public Oid DrawPline2D(Database database)
        {
            if (this.geometri == null) return Oid.Null;
            IPointParser parser = this.geometri.MultiCurve as IPointParser;

            Point3d[] points = parser.Get3DPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2d(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            return oid;
        }

        public Oid DrawEntity3D(Database database)
        {
            Oid id = DrawPline3D(database);
            if (id.IsNull) return id;
            Polyline3d p3d = id
                .Go<Polyline3d>(database.TransactionManager.TopTransaction, OpenMode.ForWrite);

            string layerName = DetermineLayerName(database, true);

            p3d.Layer = layerName;

            return p3d.Id;
        }
        public Oid DrawPline3D(Database database)
        {
            if (this.geometri == null) return Oid.Null;
            IPointParser parser = this.geometri.MultiCurve as IPointParser;

            Point3d[] points = parser.Get3DPoints();
            Point3dCollection col = new Point3dCollection(points);

            Polyline3d p3d = new Polyline3d(Poly3dType.SimplePoly, col, false);

            Oid oid = p3d.AddEntityToDbModelSpace(database);

            return oid;
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
