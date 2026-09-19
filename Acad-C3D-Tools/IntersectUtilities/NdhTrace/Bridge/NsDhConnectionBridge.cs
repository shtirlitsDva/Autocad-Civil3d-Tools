using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// NsDh_ConnectBranch and NsDh_ReadPipelineConnections, mirrored field for
/// field from NsDhPipelineBridge.h (sizes and offsets in the comments are the
/// header's static_asserts).
/// </summary>
internal sealed class NsDhConnectionBridge : INdhConnector
{
    private const int ReadOk = 0;
    private const int ReadBufferTooSmall = -3;

    //sizeof 264
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BranchConnectRequest
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string MainHandle;   //off 0
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string BranchHandle; //off 64
        public int BranchAtStart;                                                         //off 128
        public int Outlet;                                                                //off 132
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Produkt;      //off 136
    }

    //sizeof 1072
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BranchConnectResult
    {
        public int Status;          //off 0
        public int MainVertexIndex; //off 4
        public double DeviationDeg; //off 8
        public double EndMoveM;     //off 16
        public double LargestMoveM; //off 24
        public double PortX;        //off 32
        public double PortY;        //off 40
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Detail; //off 48
    }

    //sizeof 360
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConnectionRow
    {
        public int RouteIndex;  //off 0
        public int IsHost;      //off 4
        public int Outlet;      //off 8
        public int Reserved;    //off 12
        public double Station;  //off 16
        public double X;        //off 24
        public double Y;        //off 32
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string PartnerHandle;   //off 40
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string PinnedProdukt;   //off 104
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ResolvedProdukt; //off 232
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ConnectBranchFn(in BranchConnectRequest request, out BranchConnectResult result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int ReadConnectionsFn(
        [MarshalAs(UnmanagedType.LPWStr)] string pipelineHandle,
        [In, Out] ConnectionRow[] rows,
        int capacity,
        out int outCount);

    static NsDhConnectionBridge()
    {
        //The mirrors must keep the header's layout; a drift is a build defect,
        //caught before any call can corrupt memory.
        CheckSize<BranchConnectRequest>(264);
        CheckSize<BranchConnectResult>(1072);
        CheckSize<ConnectionRow>(360);
    }

    private static void CheckSize<T>(int expected)
    {
        int actual = Marshal.SizeOf<T>();
        if (actual != expected)
            throw new InvalidOperationException(
                $"{typeof(T).Name} marshals to {actual} bytes, the NDH header says {expected}.");
    }

    public NdhConnectOutcome Connect(NdhConnectRequest request)
    {
        ConnectBranchFn connect = NsDhModule.Resolve<ConnectBranchFn>(
            NsDhSurface.BranchConnect, "NsDh_ConnectBranch");

        BranchConnectRequest native = new BranchConnectRequest
        {
            MainHandle = request.MainHandle,
            BranchHandle = request.BranchHandle,
            BranchAtStart = request.BranchAtStart ? 1 : 0,
            Outlet = (int)request.Outlet,
            Produkt = request.Produkt,
        };

        int status = connect(native, out BranchConnectResult r);

        //A code this build does not know is still a refusal; its number is kept
        //in the detail so it is not lost.
        NdhConnectStatus named = Enum.IsDefined(typeof(NdhConnectStatus), status)
            ? (NdhConnectStatus)status
            : NdhConnectStatus.Failed;
        string detail = named == (NdhConnectStatus)status
            ? r.Detail ?? ""
            : $"(status {status}) {r.Detail}";

        return new NdhConnectOutcome(
            named, r.MainVertexIndex, r.DeviationDeg, r.EndMoveM, r.LargestMoveM, r.PortX, r.PortY, detail);
    }

    public IReadOnlyList<NdhConnectionRow> ReadConnections(string pipelineHandle)
    {
        ReadConnectionsFn read = NsDhModule.Resolve<ReadConnectionsFn>(
            NsDhSurface.PipelineRead, "NsDh_ReadPipelineConnections");

        ConnectionRow[] rows = new ConnectionRow[8];
        int status = read(pipelineHandle, rows, rows.Length, out int count);
        if (status == ReadBufferTooSmall)
        {
            rows = new ConnectionRow[count];
            status = read(pipelineHandle, rows, rows.Length, out count);
        }
        if (status != ReadOk)
            throw new InvalidOperationException(
                $"The connections of pipeline {pipelineHandle} could not be read (status {status}).");

        List<NdhConnectionRow> result = new List<NdhConnectionRow>(count);
        for (int i = 0; i < count; i++)
        {
            ConnectionRow r = rows[i];
            result.Add(new NdhConnectionRow(
                r.RouteIndex, r.IsHost != 0, (NdhBranchOutlet)r.Outlet, r.Station, r.X, r.Y,
                r.PartnerHandle ?? "", r.PinnedProdukt ?? "", r.ResolvedProdukt ?? ""));
        }
        return result;
    }
}
