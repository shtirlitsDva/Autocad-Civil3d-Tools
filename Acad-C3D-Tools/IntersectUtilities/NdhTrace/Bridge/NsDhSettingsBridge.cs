using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The drawing-settings exports (NsDh_SetProducer, NsDh_ReadProducer,
/// NsDh_SetSeriesMatrix, NsDh_ReadSeriesMatrix, NsDh_SetFittingRules,
/// NsDh_ReadFittingRules), mirrored from NsDhPipelineBridge.h.
/// </summary>
internal sealed class NsDhSettingsBridge : INdhDrawingSettings
{
    //NsDh_ReadProducer asks for at least 16 characters.
    private const int ProducerCapacity = 64;

    //sizeof 72
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SeriesCell
    {
        public int Dn;      //off 0
        public int Series;  //off 4
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System; //off 8
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Type;   //off 40
    }

    //THE WIDEST ROW THE FIELD HOLDS. Four is twice the widest published
    //situation's axis count; a row wanting more is refused here rather than
    //cut short, because a dropped band WIDENS the row it was written to narrow.
    private const int MaxBands = 4;

    //kNsDhRule*: which of a selection's two states a row carries. The states
    //spell their own values (NdhRuleSelection), so these are only read back.
    private const int RuleProdukt = 0;
    private const int RuleNothing = 1;

    //sizeof 80
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RuleBand
    {
        public double From;  //off 0
        public double To;    //off 8
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Axis; //off 16
    }

    //sizeof 552
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FittingRule
    {
        public int BandCount;  //off 0
        public int Selects;    //off 4
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string System;    //off 8
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Situation; //off 40
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Produkt;   //off 104
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxBands)] public RuleBand[] Bands; //off 232
    }

    //sizeof 1032
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SettingsResult
    {
        public int Status;     //off 0
        public int CellIndex;  //off 4
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail; //off 8
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int SetProducerFn(
        [MarshalAs(UnmanagedType.LPWStr)] string producerToken, out SettingsResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ReadProducerFn(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder output, int capacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int SetSeriesMatrixFn([In] SeriesCell[] cells, int count, out SettingsResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ReadSeriesMatrixFn([In, Out] SeriesCell[] cells, int capacity, out int outCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int SetFittingRulesFn([In] FittingRule[] rows, int count, out SettingsResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ReadFittingRulesFn([In, Out] FittingRule[] rows, int capacity, out int outCount);

    static NsDhSettingsBridge()
    {
        NsDhModule.RequireLayout<SeriesCell>(72);
        NsDhModule.RequireLayout<SettingsResult>(1032);
        NsDhModule.RequireLayout<RuleBand>(80);
        NsDhModule.RequireLayout<FittingRule>(552);
    }

    public string ReadProducer()
    {
        ReadProducerFn read = NsDhModule.Resolve<ReadProducerFn>(
            NsDhSurface.DrawingSettings, "NsDh_ReadProducer");

        StringBuilder output = new StringBuilder(ProducerCapacity);
        (NdhSettingsStatus status, string said) = NsDhModule.Named(
            read(output, ProducerCapacity), null, NdhSettingsStatus.Failed);
        if (status != NdhSettingsStatus.Ok)
            throw new InvalidOperationException(
                $"The drawing's producer could not be read ({status}). {said}".TrimEnd());
        return output.ToString();
    }

    public NdhSettingsOutcome SetProducer(string producerToken)
    {
        SetProducerFn set = NsDhModule.Resolve<SetProducerFn>(
            NsDhSurface.DrawingSettings, "NsDh_SetProducer");

        return Outcome(set(producerToken, out SettingsResult r), r);
    }

    public IReadOnlyList<NdhSeriesCell> ReadSeriesMatrix()
    {
        ReadSeriesMatrixFn read = NsDhModule.Resolve<ReadSeriesMatrixFn>(
            NsDhSurface.DrawingSettings, "NsDh_ReadSeriesMatrix");

        SeriesCell[] cells = new SeriesCell[64];
        (NdhSettingsStatus status, string said) = NsDhModule.Named(
            read(cells, cells.Length, out int count), null, NdhSettingsStatus.Failed);
        if (status == NdhSettingsStatus.BufferTooSmall)
        {
            cells = new SeriesCell[count];
            (status, said) = NsDhModule.Named(
                read(cells, cells.Length, out count), null, NdhSettingsStatus.Failed);
        }
        if (status != NdhSettingsStatus.Ok)
            throw new InvalidOperationException(
                $"The drawing's series matrix could not be read ({status}). {said}".TrimEnd());

        List<NdhSeriesCell> result = new List<NdhSeriesCell>(count);
        for (int i = 0; i < count; i++)
            result.Add(new NdhSeriesCell(cells[i].System ?? "", cells[i].Type ?? "", cells[i].Dn, cells[i].Series));
        return result;
    }

    public NdhSettingsOutcome SetSeriesMatrix(IReadOnlyList<NdhSeriesCell> cells)
    {
        SetSeriesMatrixFn set = NsDhModule.Resolve<SetSeriesMatrixFn>(
            NsDhSurface.DrawingSettings, "NsDh_SetSeriesMatrix");

        SeriesCell[] native = new SeriesCell[cells.Count];
        for (int i = 0; i < native.Length; i++)
            native[i] = new SeriesCell
            {
                Dn = cells[i].Dn,
                Series = cells[i].Series,
                System = cells[i].SystemToken,
                Type = cells[i].TypeToken,
            };

        return Outcome(set(native, native.Length, out SettingsResult r), r);
    }

    public IReadOnlyList<NdhFittingRule> ReadFittingRules()
    {
        ReadFittingRulesFn read = NsDhModule.Resolve<ReadFittingRulesFn>(
            NsDhSurface.DrawingSettings, "NsDh_ReadFittingRules");

        //Ask with nothing, then with exactly what it said: a sheet is small and
        //read once, so a guessed first capacity would only be a second way to
        //be wrong about its size.
        (NdhSettingsStatus status, string said) = NsDhModule.Named(
            read(System.Array.Empty<FittingRule>(), 0, out int count), null, NdhSettingsStatus.Failed);
        FittingRule[] rows = NewRows(count);
        if (count > 0)
            (status, said) = NsDhModule.Named(read(rows, rows.Length, out count), null, NdhSettingsStatus.Failed);
        else if (status == NdhSettingsStatus.BufferTooSmall)
            status = NdhSettingsStatus.Ok;

        if (status != NdhSettingsStatus.Ok)
            throw new InvalidOperationException(
                $"The drawing's fitting rule sheet could not be read ({status}). {said}".TrimEnd());

        List<NdhFittingRule> sheet = new List<NdhFittingRule>(count);
        for (int i = 0; i < count; i++)
        {
            List<NdhRuleBand> bands = new List<NdhRuleBand>(rows[i].BandCount);
            for (int b = 0; b < rows[i].BandCount; b++)
                bands.Add(new NdhRuleBand(rows[i].Bands[b].Axis ?? "", rows[i].Bands[b].From, rows[i].Bands[b].To));
            NdhRuleSelection selects = rows[i].Selects == RuleNothing
                ? new NdhSelectsNothing()
                : (NdhRuleSelection)new NdhSelectsProdukt(rows[i].Produkt ?? "");
            sheet.Add(new NdhFittingRule(rows[i].System ?? "", rows[i].Situation ?? "", bands, selects));
        }
        return sheet;
    }

    public NdhSettingsOutcome SetFittingRules(IReadOnlyList<NdhFittingRule> rows)
    {
        SetFittingRulesFn set = NsDhModule.Resolve<SetFittingRulesFn>(
            NsDhSurface.DrawingSettings, "NsDh_SetFittingRules");

        FittingRule[] native = NewRows(rows.Count);
        for (int i = 0; i < native.Length; i++)
        {
            IReadOnlyList<NdhRuleBand> bands = rows[i].Bands;
            if (bands.Count > MaxBands)
                throw new ArgumentException(
                    $"Row {i} ({rows[i].SituationToken}) carries {bands.Count} bands and the wire " +
                    $"field holds {MaxBands}. A band cannot be dropped to make it fit: that would " +
                    "widen the row it was written to narrow.", nameof(rows));

            native[i].BandCount = bands.Count;
            native[i].System = rows[i].SystemToken;
            native[i].Situation = rows[i].SituationToken;
            //The selection spells its own wire state; nothing here asks what it is.
            native[i].Selects = rows[i].Selects.Selects;
            native[i].Produkt = rows[i].Selects.Produkt;
            for (int b = 0; b < bands.Count; b++)
                native[i].Bands[b] = new RuleBand { Axis = bands[b].Axis, From = bands[b].From, To = bands[b].To };
        }

        return Outcome(set(native, native.Length, out SettingsResult r), r);
    }

    //A ByValArray field is marshalled element by element and must be exactly
    //its declared length, on the way out AND on the way back, so every row is
    //born with its four bands and the unused ones carry an empty axis.
    private static FittingRule[] NewRows(int count)
    {
        FittingRule[] rows = new FittingRule[count];
        for (int i = 0; i < count; i++)
        {
            rows[i].Bands = new RuleBand[MaxBands];
            for (int b = 0; b < MaxBands; b++) rows[i].Bands[b].Axis = "";
            rows[i].System = "";
            rows[i].Situation = "";
            rows[i].Produkt = "";
        }
        return rows;
    }

    private static NdhSettingsOutcome Outcome(int code, SettingsResult r)
    {
        (NdhSettingsStatus status, string detail) = NsDhModule.Named(code, r.Detail, NdhSettingsStatus.Failed);
        return new NdhSettingsOutcome(status, r.CellIndex, detail);
    }
}
