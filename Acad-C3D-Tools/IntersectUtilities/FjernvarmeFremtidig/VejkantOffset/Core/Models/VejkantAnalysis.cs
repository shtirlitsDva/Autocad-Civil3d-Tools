using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models
{
    internal sealed class PipelineSegment
    {
        public PipelineSegment(Polyline polyline, PipeSeriesEnum series)
        {
            PipeSystem = GetPipeSystem(polyline);
            PipeDim = GetPipeDN(polyline);
            PipeSeries = series;
        }
        public PipelineSegment(PipeSystemEnum pipeSystem, int pipeDim, PipeSeriesEnum series)
        {
            PipeSystem = pipeSystem;
            PipeDim = pipeDim;
            PipeSeries = series;
        }
        public List<PipelinePrimitiveDomain> Primitives = new();
        public PipeSystemEnum PipeSystem { get; init; }
        public PipeSeriesEnum PipeSeries { get; init; }
        public int PipeDim { get; init; }
        public PipeTypeEnum PipeType => GetPipeTypeByAvailability(PipeSystem, PipeDim);
        public double PipeKod => GetPipeKOd(PipeSystem, PipeDim, PipeType, PipeSeries);
        public short LayerColor => GetLayerColor(PipeSystem, PipeType);
        public string LayerName => string.Concat(
            "FJV-", PipeType, "-", GetSystemString(PipeSystem), PipeDim).ToUpper();
        public double Width => PipeKod / 1000;

    }
    internal abstract class PipelinePrimitiveDomain
    {
        public PipelinePrimitiveDomain(PipelineSegment segment) { Owner = segment; }
        public PipelineSegment Owner { get; init; }
        public short ColorIndex => Owner.LayerColor;
        public double Width => Owner.Width;
    }

    internal sealed class PipelineLinePrimitiveDomain : PipelinePrimitiveDomain
    {
        public PipelineLinePrimitiveDomain(PipelineSegment segment) : base(segment) { }
        public Point3d Start { get; init; }
        public Point3d End { get; init; }
    }

    internal sealed class PipelineArcPrimitiveDomain : PipelinePrimitiveDomain
    {
        public PipelineArcPrimitiveDomain(PipelineSegment segment) : base(segment) { }
        public Point3d Center { get; init; }
        public double Radius { get; init; }
        public double StartAngle { get; init; }
        public double EndAngle { get; init; }
        public bool IsCCW { get; init; }
    }

    internal sealed class VejkantAnalysis(
        Line workingLine,
        IReadOnlyList<PipelineSegment> segments,
        IReadOnlyList<Segment2d> gkIntersections)
    {
        public Line WorkingLine { get; init; } = workingLine;
        public IReadOnlyList<PipelineSegment> Segments { get; init; } = segments;
        public IReadOnlyList<Segment2d> GkIntersections { get; init; } = gkIntersections;
    }
}


