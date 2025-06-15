namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_HorizontalArc : PipelineDataBase
    {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public AP_HorizontalArc(double start, double end, AP_PipelineData pipeline) : base(pipeline)
        {
            StartStation = start;
            EndStation = end;
        }
    }
}