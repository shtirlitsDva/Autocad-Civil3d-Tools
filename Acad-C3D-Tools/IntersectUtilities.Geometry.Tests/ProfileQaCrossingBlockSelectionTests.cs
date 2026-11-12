using IntersectUtilities.LongitudinalProfiles.Quality;

public sealed class ProfileQaCrossingBlockSelectionTests
{
    [Fact]
    public void UniqueMatchingNameWinsAmongOtherViewsBlocks()
    {
        var blocks = new[] { (Id: 1, Name: "001_PV"), (Id: 2, Name: "002_PV"), (Id: 3, Name: "003_PV") };
        Assert.Equal(2, QaCrossingBlockSelection.Find(blocks, "002_PV").Match(id => id, message => throw new InvalidOperationException(message)));
    }

    [Fact]
    public void MatchingUsesCaseInsensitiveViewName()
    {
        var blocks = new[] { (Id: 2, Name: "002_pv") };
        Assert.Equal(2, QaCrossingBlockSelection.Find(blocks, "002_PV").Match(id => id, message => throw new InvalidOperationException(message)));
    }

    [Fact]
    public void DuplicateReferencesRequireManualSelection()
    {
        var blocks = new[] { (Id: 2, Name: "002_PV"), (Id: 3, Name: "002_pv") };
        Assert.True(QaCrossingBlockSelection.Find(blocks, "002_PV").Match(_ => false, _ => true));
        Assert.True(QaCrossingBlockSelection.Find(blocks.Reverse(), "002_PV").Match(_ => false, _ => true));
    }

    [Fact]
    public void DifferentViewsBlockCannotBeChosenWhenMatchingNameIsMissing()
    {
        var blocks = new[] { (Id: 1, Name: "001_PV"), (Id: 3, Name: "003_PV") };
        Assert.True(QaCrossingBlockSelection.Find(blocks, "002_PV").Match(_ => false, _ => true));
    }

    [Fact]
    public void EmptyDrawingSpaceRequiresManualSelection()
    {
        Assert.True(QaCrossingBlockSelection.Find(Array.Empty<(int Id, string Name)>(), "002_PV").Match(_ => false, _ => true));
    }
}
