using MessagePack;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal class EdgePipeSegmentMsgDto
{
    [Key(0)] public int SourceIndex { get; set; }
    [Key(1)] public int TargetIndex { get; set; }
    [Key(2)] public AnalysisFeatureMsgDto PipeSegment { get; set; }
    [Key(3)] public int Level { get; set; }
}
