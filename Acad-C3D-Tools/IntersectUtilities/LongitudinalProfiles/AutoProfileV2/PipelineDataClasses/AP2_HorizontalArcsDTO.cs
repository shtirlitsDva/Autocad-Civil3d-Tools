namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_HorizontalArcsDTO
    {
        public double[][] HorizontalArcsData { get; set; }
        public AP2_HorizontalArcsDTO(List<AP2_HorizontalArc>? arcs)
        {
            if (arcs == null || arcs.Count == 0)
            {
                HorizontalArcsData = [];
                return;
            }

            HorizontalArcsData = arcs.Select(arc => new double[]
            {
                arc.StartStation,
                arc.EndStation,
            }).ToArray();
        }
    }
}