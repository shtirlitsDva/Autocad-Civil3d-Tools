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
    //sizeof 280
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BranchConnectRequest
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string MainHandle;   //off 0
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string BranchHandle; //off 64
        public int BranchAtStart;                                                         //off 128
        public int Outlet;                                                                //off 132
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Produkt;      //off 136
        public double SiteX;                                                              //off 264
        public double SiteY;                                                              //off 272
    }

    //sizeof 1088
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
        public int Correction;      //off 1072
        public int Reserved;        //off 1076
        public double CornerOffsetM; //off 1080
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
        NsDhModule.RequireLayout<BranchConnectRequest>(280);
        NsDhModule.RequireLayout<BranchConnectResult>(1088);
        NsDhModule.RequireLayout<ConnectionRow>(360);
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
            SiteX = request.Site.X,
            SiteY = request.Site.Y,
        };

        (NdhConnectStatus named, string detail) = NsDhModule.Named(
            connect(native, out BranchConnectResult r), r.Detail, NdhConnectStatus.Failed);

        return new NdhConnectOutcome(
            named, r.MainVertexIndex, r.DeviationDeg, r.EndMoveM, r.LargestMoveM, r.PortX, r.PortY,
            (NdhCornerCorrection)r.Correction, r.CornerOffsetM, detail);
    }

    public IReadOnlyList<NdhConnectionRow> ReadConnections(string pipelineHandle)
    {
        ReadConnectionsFn read = NsDhModule.Resolve<ReadConnectionsFn>(
            NsDhSurface.PipelineRead, "NsDh_ReadPipelineConnections");

        ConnectionRow[] rows = new ConnectionRow[8];
        (NdhPipelineReadStatus status, string said) = NsDhModule.Named(
            read(pipelineHandle, rows, rows.Length, out int count), null, NdhPipelineReadStatus.Failed);
        if (status == NdhPipelineReadStatus.BufferTooSmall)
        {
            rows = new ConnectionRow[count];
            (status, said) = NsDhModule.Named(
                read(pipelineHandle, rows, rows.Length, out count), null, NdhPipelineReadStatus.Failed);
        }
        if (status != NdhPipelineReadStatus.Ok)
            throw new InvalidOperationException(
                $"The connections of pipeline {pipelineHandle} could not be read ({status}). {said}".TrimEnd());

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

/// <summary>The pipeline-read exports' status codes (kNsDhPipelineRead*).</summary>
internal enum NdhPipelineReadStatus
{
    Ok = 0,
    BadArgs = -1,
    /// <summary>The handle names no live Pipeline.</summary>
    NotAPipeline = -2,
    /// <summary>The rows did not fit; the count says how many to ask for.</summary>
    BufferTooSmall = -3,
    Failed = -4,
}
