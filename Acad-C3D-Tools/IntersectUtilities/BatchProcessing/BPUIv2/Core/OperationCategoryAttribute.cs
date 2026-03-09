namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OperationCategoryAttribute : Attribute
{
    public string Category { get; }

    public OperationCategoryAttribute(string category) => Category = category;
}
