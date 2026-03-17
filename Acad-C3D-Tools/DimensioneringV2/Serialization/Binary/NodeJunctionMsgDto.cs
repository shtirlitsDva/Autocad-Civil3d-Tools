using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;

using MessagePack;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class NodeJunctionMsgDto
{
    [Key(0)] internal double X { get; set; }
    [Key(1)] internal double Y { get; set; }
    [Key(2)] internal bool IsRootNode { get; set; }
    [Key(3)] internal bool IsBuildingNode { get; set; }
    [Key(4)] internal int Degree { get; set; }
    [Key(5)] internal int STP_Node { get; set; }
    [Key(6)] internal string Name { get; set; } = "";
    [Key(7)] internal int NodeId { get; set; } = -1;

    internal static NodeJunctionMsgDto FromDomain(NodeJunction nj) => new()
    {
        X = nj.Location.X,
        Y = nj.Location.Y,
        IsRootNode = nj.IsRootNode,
        IsBuildingNode = nj.IsBuildingNode,
        Degree = nj.Degree,
        STP_Node = nj.STP_Node,
        Name = nj.Name,
        NodeId = nj.NodeId,
    };

    internal NodeJunction ToDomain()
    {
        return new NodeJunction(new Point2D(X, Y))
        {
            IsRootNode = IsRootNode,
            IsBuildingNode = IsBuildingNode,
            Degree = Degree,
            STP_Node = STP_Node,
            Name = Name,
            NodeId = NodeId,
        };
    }
}
