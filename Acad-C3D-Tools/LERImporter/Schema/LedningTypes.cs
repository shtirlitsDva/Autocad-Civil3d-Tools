using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public static class LerLedning
    {

    }
    public partial class LedningType
    {
        public DriftsstatusType Driftsstatus { get => this.driftsstatus.Value; }
    }
    public partial class TelekommunikationsledningType : ILerLedning
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
    public partial class RoerledningType
    {

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
    public partial class OlieledningType : ILerLedning
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
        public ForsyningsartEnum Forsyningsart { get => getForsyningsart(forsyningsart); }
        private ForsyningsartEnum getForsyningsart(string[] forsyningsart)
        {
            if (forsyningsart.Length == 0) return ForsyningsartEnum.none;
            if (forsyningsart.Length > 1) Log.log($"WARNING! Flere forsyningsarter på Føringsrør {this.id}.");
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

        public Oid DrawEntity2D(Database database)
        {
            #region Determine correct layer name
            string layerName;
            string suffix = "";

            switch (this.Driftsstatus)
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

            switch (this.Forsyningsart)
            {
                case ForsyningsartEnum.none:
                case ForsyningsartEnum.other:
                case ForsyningsartEnum.afløb:
                    throw new NotImplementedException($"Element id {this.id} has unimplemented Forsyningsart!");
                case ForsyningsartEnum.el:
                    layerName = "El-beskyttelsesrør";
                    break;
                case ForsyningsartEnum.fjernvarmefjernkøling:
                case ForsyningsartEnum.gas:
                case ForsyningsartEnum.olie:
                case ForsyningsartEnum.telekommunikation:
                case ForsyningsartEnum.vand:
                    throw new NotImplementedException($"Element id {this.id} has unimplemented Forsyningsart!");
                default:
                    throw new System.Exception(
                        $"Element id {this.id} has invalid forsyningsart: {forsyningsart[0].ToString()}!");
            }

            layerName += suffix;
            database.CheckOrCreateLayer(layerName);
            #endregion

            #region Draw 2D polyline
            IPointParser parser = this.geometri.AbstractCurve as IPointParser;
            
            Point3d[] points = parser.GetPoints();
            Polyline polyline = new Polyline(points.Length);
            
            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2D(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            polyline.Layer = layerName;

            return oid;
            #endregion
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

        private class LerFoeringsroerPS
        {
            DefinedSets SetName { get; } = DefinedSets.LerFoeringsroerPS;
            IntersectUtilities.PSetDefs.Property Forsyningsart { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Forsyningsart",
                    "Arten af rør som foringsrøret indeholder.",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property Tværsnitsform { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Tværsnitsform",
                    "Ledningens tværsnitsform.",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property UdvendigDiameterUnits { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "UdvendigDiameterUnits",
                    "Enheden som udvendig diameter er angivet med.",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property UdvendigDiameterValue { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "UdvendigDiameterValue",
                    "Udvendig diameter af røret.",
                    PsDataType.Real,
                    0);
            IntersectUtilities.PSetDefs.Property Driftsstatus { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Driftsstatus",
                    "Er ledningen i drift?",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property Id { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Id",
                    "Ledningens Ler id.",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property Indtegningsmetode { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Indtegningsmetode",
                    "Nøjagtighed på geometri.",
                    PsDataType.Text,
                    "");
            IntersectUtilities.PSetDefs.Property Nøjagtighedsklasse { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "Nøjagtighedsklasse",
                    "Nøjagtighedsmargin i geometri.",
                    PsDataType.Text,
                    "");

            IntersectUtilities.PSetDefs.Property VejledendeDybde { get; } =
                new IntersectUtilities.PSetDefs.Property(
                    "VejledendeDybde",
                    "Vejledende dybde for ledningens placering.",
                    PsDataType.Text,
                    "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                };
        }
        private enum DefinedSets
        {
            LerFoeringsroerPS
        }
    }
    public partial class AfloebsledningType : ILerLedning
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
        public Oid DrawEntity2D(Database database)
        {
            #region Draw 2D polyline
            IPointParser parser = this.geometri.AbstractCurve as IPointParser;
            Point3d[] points = parser.GetPoints();
            Polyline polyline = new Polyline(points.Length);

            for (int i = 0; i < points.Length; i++)
                polyline.AddVertexAt(polyline.NumberOfVertices, points[i].To2D(), 0, 0, 0);

            Oid oid = polyline.AddEntityToDbModelSpace(database);

            //polyline.Layer = layerName;

            return oid;
            #endregion
        }

        public Oid DrawEntity3D(Database database)
        {
            throw new NotImplementedException();
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
}
