using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;

using MessagePack;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal class NodeJunctionMsgDto
{
    [Key(0)] public double X { get; set; }
    [Key(1)] public double Y { get; set; }
    [Key(2)] public bool IsRootNode { get; set; }
    [Key(3)] public bool IsBuildingNode { get; set; }
    [Key(4)] public int Degree { get; set; }
    [Key(5)] public int STP_Node { get; set; }
    [Key(6)] public string Name { get; set; } = "";

    public static NodeJunctionMsgDto FromDomain(NodeJunction nj) => new()
    {
        X = nj.Location.X,
        Y = nj.Location.Y,
        IsRootNode = nj.IsRootNode,
        IsBuildingNode = nj.IsBuildingNode,
        Degree = nj.Degree,
        STP_Node = nj.STP_Node,
        Name = nj.Name,
    };

    public NodeJunction ToDomain()
    {
        return new NodeJunction(new Point2D(X, Y))
        {
            IsRootNode = IsRootNode,
            IsBuildingNode = IsBuildingNode,
            Degree = Degree,
            STP_Node = STP_Node,
            Name = Name,
        };
    }
}
