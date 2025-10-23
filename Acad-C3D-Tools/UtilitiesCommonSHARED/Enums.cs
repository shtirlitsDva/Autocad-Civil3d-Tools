using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.Enums
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

    public enum PipeSystemEnum
    {
        Ukendt,
        Stål,
        Kobberflex,
        AluPex,
        PertFlextra,
        AquaTherm11,
        PE,
    }

    public enum LerTypeEnum
    {
        Ukendt,
        Afløb,
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

    public enum PipelineElementType
    {
        Pipe, //TPipe
        AfgreningMedSpring, //AfgreningMedSpring
        AfgreningParallel, //AfgreningParallel
        Afgreningsstuds, //Afgreningsstuds
        Endebund,
        Engangsventil, //Valve
        F_Model, //FModel
        Kedelrørsbøjning, //ElbowFormstykke
        LigeAfgrening, //LigeAfgrening
        PreskoblingTee, //TeeFormstykke
        PræisoleretBøjning90gr, //PreinsulatedElbow
        PræisoleretBøjning45gr, //PreinsulatedElbow
        Bøjning45gr, //ElbowFormstykke
        Bøjning30gr, //ElbowFormstykke
        Bøjning15gr, //ElbowFormstykke
        PræisoleretBøjningVariabel, //PreinsulatedElbowSteel
        PræisoleretVentil, //Valve
        PræventilMedUdluftning, //Valve
        Reduktion, //Reducer
        Svanehals, //Svanehals
        Svejsetee, //TeeFormstykke
        Svejsning,
        Y_Model, //YModel
        Buerør, //Bueror
        Stikafgrening, 
        Muffetee, //TeeFormstykke
        Materialeskift, //Materialeskift
    }
    #endregion

}