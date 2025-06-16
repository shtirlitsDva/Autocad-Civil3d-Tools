namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal abstract class PipelineDataBase
    {
        protected AP_PipelineData _pipeLine;
        public PipelineDataBase(AP_PipelineData pipeLine)
        {
            _pipeLine = pipeLine;
        }
    }
}