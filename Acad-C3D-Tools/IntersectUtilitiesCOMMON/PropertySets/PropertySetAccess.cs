#if BRICSCAD
using Teigha.DatabaseServices;
#else
using Autodesk.AutoCAD.DatabaseServices;
#endif

namespace IntersectUtilities
{
    /// <summary>
    /// Reads and writes one property set's values on entities. In
    /// IntersectUtilities it is the AEC PropertySetManager; a host without AEC
    /// brings its own (NorsynDrawingTools reads the legacy drawing's sets
    /// natively and keeps its writes in memory).
    /// </summary>
    public interface IPropertySetAccess
    {
        string ReadPropertyString(Entity ent, PSetDefs.Property property);
        void WritePropertyString(Entity ent, PSetDefs.Property property, string value);
        bool FilterPropetyString(Entity ent, PSetDefs.Property property, string value);
    }

    /// <summary>
    /// The two property sets the pipeline network reads: the connectivity graph
    /// (DriGraph) and the pipeline membership (DriPipelineData).
    /// </summary>
    public partial class PropertySetHelper
    {
        public IPropertySetAccess Graph;
        public IPropertySetAccess Pipeline;
        public PSetDefs.DriGraph GraphDef;
        public PSetDefs.DriPipelineData PipelineDef;

        public PropertySetHelper(IPropertySetAccess graph, IPropertySetAccess pipeline)
        {
            Graph = graph;
            GraphDef = new PSetDefs.DriGraph();
            Pipeline = pipeline;
            PipelineDef = new PSetDefs.DriPipelineData();
        }

        public string BelongsToAlignment(Entity ent) =>
            Pipeline.ReadPropertyString(ent, PipelineDef.BelongsToAlignment);
    }
}
