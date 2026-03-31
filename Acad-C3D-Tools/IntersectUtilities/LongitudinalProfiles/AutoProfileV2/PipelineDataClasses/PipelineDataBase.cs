namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal abstract class PipelineDataBase
    {
        protected AP2_PipelineData _pipeLine;
        public PipelineDataBase(AP2_PipelineData pipeLine)
        {
            _pipeLine = pipeLine;
        }
    }
}