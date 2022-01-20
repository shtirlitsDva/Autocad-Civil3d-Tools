using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public static class PipeSchedule
    {
        private static readonly Dictionary<int, double> kOdsTwin = new Dictionary<int, double>
            {
                {  20, 160.0 },
                {  25, 180.0 },
                {  32, 200.0 },
                {  40, 200.0 },
                {  50, 250.0 },
                {  65, 280.0 },
                {  80, 315.0 },
                { 100, 400.0 },
                { 125, 500.0 },
                { 150, 560.0 },
                { 200, 710.0 }
            };
        private static readonly Dictionary<int, double> kOdsBonded = new Dictionary<int, double>
            {
                { 20, 125.0 },
                { 25, 125.0 },
                { 32, 140.0 },
                { 40, 140.0 },
                { 50, 160.0 },
                { 65, 180.0 },
                { 80, 200.0 },
                { 100, 250.0 },
                { 125, 280.0 },
                { 150, 315.0 },
                { 200, 400.0 },
                { 250, 500.0 },
                { 300, 560.0 },
                { 350, 630.0 },
                { 400, 710.0 },
                { 450, 800.0 },
                { 500, 900.0 },
                { 600, 1000.0 }
            };
        public static int GetPipeDN(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                    return 32;
                case "FJV-TWIN-DN40":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                    return 40;
                case "FJV-TWIN-DN50":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                    return 50;
                case "FJV-TWIN-DN65":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                    return 65;
                case "FJV-TWIN-DN80":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                    return 80;
                case "FJV-TWIN-DN100":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                    return 100;
                case "FJV-TWIN-DN125":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                    return 125;
                case "FJV-TWIN-DN150":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                    return 150;
                case "FJV-TWIN-DN200":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                    return 200;
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                    return 250;
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                    return 300;
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                    return 350;
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                    return 400;
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                    return 450;
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                    return 500;
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return 600;
                default:
                    DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
                    Editor editor = docCol.MdiActiveDocument.Editor;
                    editor.WriteMessage("\nFor entity: " + ent.Handle.ToString() + " no pipe dimension could be determined!");
                    return 999;
            }
        }
        public static string GetPipeSystem(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                    return "Twin";
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return "Enkelt";
                default:
                    DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
                    Editor editor = docCol.MdiActiveDocument.Editor;
                    editor.WriteMessage("\nFor entity: " + ent.Handle.ToString() + " no system could be determined!");
                    return null;
            }
        }
        public static double GetPipeOd(Entity ent)
        {
            Dictionary<int, double> Ods = new Dictionary<int, double>()
            {
                { 10, 17.2 },
                { 15, 21.3 },
                { 20, 26.9 },
                { 25, 33.7 },
                { 32, 42.4 },
                { 40, 48.3 },
                { 50, 60.3 },
                { 65, 76.1 },
                { 80, 88.9 },
                { 100, 114.3 },
                { 125, 139.7 },
                { 150, 168.3 },
                { 200, 219.1 },
                { 250, 273.0 },
                { 300, 323.9 },
                { 350, 355.6 },
                { 400, 406.4 },
                { 450, 457.0 },
                { 500, 508.0 },
                { 600, 610.0 },
            };

            int dn = GetPipeDN(ent);
            if (Ods.ContainsKey(dn)) return Ods[dn];
            return 0;
        }
        /// <summary>
        /// WARNING! Currently Series 3 only.
        /// </summary>
        public static double GetTwinPipeKOd(Entity ent)
        {
            int dn = GetPipeDN(ent);
            if (kOdsTwin.ContainsKey(dn)) return kOdsTwin[dn];
            return 0;
        }
        public static double GetTwinPipeKOd(int DN)
        {
            if (kOdsTwin.ContainsKey(DN)) return kOdsTwin[DN];
            return 0;
        }
        /// <summary>
        /// WARNING! Currently S3 only.
        /// </summary>
        public static double GetBondedPipeKOd(Entity ent)
        {
            int dn = GetPipeDN(ent);
            if (kOdsBonded.ContainsKey(dn)) return kOdsBonded[dn];
            return 0;
        }
        public static double GetBondedPipeKOd(int DN)
        {
            if (kOdsBonded.ContainsKey(DN)) return kOdsBonded[DN];
            return 0;
        }
        public static double GetPipeKOd(Entity ent) =>
            GetPipeSystem(ent) == "Twin" ? GetTwinPipeKOd(ent) : GetBondedPipeKOd(ent);
        public static string GetPipeSeries(Entity ent) => "S3";
        public static double GetPipeStdLength(Entity ent) => GetPipeDN(ent) <= 80 ? 12 : 16;
        public static double GetPipeMinElasticRadius(Entity ent)
        {
            Dictionary<int, double> radii = new Dictionary<int, double>
            {
                { 20, 13.0 },
                { 25, 17.0 },
                { 32, 21.0 },
                { 40, 24.0 },
                { 50, 30.0 },
                { 65, 38.0 },
                { 80, 44.0 },
                { 100, 57.0 },
                { 125, 70.0 },
                { 150, 84.0 },
                { 200, 110.0 },
                { 250, 137.0 },
                { 300, 162.0 },
                { 350, 178.0 },
                { 400, 203.0 },
                { 999, 0.0 }
            };
            return radii[GetPipeDN(ent)];
        }
    }
}
