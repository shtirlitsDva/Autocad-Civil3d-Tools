using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;

namespace NTRExport.TopologyModel
{
    internal sealed class GenericFitting : TFitting
    {
        public GenericFitting(Handle source, PipelineElementType kind)
            : base(source, kind) { }
    }
}

