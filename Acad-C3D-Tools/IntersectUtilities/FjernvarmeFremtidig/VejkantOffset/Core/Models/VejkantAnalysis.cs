using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models
{
    public sealed class PipelineSegment
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
    public abstract class PipelinePrimitiveDomain
    {
        public PipelinePrimitiveDomain(PipelineSegment segment) { Owner = segment; }
        public PipelineSegment Owner { get; init; }
        public short ColorIndex => Owner.LayerColor;
        public double Width => Owner.Width;
    }

    public sealed class PipelineLinePrimitiveDomain : PipelinePrimitiveDomain
    {
        public PipelineLinePrimitiveDomain(PipelineSegment segment) : base(segment) { }
        public Point3d Start { get; init; }
        public Point3d End { get; init; }
    }

    public sealed class PipelineArcPrimitiveDomain : PipelinePrimitiveDomain
    {
        public PipelineArcPrimitiveDomain(PipelineSegment segment) : base(segment) { }
        public Point3d Center { get; init; }
        public double Radius { get; init; }
        public double StartAngle { get; init; }
        public double EndAngle { get; init; }
        public bool IsCCW { get; init; }
    }

    public sealed class VejkantAnalysis
    {
        public double Length { get; init; }
        public string? ChosenSideLabel { get; init; }
        public IReadOnlyList<PipelineSegment> Segments { get; init; } = Array.Empty<PipelineSegment>();
        public IReadOnlyList<SegmentHit> GkIntersections { get; init; } = Array.Empty<SegmentHit>();
    }
}


