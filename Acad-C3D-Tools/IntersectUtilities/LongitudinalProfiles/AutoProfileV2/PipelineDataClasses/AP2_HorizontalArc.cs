namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_HorizontalArc : PipelineDataBase
    {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public AP2_HorizontalArc(double start, double end, AP2_PipelineData pipeline) : base(pipeline)
        {
            StartStation = start;
            EndStation = end;
        }
    }
}