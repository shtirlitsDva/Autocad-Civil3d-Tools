using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The drawing-settings exports (NsDh_SetProducer, NsDh_ReadProducer,
/// NsDh_SetSeriesMatrix, NsDh_ReadSeriesMatrix), mirrored from
/// NsDhPipelineBridge.h.
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

    static NsDhSettingsBridge()
    {
        NsDhModule.RequireLayout<SeriesCell>(72);
        NsDhModule.RequireLayout<SettingsResult>(1032);
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

    private static NdhSettingsOutcome Outcome(int code, SettingsResult r)
    {
        (NdhSettingsStatus status, string detail) = NsDhModule.Named(code, r.Detail, NdhSettingsStatus.Failed);
        return new NdhSettingsOutcome(status, r.CellIndex, detail);
    }
}
