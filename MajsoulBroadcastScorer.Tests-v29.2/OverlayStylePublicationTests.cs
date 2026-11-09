using MajsoulBroadcastScorer.Core.V27;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class OverlayStylePublicationTests
{
    [Fact]
    public void SelectingGapConditions_WaitsForConfirmationBeforePublishing()
    {
        var publication = new OverlayStylePublication(OverlayLayoutStyle.VerticalAvatar);

        var publishedImmediately = publication.Select(OverlayLayoutStyle.GapConditions);

        Assert.False(publishedImmediately);
        Assert.True(publication.HasPendingPublication);
        Assert.Equal(OverlayLayoutStyle.GapConditions, publication.PreviewStyle);
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, publication.PublishedStyle);

        Assert.True(publication.Confirm());
        Assert.False(publication.HasPendingPublication);
        Assert.Equal(OverlayLayoutStyle.GapConditions, publication.PublishedStyle);
    }

    [Fact]
    public void SelectingRegularStyle_PublishesImmediately()
    {
        var publication = new OverlayStylePublication(OverlayLayoutStyle.GapConditions);

        var publishedImmediately = publication.Select(OverlayLayoutStyle.HorizontalPlayers);

        Assert.True(publishedImmediately);
        Assert.False(publication.HasPendingPublication);
        Assert.Equal(OverlayLayoutStyle.HorizontalPlayers, publication.PublishedStyle);
    }

    [Fact]
    public void SelectingTeamMode_WaitsForConfirmationBeforePublishing()
    {
        var publication = new OverlayStylePublication(OverlayLayoutStyle.VerticalAvatar);

        var publishedImmediately = publication.Select(OverlayLayoutStyle.TeamMode);

        Assert.False(publishedImmediately);
        Assert.True(publication.HasPendingPublication);
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, publication.PublishedStyle);
        Assert.Equal(OverlayLayoutStyle.TeamMode, publication.PreviewStyle);
        Assert.True(publication.Confirm());
        Assert.Equal(OverlayLayoutStyle.TeamMode, publication.PublishedStyle);
    }

    [Fact]
    public void LeavingPendingGapConditions_CancelsThePendingPublication()
    {
        var publication = new OverlayStylePublication(OverlayLayoutStyle.VerticalAvatar);
        publication.Select(OverlayLayoutStyle.GapConditions);

        publication.Select(OverlayLayoutStyle.VerticalAvatar);

        Assert.False(publication.HasPendingPublication);
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, publication.PreviewStyle);
        Assert.Equal(OverlayLayoutStyle.VerticalAvatar, publication.PublishedStyle);
    }
}
