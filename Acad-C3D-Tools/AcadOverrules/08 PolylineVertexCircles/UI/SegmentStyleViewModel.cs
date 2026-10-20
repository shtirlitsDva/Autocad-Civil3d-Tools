using CommunityToolkit.Mvvm.ComponentModel;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// The editable state of one <see cref="SegmentStyle"/> - how one class of segment is
    /// drawn when the profile overrides it. A profile owns two of these, so the two segment
    /// tabs of the Appearance group are the same template bound to different instances.
    /// </summary>
    public partial class SegmentStyleViewModel : LineStyleViewModel
    {
        [ObservableProperty]
        private bool isOverridden;

        public SegmentStyleViewModel(SegmentStyle style)
            : base(style)
        {
            isOverridden = style.Override;
        }

        public new SegmentStyle ToModel()
        {
            SegmentStyle model = CopyTo(new SegmentStyle());
            model.Override = IsOverridden;
            return model;
        }
    }
}
