using Autodesk.Civil.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_ProfileViewData : PipelineDataBase
    {
        public string Name { get; set; }
        public ProfileView ProfileView { get; set; }
        public AP2_ProfileViewData(string name, ProfileView profileView, AP2_PipelineData pipeline) : base(pipeline)
        {
            Name = name;
            ProfileView = profileView;
        }
    }
}