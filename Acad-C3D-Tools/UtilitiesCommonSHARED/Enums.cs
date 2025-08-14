using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon
{
    public static partial class Utils
    {
        #region Enums
        public enum EndType
        {
            None, //0:
            Start, //1: For start of pipes
            End, //2: For ends of pipes
            Main, //3: For main run in components
            Branch, //4: For branches in components
            StikAfgrening, //5: For points where stik are connected to supply pipes
            StikStart, //6: For stik starts
            StikEnd, //7: For stik ends
            WeldOn, //8: For elements welded directly on pipe without breaking it
        }

        public enum PipeTypeEnum
        {
            Ukendt,
            Twin,
            Frem,
            Retur,
            Enkelt, //Bruges kun til blokke vist
        }

        public enum PipeSeriesEnum
        {
            Undefined,
            S1,
            S2,
            S3,
        }

        public enum PipeDnEnum
        {
            ALUPEX26,
            ALUPEX32,
            CU22,
            CU28,
            DN20,
            DN25,
            DN32,
            DN40,
            DN50,
            DN65,
            DN80,
            DN100,
            DN125,
            DN150,
            DN200,
            DN250,
            DN300,
            DN350,
            DN400,
            DN450,
            DN500,
            DN600,
        }

        public enum PipeSystemEnum
        {
            Ukendt,
            St�l,
            Kobberflex,
            AluPex,
            PertFlextra,
            AquaTherm11,
            PE,
        }

        public enum LerTypeEnum
        {
            Ukendt,
            Afl�b,
            Damp,
            EL_LS, // Low Supply (EL_04)
            EL_HS, // High Supply (EL_10, EL_30, EL_50, EL_132)
            FJV,
            Gas,
            Luft,
            Oil,
            Vand,
            UAD, // Ude Af Drift (Out of Service)
            Ignored,
        }

        public enum Spatial
        {
            Unknown,
            TwoD,
            ThreeD,
        }

        public enum DynamicProperty
        {
            None,
            Navn,
            Type,
            DN1,
            DN2,
            System,
            Vinkel,
            Serie,
            Version,
            TBLNavn,
            M1,
            M2,
            Function,
            SysNavn,
        }

        public enum CompanyEnum
        {
            Logstor,
            Isoplus,
            AquaTherm,
        }

        public static Dictionary<string, PipelineElementType> PipelineElementTypeDict =
            new Dictionary<string, PipelineElementType>()
            {
                { "Pipe", PipelineElementType.Pipe },
                { "Afgrening med spring", PipelineElementType.AfgreningMedSpring },
                { "Afgrening, parallel", PipelineElementType.AfgreningParallel },
                { "Afgreningsstuds", PipelineElementType.Afgreningsstuds },
                { "Endebund", PipelineElementType.Endebund },
                { "Engangsventil", PipelineElementType.Engangsventil },
                { "F-Model", PipelineElementType.F_Model },
                { "Kedelr�rsb�jning", PipelineElementType.Kedelr�rsb�jning },
                { "Kedelr�rsb�jning, vertikal", PipelineElementType.Kedelr�rsb�jning },
                { "Lige afgrening", PipelineElementType.LigeAfgrening },
                { "Parallelafgrening", PipelineElementType.AfgreningParallel },
                { "Pr�isoleret b�jning, 90gr", PipelineElementType.Pr�isoleretB�jning90gr },
                { "Pr�isoleret b�jning, 45gr", PipelineElementType.Pr�isoleretB�jning45gr },
                { "Pr�isoleret b�jning, 30gr", PipelineElementType.Pr�isoleretB�jning30gr },
                { "Pr�isoleret b�jning, 15gr", PipelineElementType.Pr�isoleretB�jning15gr },
                {
                    "$Pr�isoleret b�jning, L {$L1}x{$L2} m, V {$V}�",
                    PipelineElementType.Pr�isoleretB�jningVariabel
                },
                {
                    "$Pr�isoleret b�jning, 90gr, L {$L1}x{$L2} m",
                    PipelineElementType.Pr�isoleretB�jningVariabel
                },
                {
                    "Pr�isoleret b�jning, L {$L1}x{$L2} m, V {$V}�",
                    PipelineElementType.Pr�isoleretB�jningVariabel
                },
                { "Pr�isoleret ventil", PipelineElementType.Pr�isoleretVentil },
                { "Pr�ventil med udluftning", PipelineElementType.Pr�ventilMedUdluftning },
                { "Reduktion", PipelineElementType.Reduktion },
                { "Svanehals", PipelineElementType.Svanehals },
                { "Svejsetee", PipelineElementType.Svejsetee },
                { "Svejsning", PipelineElementType.Svejsning },
                { "Y-Model", PipelineElementType.Y_Model },
                { "$Buer�r V{$Vinkel}� R{$R} L{$L}", PipelineElementType.Buer�r },
                { "Stikafgrening", PipelineElementType.Stikafgrening },
                { "Muffetee", PipelineElementType.Muffetee },
                { "Preskobling tee", PipelineElementType.Muffetee },
                { "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}", PipelineElementType.Materialeskift },
            };

        public enum PipelineElementType
        {
            Pipe,
            AfgreningMedSpring,
            AfgreningParallel,
            Afgreningsstuds,
            Endebund,
            Engangsventil,
            F_Model,
            Kedelr�rsb�jning,
            LigeAfgrening,
            PreskoblingTee,
            Pr�isoleretB�jning90gr,
            Pr�isoleretB�jning45gr,
            Pr�isoleretB�jning30gr,
            Pr�isoleretB�jning15gr,
            Pr�isoleretB�jningVariabel,
            Pr�isoleretVentil,
            Pr�ventilMedUdluftning,
            Reduktion,
            Svanehals,
            Svejsetee,
            Svejsning,
            Y_Model,
            Buer�r,
            Stikafgrening,
            Muffetee,
            Materialeskift,
        }
        #endregion
    }
}