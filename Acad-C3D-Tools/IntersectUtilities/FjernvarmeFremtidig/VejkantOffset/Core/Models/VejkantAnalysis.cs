using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models
{
    public sealed class PipelineSegment
    {
        public List<PipelinePrimitiveDomain> primitives = new();
        public PipeSystemEnum PipeSystem { get; init; }
        public PipeSeriesEnum PipeSeries { get; init; }
        public int PipeDim { get; init; }
        public PipeTypeEnum PipeType => GetPipeTypeByAvailability(PipeSystem, PipeDim);        
        public double PipeKod => GetPipeKOd(PipeSystem, PipeDim, PipeType, PipeSeries);
        public short LayerColor => GetLayerColor(PipeSystem, PipeType);
        public string LayerName
        {
            get
            {
                string systemString = GetSystemString(PipeSystem);
                return string.Concat(
                    "FJV-", PipeType, "-", systemString, PipeDim).ToUpper();
            }
        }
    }
    public abstract class PipelinePrimitiveDomain
    {
        public PipelinePrimitiveDomain(PipelineSegment segment) { Owner = segment; }
        public PipelineSegment Owner { get; init; }
        public short ColorIndex => Owner.LayerColor;
        public double Width => Owner.PipeKod / 1000;
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
        public IReadOnlyList<PipelinePrimitiveDomain> Segments { get; init; } = Array.Empty<PipelinePrimitiveDomain>();
        public IReadOnlyList<SegmentHit> GkIntersections { get; init; } = Array.Empty<SegmentHit>();
    }
}


