using Autodesk.Civil.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AP_ProfileViewData : PipelineDataBase
    {
        public string Name { get; set; }
        public ProfileView ProfileView { get; set; }
        public AP_ProfileViewData(string name, ProfileView profileView, AP_PipelineData pipeline) : base(pipeline)
        {
            Name = name;
            ProfileView = profileView;
        }
    }
}