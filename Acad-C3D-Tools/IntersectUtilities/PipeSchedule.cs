using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public static class PipeSchedule
    {
        private static readonly Dictionary<int, double> kOdsS1Twin = new Dictionary<int, double>
            {
                {20,125.0},
                {25,140.0},
                {32,160.0},
                {40,160.0},
                {50,200.0},
                {65,225.0},
                {80,250.0},
                {100,315.0},
                {125,400.0},
                {150,450.0},
                {200,560.0},
                {250,710.0}
            };
        private static readonly Dictionary<int, double> kOdsS2Twin = new Dictionary<int, double>
            {
                {20,140.0},
                {25,160.0},
                {32,180.0},
                {40,180.0},
                {50,225.0},
                {65,250.0},
                {80,280.0},
                {100,355.0},
                {125,450.0},
                {150,500.0},
                {200,630.0},
                {250,800.0}
            };
        private static readonly Dictionary<int, double> kOdsS3Twin = new Dictionary<int, double>
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
                { 200, 710.0 },
                { 250, 900.0 }
            };
        private static readonly Dictionary<int, double> kOdsS1Bonded = new Dictionary<int, double>
            {
                {20,90.0},
                {25,90.0},
                {32,110.0},
                {40,110.0},
                {50,125.0},
                {65,140.0},
                {80,160.0},
                {100,200.0},
                {125,225.0},
                {150,250.0},
                {200,315.0},
                {250,400.0},
                {300,450.0},
                {350,500.0},
                {400,560.0},
                {450,630.0},
                {500,710.0},
                {600,800.0}
            };
        private static readonly Dictionary<int, double> kOdsS2Bonded = new Dictionary<int, double>
            {
                {20,110.0},
                {25,110.0},
                {32,125.0},
                {40,125.0},
                {50,140.0},
                {65,160.0},
                {80,180.0},
                {100,225.0},
                {125,250.0},
                {150,280.0},
                {200,355.0},
                {250,450.0},
                {300,500.0},
                {350,560.0},
                {400,630.0},
                {450,710.0},
                {500,800.0},
                {600,900.0}
            };
        private static readonly Dictionary<int, double> kOdsS3Bonded = new Dictionary<int, double>
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
        private static readonly Dictionary<int, double> kOdsS1CuTwin = new Dictionary<int, double>
            {
                {22, 90.0},
                {28, 90.0},
            };
        private static readonly Dictionary<int, double> kOdsS2CuTwin = new Dictionary<int, double>
            {
                {22, 110.0},
                {28, 110.0}
            };
        private static readonly Dictionary<int, double> kOdsS1CuEnkelt = new Dictionary<int, double>
            {
                {22, 65.0},
                {28, 75.0},
            };
        private static readonly Dictionary<int, double> kOdsS2CuEnkelt = new Dictionary<int, double>
            {
                {22, 75.0},
                {28, 90.0}
            };
        private static readonly Dictionary<int, double> kOdsS1AluPexTwin = new Dictionary<int, double>
            {
                { 20, 90.0 },
                { 26, 110.0 },
                { 32, 125.0 },
            };
        private static readonly Dictionary<int, double> kOdsS2AluPexTwin = new Dictionary<int, double>
            {
                {16, 110.0},
                {20, 110.0},
                {26, 125.0},
                {32, 125.0}
            };
        private static readonly Dictionary<int, double> kOdsS3AluPexTwin = new Dictionary<int, double>
            {
                {16, 125.0},
                {20, 125.0},
                {26, 140.0},
                {32, 140.0}
            };
        private static readonly Dictionary<int, double> kOdsS1AluPexEnkelt = new Dictionary<int, double>
            {
                {20, 75.0},
                {26, 90.0},
                {32, 90.0},
            };
        private static readonly Dictionary<int, double> kOdsS2AluPexEnkelt = new Dictionary<int, double>
            {
                {20, 90.0},
                {26, 90.0},
                {32, 110.0},
            };
        private static readonly Dictionary<int, double> kOdsS3AluPexEnkelt = new Dictionary<int, double>
            {
                {20, 110.0},
                {26, 110.0},
                {32, 125.0},
            };
        private static readonly Dictionary<int, double> OdsSteel = new Dictionary<int, double>()
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
        private static readonly Dictionary<int, double> OdsCu = new Dictionary<int, double>()
            {
                { 15, 15.0 },
                { 18, 18.0 },
                { 22, 22.0 },
                { 28, 28.0 }
            };
        private static readonly Dictionary<int, double> OdsAluPex = new Dictionary<int, double>()
            {
                { 16, 16.0 },
                { 20, 20.0 },
                { 26, 26.0 },
                { 32, 32.0 }
            };

        #region Code for determining trench widths
        private static readonly Dictionary<int, double> trenchWidthsS3EnkeltSteel = new Dictionary<int, double>()
        {
            { 20, 900 },
            { 25, 900 },
            { 32, 950 },
            { 40, 950 },
            { 50, 1000 },
            { 65, 1000 },
            { 80, 1000 },
            { 100, 1200 },
            { 125, 1200 },
            { 150, 1300 },
            { 200, 1550 },
            { 250, 1750 },
            { 300, 1900 },
            { 350, 2000 },
            { 400, 2200 },
            { 450, 2350 },
            { 500, 2600 },
            { 600, 2800 },
        };
        private static readonly Dictionary<int, double> trenchWidthsS2CuEnkelt = new Dictionary<int, double>
        {
            {22, 500},
            {28, 500}
        };
        private static readonly Dictionary<int, double> trenchWidthsS3TwinSteel = new Dictionary<int, double>()
        {
            { 20, 500 },
            { 25, 550 },
            { 32, 550 },
            { 40, 550 },
            { 50, 600 },
            { 65, 700 },
            { 80, 700 },
            { 100, 750 },
            { 125, 900 },
            { 150, 900 },
            { 200, 1050 },
        };
        private static readonly Dictionary<int, double> trenchWidthsS3AluPexTwin = new Dictionary<int, double>()
        {
            {26, 500},
            {32, 500}
        };
        private static readonly Dictionary<int, double> trenchWidthsS2CuTwin = new Dictionary<int, double>
        {
            {22, 500},
            {28, 500}
        };

        private struct TrenchKey
        {
            private PipeSystemEnum E1;
            private PipeTypeEnum E2;
            private PipeSeriesEnum E3;

            public TrenchKey(PipeSystemEnum e1, PipeTypeEnum e2, PipeSeriesEnum e3)
            {
                E1 = e1;
                E2 = e2;
                E3 = e3;
            }

            public override bool Equals(object obj)
            {
                if (obj is TrenchKey other)
                {
                    return E1 == other.E1 && E2 == other.E2 && E3 == other.E3;
                }
                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + E1.GetHashCode();
                    hash = hash * 23 + E2.GetHashCode();
                    hash = hash * 23 + E3.GetHashCode();
                    return hash;
                }
            }
        }
        private static readonly Dictionary<TrenchKey, Func<int, double>> trenchWidthsMap =
            new Dictionary<TrenchKey, Func<int, double>>()
        {
            { new TrenchKey(PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, PipeSeriesEnum.S3), getTrenchWidthStålEnkeltS3 },
            { new TrenchKey(PipeSystemEnum.Stål, PipeTypeEnum.Twin, PipeSeriesEnum.S3), getTrenchWidthStålTwinS3 },
            { new TrenchKey(PipeSystemEnum.Kobberflex, PipeTypeEnum.Enkelt, PipeSeriesEnum.S3), getTrenchWidthCuEnkeltS2 },
            { new TrenchKey(PipeSystemEnum.Kobberflex, PipeTypeEnum.Twin, PipeSeriesEnum.S3), getTrenchWidthCuTwinS2 }
        };

        private static double getTrenchWidthStålEnkeltS3(int dn)
        {
            if (trenchWidthsS3EnkeltSteel.ContainsKey(dn)) return trenchWidthsS3EnkeltSteel[dn];
            else throw new Exception($"trenchWidthsS3EnkeltSteel does not contain key {dn}!");
        }
        private static double getTrenchWidthStålTwinS3(int dn)
        {
            if (trenchWidthsS3TwinSteel.ContainsKey(dn)) return trenchWidthsS3TwinSteel[dn];
            else throw new Exception($"trenchWidthsS3TwinSteel does not contain key {dn}!");
        }
        private static double getTrenchWidthCuEnkeltS2(int dn)
        {
            if (trenchWidthsS2CuEnkelt.ContainsKey(dn)) return trenchWidthsS2CuEnkelt[dn];
            else throw new Exception($"trenchWidthsS2CuEnkelt does not contain key {dn}!");
        }
        private static double getTrenchWidthCuTwinS2(int dn)
        {
            if (trenchWidthsS2CuTwin.ContainsKey(dn)) return trenchWidthsS2CuTwin[dn];
            else throw new Exception($"trenchWidthsS2CuTwin does not contain key {dn}!");
        }
        public static double GetTrenchWidth(Entity ent)
        {
            int dn = GetPipeDN(ent);

            //Twin eller enkelt
            PipeTypeEnum type = GetPipeType(ent);
            if (type == PipeTypeEnum.Frem || type == PipeTypeEnum.Retur) type = PipeTypeEnum.Enkelt;

            PipeSeriesEnum series = GetPipeSeriesV2(ent, true);

            //Stål, cu- el. aluflex
            PipeSystemEnum system = GetPipeSystem(ent);

            double result = GetTrenchWidth(dn, system, type, series);
            if (result > 0) return result;
            else throw new Exception($"Entity {ent.Handle} failed to get correct thrench width!");
        }
        public static double GetTrenchWidth(int dn, PipeSystemEnum ps, PipeTypeEnum pt,  PipeSeriesEnum series)
        {
            TrenchKey tk = new TrenchKey(ps, pt, series);
            if (trenchWidthsMap.ContainsKey(tk)) return trenchWidthsMap[tk].Invoke(dn);
            else
            {
                prdDbg($"DN {dn}, System {ps}, Type {pt}, Series {series}: Could not get a Trench Width!");
                return 0;
            }
        }
        #endregion

        private static string ExtractLayerName(Entity ent)
        {
            string layer = ent.Layer;
            if (layer.Contains("|")) return layer.Split('|').Last();
            else return layer;
        }
        private static string ExtractLayerName(string layer)
        {
            if (layer.Contains("|")) return layer.Split('|').Last();
            else return layer;
        }
        public static int GetPipeDN(Entity ent)
        {
            return GetPipeDN(ExtractLayerName(ent));
        }
        public static PipeDnEnum GetPipeDnEnum(Entity ent)
        {
            int DN = GetPipeDN(ent);

            switch (GetPipeSystem(ent))
            {
                case PipeSystemEnum.Ukendt:
                    return default;
                case PipeSystemEnum.Stål:
                    return (PipeDnEnum)Enum.Parse(
                        typeof(PipeDnEnum), "DN" + DN.ToString());
                case PipeSystemEnum.Kobberflex:
                    return (PipeDnEnum)Enum.Parse(
                        typeof(PipeDnEnum), "CU" + DN.ToString());
                case PipeSystemEnum.AluPex:
                    return (PipeDnEnum)Enum.Parse(
                        typeof(PipeDnEnum), "ALUPEX" + DN.ToString());
                default:
                    return default;
            }
        }
        public static int GetPipeDN(string layer)
        {
            layer = ExtractLayerName(layer);
            switch (layer)
            {
                case "FJV-TWIN-ALUPEX16":
                case "FJV-FREM-ALUPEX16":
                case "FJV-RETUR-ALUPEX16":
                    return 15;
                case "FJV-TWIN-ALUPEX20":
                case "FJV-FREM-ALUPEX20":
                case "FJV-RETUR-ALUPEX20":
                    return 20;
                case "FJV-TWIN-ALUPEX26":
                case "FJV-FREM-ALUPEX26":
                case "FJV-RETUR-ALUPEX26":
                    return 26;
                case "FJV-TWIN-ALUPEX32":
                case "FJV-FREM-ALUPEX32":
                case "FJV-RETUR-ALUPEX32":
                    return 32;
                case "FJV-TWIN-CU15":
                case "FJV-FREM-CU15":
                case "FJV-RETUR-CU15":
                    return 15;
                case "FJV-TWIN-CU18":
                case "FJV-FREM-CU18":
                case "FJV-RETUR-CU18":
                    return 18;
                case "FJV-TWIN-CU22":
                case "FJV-FREM-CU22":
                case "FJV-RETUR-CU22":
                    return 22;
                case "FJV-TWIN-CU28":
                case "FJV-FREM-CU28":
                case "FJV-RETUR-CU28":
                    return 28;
                case "FJV-TWIN-DN20":
                case "FJV-FREM-DN20":
                case "FJV-RETUR-DN20":
                    return 20;
                case "FJV-TWIN-DN25":
                case "FJV-FREM-DN25":
                case "FJV-RETUR-DN25":
                    return 25;
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
                case "FJV-TWIN-DN250":
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
                    editor.WriteMessage("\nFor layer named: " + layer + " no pipe dimension could be determined!");
                    return 999;
            }
        }
        public static PipeTypeEnum GetPipeType(Entity ent)
        {
            return GetPipeType(ExtractLayerName(ent));
        }
        public static PipeTypeEnum GetPipeType(Entity ent, bool FRtoEnkelt = false)
        {
            var type = GetPipeType(ExtractLayerName(ent));
            if (!FRtoEnkelt) return type;
            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                case PipeTypeEnum.Twin:
                    return type;
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                case PipeTypeEnum.Enkelt:
                    return PipeTypeEnum.Enkelt;
                default:
                    return PipeTypeEnum.Ukendt;
            }
        }
        public static PipeTypeEnum GetPipeType(string layer)
        {
            layer = ExtractLayerName(layer);
            switch (layer)
            {
                case "FJV-TWIN-ALUPEX16":
                case "FJV-TWIN-ALUPEX20":
                case "FJV-TWIN-ALUPEX26":
                case "FJV-TWIN-ALUPEX32":
                case "FJV-TWIN-CU15":
                case "FJV-TWIN-CU18":
                case "FJV-TWIN-CU22":
                case "FJV-TWIN-CU28":
                case "FJV-TWIN-DN20":
                case "FJV-TWIN-DN25":
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                case "FJV-TWIN-DN250":
                    return PipeTypeEnum.Twin;
                case "FJV-FREM-ALUPEX16":
                case "FJV-FREM-ALUPEX20":
                case "FJV-FREM-ALUPEX26":
                case "FJV-FREM-ALUPEX32":
                case "FJV-FREM-CU15":
                case "FJV-FREM-CU18":
                case "FJV-FREM-CU22":
                case "FJV-FREM-CU28":
                case "FJV-FREM-DN20":
                case "FJV-FREM-DN25":
                case "FJV-FREM-DN32":
                case "FJV-FREM-DN40":
                case "FJV-FREM-DN50":
                case "FJV-FREM-DN65":
                case "FJV-FREM-DN80":
                case "FJV-FREM-DN100":
                case "FJV-FREM-DN125":
                case "FJV-FREM-DN150":
                case "FJV-FREM-DN200":
                case "FJV-FREM-DN250":
                case "FJV-FREM-DN300":
                case "FJV-FREM-DN350":
                case "FJV-FREM-DN400":
                case "FJV-FREM-DN450":
                case "FJV-FREM-DN500":
                case "FJV-FREM-DN600":
                    return PipeTypeEnum.Frem;
                case "FJV-RETUR-ALUPEX16":
                case "FJV-RETUR-ALUPEX20":
                case "FJV-RETUR-ALUPEX26":
                case "FJV-RETUR-ALUPEX32":
                case "FJV-RETUR-CU15":
                case "FJV-RETUR-CU18":
                case "FJV-RETUR-CU22":
                case "FJV-RETUR-CU28":
                case "FJV-RETUR-DN20":
                case "FJV-RETUR-DN25":
                case "FJV-RETUR-DN32":
                case "FJV-RETUR-DN40":
                case "FJV-RETUR-DN50":
                case "FJV-RETUR-DN65":
                case "FJV-RETUR-DN80":
                case "FJV-RETUR-DN100":
                case "FJV-RETUR-DN125":
                case "FJV-RETUR-DN150":
                case "FJV-RETUR-DN200":
                case "FJV-RETUR-DN250":
                case "FJV-RETUR-DN300":
                case "FJV-RETUR-DN350":
                case "FJV-RETUR-DN400":
                case "FJV-RETUR-DN450":
                case "FJV-RETUR-DN500":
                case "FJV-RETUR-DN600":
                    return PipeTypeEnum.Retur;
                default:
                    DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
                    Editor editor = docCol.MdiActiveDocument.Editor;
                    editor.WriteMessage("\nFor layer name: " + layer + " no system could be determined!");
                    return PipeTypeEnum.Ukendt;
            }
        }
        public static double GetPipeOd(Entity ent)
        {
            int dn = GetPipeDN(ent);
            PipeSystemEnum system = GetPipeSystem(ent);
            switch (system)
            {
                case PipeSystemEnum.Ukendt:
                    return 0;
                case PipeSystemEnum.Stål:
                    if (OdsSteel.ContainsKey(dn)) return OdsSteel[dn];
                    else return 0;
                case PipeSystemEnum.Kobberflex:
                    if (OdsCu.ContainsKey(dn)) return OdsCu[dn];
                    else return 0;
                case PipeSystemEnum.AluPex:
                    if (OdsAluPex.ContainsKey(dn)) return OdsAluPex[dn];
                    else return 0;
                default:
                    return 0;
            }
        }
        internal static PipeSystemEnum GetPipeSystem(Entity ent)
        {
            string layer = ExtractLayerName(ent);
            switch (layer)
            {
                case "FJV-FREM-DN20":
                case "FJV-RETUR-DN20":
                case "FJV-FREM-DN25":
                case "FJV-RETUR-DN25":
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
                case "FJV-TWIN-DN20":
                case "FJV-TWIN-DN25":
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                case "FJV-TWIN-DN250":
                    return PipeSystemEnum.Stål;
                case "FJV-FREM-CU15":
                case "FJV-RETUR-CU15":
                case "FJV-FREM-CU18":
                case "FJV-RETUR-CU18":
                case "FJV-FREM-CU22":
                case "FJV-RETUR-CU22":
                case "FJV-FREM-CU28":
                case "FJV-RETUR-CU28":
                case "FJV-TWIN-CU15":
                case "FJV-TWIN-CU18":
                case "FJV-TWIN-CU22":
                case "FJV-TWIN-CU28":
                    return PipeSystemEnum.Kobberflex;
                case "FJV-TWIN-ALUPEX16":
                case "FJV-FREM-ALUPEX16":
                case "FJV-RETUR-ALUPEX16":
                case "FJV-TWIN-ALUPEX20":
                case "FJV-FREM-ALUPEX20":
                case "FJV-RETUR-ALUPEX20":
                case "FJV-TWIN-ALUPEX26":
                case "FJV-FREM-ALUPEX26":
                case "FJV-RETUR-ALUPEX26":
                case "FJV-TWIN-ALUPEX32":
                case "FJV-FREM-ALUPEX32":
                case "FJV-RETUR-ALUPEX32":
                    return PipeSystemEnum.AluPex;
                default:
                    //prdDbg("\nFor layer name: " + layer + " no system could be determined!");
                    return PipeSystemEnum.Ukendt;
            }
        }
        public static double GetTwinPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            return GetTwinPipeKOd(dn, pipeSeries);
        }
        public static double GetTwinPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1Twin.ContainsKey(dn)) return kOdsS1Twin[dn];
                    else return 0;
                case PipeSeriesEnum.S2:
                    if (kOdsS2Twin.ContainsKey(dn)) return kOdsS2Twin[dn];
                    else return 0;
                case PipeSeriesEnum.S3:
                    if (kOdsS3Twin.ContainsKey(dn)) return kOdsS3Twin[dn];
                    else return 0;
                default:
                    return 0;
            }
        }
        //public static double GetTwinPipeKOd(Entity ent)
        //{
        //    int DN = GetPipeDN(ent);
        //    if (kOdsS3Twin.ContainsKey(DN)) return kOdsS3Twin[DN];
        //    return 0;
        //}
        //public static double GetTwinPipeKOd(int DN)
        //{
        //    if (kOdsS3Twin.ContainsKey(DN)) return kOdsS3Twin[DN];
        //    return 0;
        //}
        public static double GetBondedPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1Bonded.ContainsKey(dn)) return kOdsS1Bonded[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2Bonded.ContainsKey(dn)) return kOdsS2Bonded[dn];
                    break;
                case PipeSeriesEnum.S3:
                    if (kOdsS3Bonded.ContainsKey(dn)) return kOdsS3Bonded[dn];
                    break;
                default:
                    return 0.0;
            }
            return 0.0;
        }
        public static double GetCuEnkeltPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            return GetCuEnkeltPipeKOd(dn, pipeSeries);
        }
        public static double GetCuEnkeltPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1CuEnkelt.ContainsKey(dn)) return kOdsS1CuEnkelt[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2CuEnkelt.ContainsKey(dn)) return kOdsS2CuEnkelt[dn];
                    break;
                default:
                    return 0.0;
            }
            return 0.0;
        }
        public static double GetCuTwinPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            return GetCuTwinPipeKOd(dn, pipeSeries);
        }
        public static double GetCuTwinPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1CuTwin.ContainsKey(dn)) return kOdsS1CuTwin[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2CuTwin.ContainsKey(dn)) return kOdsS2CuTwin[dn];
                    break;
                default:
                    return 0.0;
            }
            return 0.0;
        }
        public static double GetAluPexEnkeltPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            return GetAluPexEnkeltPipeKOd(dn, pipeSeries);
        }
        public static double GetAluPexEnkeltPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1AluPexEnkelt.ContainsKey(dn)) return kOdsS1AluPexEnkelt[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2AluPexEnkelt.ContainsKey(dn)) return kOdsS2AluPexEnkelt[dn];
                    break;
                case PipeSeriesEnum.S3:
                    if (kOdsS3AluPexEnkelt.ContainsKey(dn)) return kOdsS3AluPexEnkelt[dn];
                    break;
                default:
                    return 0.0;
            }
            return 0.0;
        }
        public static double GetAluPexTwinPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            return GetAluPexTwinPipeKOd(dn, pipeSeries);
        }
        public static double GetAluPexTwinPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1AluPexTwin.ContainsKey(dn)) return kOdsS1AluPexTwin[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2AluPexTwin.ContainsKey(dn)) return kOdsS2AluPexTwin[dn];
                    break;
                case PipeSeriesEnum.S3:
                    if (kOdsS3AluPexTwin.ContainsKey(dn)) return kOdsS3AluPexTwin[dn];
                    break;
                default:
                    return 0.0;
            }
            return 0.0;
        }
        public static double GetBondedPipeKOd(int dn, PipeSeriesEnum pipeSeries)
        {
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1Bonded.ContainsKey(dn)) return kOdsS1Bonded[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2Bonded.ContainsKey(dn)) return kOdsS2Bonded[dn];
                    break;
                case PipeSeriesEnum.S3:
                    if (kOdsS3Bonded.ContainsKey(dn)) return kOdsS3Bonded[dn];
                    break;
                default:
                    return 0;
            }
            return 0;
        }
        //public static double GetBondedPipeKOd(Entity ent)
        //{
        //    int dn = GetPipeDN(ent);
        //    if (kOdsS3Bonded.ContainsKey(dn)) return kOdsS3Bonded[dn];
        //    return 0;
        //}
        //public static double GetBondedPipeKOd(int DN)
        //{
        //    if (kOdsS3Bonded.ContainsKey(DN)) return kOdsS3Bonded[DN];
        //    return 0;
        //}
        public static double GetPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            PipeTypeEnum pipeType = GetPipeType(ent);
            PipeSystemEnum pipeSystem = GetPipeSystem(ent);
            switch (pipeType)
            {
                case PipeTypeEnum.Ukendt:
                    return 0.0;
                case PipeTypeEnum.Twin:
                    switch (pipeSystem)
                    {
                        case PipeSystemEnum.Ukendt:
                            return 0.0;
                        case PipeSystemEnum.Stål:
                            return GetTwinPipeKOd(ent, pipeSeries);
                        case PipeSystemEnum.Kobberflex:
                            return GetCuTwinPipeKOd(ent, pipeSeries);
                        case PipeSystemEnum.AluPex:
                            return GetAluPexTwinPipeKOd(ent, pipeSeries);
                        default:
                            return 0.0;
                    }
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    switch (pipeSystem)
                    {
                        case PipeSystemEnum.Ukendt:
                            return 0.0;
                        case PipeSystemEnum.Stål:
                            return GetBondedPipeKOd(ent, pipeSeries);
                        case PipeSystemEnum.Kobberflex:
                            return GetCuEnkeltPipeKOd(ent, pipeSeries);
                        case PipeSystemEnum.AluPex:
                            return GetAluPexEnkeltPipeKOd(ent, pipeSeries);
                        default:
                            return 0.0;
                    }
                default:
                    return 0.0;
            }

        }
        public static double GetKOd(int dn, PipeTypeEnum pt, PipeSystemEnum ps, PipeSeriesEnum series)
        {
            switch (pt)
            {
                case PipeTypeEnum.Ukendt:
                    return 0.0;
                case PipeTypeEnum.Twin:
                    switch (ps)
                    {
                        case PipeSystemEnum.Ukendt:
                            return 0.0;
                        case PipeSystemEnum.Stål:
                            return GetTwinPipeKOd(dn, series);
                        case PipeSystemEnum.Kobberflex:
                            return GetCuTwinPipeKOd(dn, series);
                        case PipeSystemEnum.AluPex:
                            return GetAluPexTwinPipeKOd(dn, series);
                        default:
                            return 0.0;
                    }
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    switch (ps)
                    {
                        case PipeSystemEnum.Ukendt:
                            return 0.0;
                        case PipeSystemEnum.Stål:
                            return GetBondedPipeKOd(dn, series);
                        case PipeSystemEnum.Kobberflex:
                            return GetCuEnkeltPipeKOd(dn, series);
                        case PipeSystemEnum.AluPex:
                            return GetAluPexEnkeltPipeKOd(dn, series);
                        default:
                            return 0.0;
                    }
                default:
                    return 0.0;
            }
        }
        public static double GetPipeKOd(Entity ent, bool hardFail = false)
        {
            return GetPipeKOd(ent, GetPipeSeriesV2(ent, hardFail));
        }
        public static string GetPipeSeries(Entity ent) => "S3";
        public static PipeSeriesEnum GetPipeSeriesV2(Entity ent, bool hardFail = false)
        {
            double realKod;
            try
            {
                realKod = ((Polyline)ent).ConstantWidth;
            }
            catch (Exception)
            {
                if (hardFail) throw new Exception($"Ent {ent.Handle} ConstantWidth threw an exception!");
                prdDbg($"Ent {ent.Handle} ConstantWidth threw an exception!");
                return PipeSeriesEnum.Undefined;
            }
            PipeSystemEnum pipeSystem = GetPipeSystem(ent);
            double kod;
            switch (pipeSystem)
            {
                case PipeSystemEnum.Ukendt:
                    break;
                case PipeSystemEnum.Stål:
                case PipeSystemEnum.AluPex:
                    kod = GetPipeKOd(ent, PipeSeriesEnum.S3) / 1000;
                    if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S3;
                    kod = GetPipeKOd(ent, PipeSeriesEnum.S2) / 1000;
                    if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S2;
                    kod = GetPipeKOd(ent, PipeSeriesEnum.S1) / 1000;
                    if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S1;
                    break;
                case PipeSystemEnum.Kobberflex:
                    kod = GetPipeKOd(ent, PipeSeriesEnum.S2) / 1000;
                    if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S2;
                    kod = GetPipeKOd(ent, PipeSeriesEnum.S1) / 1000;
                    if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S1;
                    break;
                default:
                    break;
            }

            bool Equalz(double x, double y, double eps)
            {
                if (Math.Abs(x - y) < eps) return true;
                else return false;
            }

            return PipeSeriesEnum.Undefined;
        }
        public static string GetPipeSeries(PipeSeriesEnum pipeSeries) => pipeSeries.ToString();
        public static double GetPipeStdLength(Entity ent) => GetPipeDN(ent) <= 80 ? 12 : 16;
        public static bool IsInSituBent(Entity ent)
        {
            PipeTypeEnum type = GetPipeType(ent);
            PipeSystemEnum system = GetPipeSystem(ent);

            //Flexrør er altid insitu bukkede
            if (
                system == PipeSystemEnum.Kobberflex ||
                system == PipeSystemEnum.AluPex
                ) return true;

            switch (type)
            {
                case PipeTypeEnum.Ukendt:
                    throw new Exception(
                        $"IsInSituBent -> Entity handle {ent.Handle} has invalid layer!");
                case PipeTypeEnum.Twin:
                    if (GetPipeDN(ent) < 65) return true;
                    break;
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    if (GetPipeDN(ent) < 100) return true;
                    break;
                default:
                    throw new Exception(
                        $"IsInSituBent -> Entity handle {ent.Handle} has invalid layer!");
            }
            return false;
        }
        #region Tables for elastic and buerør radii
        private static Dictionary<int, double> cuS1Enkelt = new Dictionary<int, double>
            {
                { 15, 0.6 },
                { 18, 0.7 },
                { 22, 0.8 },
                { 28, 0.8 },
            };
        private static Dictionary<int, double> cuS2Enkelt = new Dictionary<int, double>
            {
                { 15, 0.8 },
                { 18, 0.8 },
                { 22, 0.8 },
                { 28, 1.0 },
            };
        private static Dictionary<int, double> cuS1Twin = new Dictionary<int, double>
            {
                { 22, 0.9 },
                { 28, 0.9 },
            };
        private static Dictionary<int, double> cuS2Twin = new Dictionary<int, double>
            {
                { 22, 1.1 },
                { 28, 1.1 },
            };
        private static Dictionary<int, double> aluPexS1Enkelt = new Dictionary<int, double>
            {
                { 20, 0.7 },
                { 26, 0.72 },
                { 32, 0.72 },
            };
        private static Dictionary<int, double> aluPexS2Enkelt = new Dictionary<int, double>
            {
                { 20, 0.72 },
                { 26, 0.72 },
                { 32, 0.9 },
            };
        private static Dictionary<int, double> aluPexS3Enkelt = new Dictionary<int, double>
            {
                { 20, 0.9 },
                { 26, 0.9 },
                { 32, 1.0 },
            };
        private static Dictionary<int, double> aluPexS1Twin = new Dictionary<int, double>
            {
                { 20, 0.9 },
                { 26, 1.1 },
                { 32, 1.25 },
            };
        private static Dictionary<int, double> aluPexS2Twin = new Dictionary<int, double>
            {
                { 16, 1.1 },
                { 20, 1.1 },
                { 26, 1.25 },
                { 32, 1.25 },
            };
        private static Dictionary<int, double> aluPexS3Twin = new Dictionary<int, double>
            {
                { 16, 1.25 },
                { 20, 1.25 },
                { 26, 1.40 },
                { 32, 1.40 },
            };
        private static Dictionary<int, double> steelMinElasticRadii = new Dictionary<int, double>
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
                { 450, 229.0 },
                { 500, 254.0 },
                { 999, 0.0 }
            };
        private static Dictionary<int, double> steelEnkeltMinBuerorRadii = new Dictionary<int, double>
            {
                { 20, 8.4 },
                { 25, 12.8 },
                { 32, 13.4 },
                { 40, 15.4 },
                { 50, 17.2 },
                { 65, 19.6 },
                { 80, 20.2 },
                { 100, 20.8 },
                { 125, 20.8 },
                { 150, 22.9 },
                { 200, 26.2 },
                { 250, 29.6 },
                { 300, 32.7 },
                { 350, 65.5 },
                { 400, 70.5 },
                { 450, 114.6 },
                { 500, 152.8 },
                { 550, 152.8 },
                { 999, 0.0 }
            };
        private static Dictionary<int, double> steelTwinMinBuerorRadii = new Dictionary<int, double>
            {
                { 20, 0.0 },
                { 25, 16.8 },
                { 32, 16.8 },
                { 40, 19.7 },
                { 50, 16.0 },
                { 65, 17.6 },
                { 80, 18.6 },
                { 100, 22.9 },
                { 125, 31.3 },
                { 150, 36.2 },
                { 200, 42.9 },
                { 999, 0.0 }
            };
        #endregion
        public static double GetPipeMinElasticRadius(Entity ent, bool considerInSituBending = true)
        {
            if (considerInSituBending && IsInSituBent(ent)) return 0;

            PipeTypeEnum pipeType = GetPipeType(ent);
            PipeSeriesEnum pipeSeries = GetPipeSeriesV2(ent);
            PipeSystemEnum pipeSystem = GetPipeSystem(ent);

            switch (pipeSystem)
            {
                case PipeSystemEnum.Ukendt:
                    return 0;
                case PipeSystemEnum.Stål:
                    int dn = GetPipeDN(ent);
                    if (steelMinElasticRadii.ContainsKey(dn)) return steelMinElasticRadii[dn];
                    else return steelMinElasticRadii[999];
                case PipeSystemEnum.Kobberflex:
                    switch (pipeType)
                    {
                        case PipeTypeEnum.Ukendt:
                            return 0;
                        case PipeTypeEnum.Twin:
                            switch (pipeSeries)
                            {
                                case PipeSeriesEnum.Undefined:
                                    return 0;
                                case PipeSeriesEnum.S1:
                                    return cuS1Twin[GetPipeDN(ent)];
                                case PipeSeriesEnum.S2:
                                    return cuS2Twin[GetPipeDN(ent)];
                                case PipeSeriesEnum.S3:
                                    return 0;
                                default:
                                    return 0;
                            }
                        case PipeTypeEnum.Frem:
                        case PipeTypeEnum.Retur:
                            switch (pipeSeries)
                            {
                                case PipeSeriesEnum.Undefined:
                                    return 0;
                                case PipeSeriesEnum.S1:
                                    return cuS1Enkelt[GetPipeDN(ent)];
                                case PipeSeriesEnum.S2:
                                    return cuS2Enkelt[GetPipeDN(ent)];
                                case PipeSeriesEnum.S3:
                                    return 0;
                                default:
                                    return 0;
                            }
                        default:
                            return 0;
                    }
                case PipeSystemEnum.AluPex:
                    switch (pipeType)
                    {
                        case PipeTypeEnum.Ukendt:
                            return 0;
                        case PipeTypeEnum.Twin:
                            switch (pipeSeries)
                            {
                                case PipeSeriesEnum.Undefined:
                                    return 0;
                                case PipeSeriesEnum.S1:
                                    return aluPexS1Twin[GetPipeDN(ent)];
                                case PipeSeriesEnum.S2:
                                    return aluPexS2Twin[GetPipeDN(ent)];
                                case PipeSeriesEnum.S3:
                                    return aluPexS3Twin[GetPipeDN(ent)];
                                default:
                                    return 0;
                            }
                        case PipeTypeEnum.Frem:
                        case PipeTypeEnum.Retur:
                            switch (pipeSeries)
                            {
                                case PipeSeriesEnum.Undefined:
                                    return 0;
                                case PipeSeriesEnum.S1:
                                    return aluPexS1Enkelt[GetPipeDN(ent)];
                                case PipeSeriesEnum.S2:
                                    return aluPexS2Enkelt[GetPipeDN(ent)];
                                case PipeSeriesEnum.S3:
                                    return aluPexS3Enkelt[GetPipeDN(ent)];
                                default:
                                    return 0;
                            }
                        default:
                            return 0;
                    }
                default:
                    return 0;
            }
        }
        public static double GetBuerorMinRadius(Entity ent)
        {
            PipeSystemEnum pipeSystem = GetPipeSystem(ent);
            if (pipeSystem != PipeSystemEnum.Stål) return 0.0;
            PipeTypeEnum pipeType = GetPipeType(ent);
            if (pipeType == PipeTypeEnum.Frem || pipeType == PipeTypeEnum.Retur)
                pipeType = PipeTypeEnum.Twin;
            int dn = GetPipeDN(ent);
            switch (pipeType)
            {
                case PipeTypeEnum.Twin:
                    if (steelTwinMinBuerorRadii.ContainsKey(dn))
                        return steelTwinMinBuerorRadii[dn];
                    else return 0.0;
                case PipeTypeEnum.Enkelt:
                    if (steelEnkeltMinBuerorRadii.ContainsKey(dn))
                        return steelEnkeltMinBuerorRadii[dn];
                    else return 0.0;
                default:
                    return 0.0;
            }

        }
        public static string GetLabel(Entity ent)
        {
            //Test to see if the polyline resides in the correct layer
            int DN = GetPipeDN(ent);
            if (DN == 999)
            {
                prdDbg("Kunne ikke finde dimension på valgte rør! Kontroller lag!");
                return "";
            }
            var type = GetPipeType(ent);
            if (type == PipeTypeEnum.Ukendt)
            {
                prdDbg("Kunne ikke finde systemet på valgte rør! Kontroller lag!");
                return "";
            }
            double od = GetPipeOd(ent);
            if (od < 1.0)
            {
                prdDbg("Kunne ikke finde rørdimensionen på valgte rør! Kontroller lag!");
                return "";
            }
            PipeSystemEnum system = GetPipeSystem(ent);
            if (system == PipeSystemEnum.Ukendt)
            {
                prdDbg("Kunne ikke finde system på valgte rør! Kontroller lag!");
                return "";
            }

            //Build label
            string labelText = "";
            double kOd = 0;
            PipeSeriesEnum series = GetPipeSeriesV2(ent);
            kOd = GetPipeKOd(ent, series);
            if (kOd < 1.0)
            {
                prdDbg("Kunne ikke finde kappedimensionen på valgte rør! Kontroller lag!");
                return "";
            }

            switch (type)
            {
                case PipeTypeEnum.Twin:
                    switch (system)
                    {
                        case PipeSystemEnum.Stål:
                            labelText = $"DN{DN}-ø{od.ToString("N1")}+ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                            break;
                        case PipeSystemEnum.Kobberflex:
                            labelText = $"CU{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                            break;
                        case PipeSystemEnum.AluPex:
                            labelText = $"AluPex{DN}-ø{od.ToString("N0")}+ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                            break;
                        default:
                            break;
                    }
                    break;
                case PipeTypeEnum.Frem:
                case PipeTypeEnum.Retur:
                    switch (system)
                    {
                        case PipeSystemEnum.Stål:
                            labelText = $"DN{DN}-ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                            break;
                        case PipeSystemEnum.Kobberflex:
                            labelText = $"CU{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                            break;
                        case PipeSystemEnum.AluPex:
                            labelText = $"AluPex{DN}-ø{od.ToString("N0")}/{kOd.ToString("N0")}";
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
            return labelText;
        }
    }
}
