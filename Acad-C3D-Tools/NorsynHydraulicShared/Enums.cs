using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc
{
    public enum SegmentType
    {
        Fordelingsledning,
        Stikledning
    }

    public enum PipeType
    {
        Stål,
        PertFlextraFL,
        PertFlextraSL,
        AquaTherm11,
        AluPEX,
        Kobber,
        Pe,
    }

    public enum TempSetType
    {
        Supply,
        Return
    }

    public enum CalcType
    {
        CW,
        TM
    }

    public enum MediumTypeEnum
    {
        Water,
        Water72Ipa28,
    }
}
