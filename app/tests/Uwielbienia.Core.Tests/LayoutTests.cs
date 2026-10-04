using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

public class LayoutTests
{
    private static readonly Song Song = new("001-x", 1, "X", "Uwielbienie", null, null, ["V1", "C", "V2", "C"],
        [new Section("V1", "Zwrotka 1", 1, []), new Section("C", "Refren", 1, []), new Section("V2", "Zwrotka 2", 1, [])]);

    [Fact]
    public void Item_without_layout_follows_song()
    {
        var layout = SongPlanItem.For(Song.Id).LayoutFor(Song);
        Assert.Equal(["V1", "C", "V2", "C"], layout.Select(e => e.Code));
        Assert.All(layout, e => Assert.True(e.Sung));
    }

    [Fact]
    public void Old_arrangement_marks_missing_parts_as_skipped()
    {
        var layout = (SongPlanItem.For(Song.Id) with { Arrangement = ["V1", "C", "C"] }).LayoutFor(Song);
        Assert.Equal([true, true, false, true], layout.Select(e => e.Sung));
    }

    [Fact]
    public void Reordered_and_repeated_layout_is_sung_in_that_order()
    {
        var item = SongPlanItem.For(Song.Id).WithLayout(Song,
            [new("C"), new("V1"), new("C"), new("V2", Sung: false), new("C")]);
        Assert.Equal(["C", "V1", "C", "C"], item.Arrangement);
        Assert.Equal(5, item.LayoutFor(Song).Count);
    }

    [Fact]
    public void Layout_equal_to_song_goes_back_to_following_the_file()
    {
        var item = (SongPlanItem.For(Song.Id) with { Arrangement = ["V1"] })
            .WithLayout(Song, [new("V1"), new("C"), new("V2"), new("C")]);
        Assert.Null(item.Arrangement);
        Assert.Null(item.Layout);
    }
}
