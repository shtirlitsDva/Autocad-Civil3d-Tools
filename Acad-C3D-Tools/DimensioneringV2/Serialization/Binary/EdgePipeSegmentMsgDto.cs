using MessagePack;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class EdgePipeSegmentMsgDto
{
    [Key(0)] internal int SourceIndex { get; set; }
    [Key(1)] internal int TargetIndex { get; set; }
    [Key(2)] internal AnalysisFeatureMsgDto PipeSegment { get; set; }
    [Key(3)] internal int Level { get; set; }
}
