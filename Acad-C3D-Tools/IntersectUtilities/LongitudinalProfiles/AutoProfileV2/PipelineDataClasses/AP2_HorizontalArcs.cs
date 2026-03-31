namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_HorizontalArcs : PipelineDataBase
    {
        public List<AP2_HorizontalArc> HorizontalArcs { get; set; }
        public AP2_HorizontalArcs(AP2_PipelineData pipeline) : base(pipeline) { HorizontalArcs = new(); }
        public AP2_HorizontalArcs(List<AP2_HorizontalArc> arcs, AP2_PipelineData pipeline) : base(pipeline)
        {
            HorizontalArcs = arcs;
        }
    }
}