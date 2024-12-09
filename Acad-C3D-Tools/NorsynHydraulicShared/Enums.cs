﻿using System;
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
        PertFlextra,
        AluPEX,
        Kobber
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
}