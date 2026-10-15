using System.Text.Json;
using Uwielbienia.Core.Plans;
using Uwielbienia.Core.Presentation;
using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

/// <summary>
/// Układ części w planie po zmianach treści w edytorze: nowe części się pojawiają, usunięte w planie zostają usunięte,
/// części usunięte w edytorze znikają, a zmiana kolejności w edytorze nie przenosi pominięć na inną treść.
/// </summary>
public sealed class PlanLayoutTests : IDisposable
{
    private readonly string _local = Directory.CreateTempSubdirectory("uwielbienia-uklad-").FullName;
    private readonly SongLibrary _library;
    private readonly LocalSongEditor _editor;

    public PlanLayoutTests()
    {
        _library = new SongLibrary(new DirectorySongSource(_local), new MarkdownSongParser());
        _editor = new LocalSongEditor(_local, _library);
    }

    public void Dispose() => Directory.Delete(_local, recursive: true);

    private Song Text(params string[] parts) =>
        _editor.Save(SongDraft.New(SongKind.Text) with { Title = "Tekst", Parts = [.. parts.Select(p => new DraftPart("O", "", p))] });

    private Song Song(params (string Kind, string Text)[] parts) =>
        _editor.Save(SongDraft.New(SongKind.Song) with { Title = "Pieśń", Parts = [.. parts.Select(p => new DraftPart(p.Kind, "", p.Text))] });

    /// <summary>Edycja jak w edytorze: części z dotychczasowymi kodami, w podanej kolejności (treść → kod z pliku).</summary>
    private Song Edit(Song song, params (string Kind, string Text)[] parts)
    {
        var draft = SongDraft.From(song);
        var byText = draft.Parts.ToDictionary(p => p.Text);
        return _editor.Save(draft with
        {
            Parts = [.. parts.Select(p => byText.TryGetValue(p.Text, out var old) ? old with { Kind = p.Kind } : new DraftPart(p.Kind, "", p.Text))],
        });
    }

    private static string Show(SongPlanItem item, Song song) =>
        string.Join(" ", item.LayoutFor(song).Select(e => song.FindSection(e.Code)!.Lines[0].Text + (e.Sung ? "" : "(-)")));

    private static SongPlanItem Customized(Song song, params (string Code, bool Sung)[] layout) =>
        SongPlanItem.For(song.Id).WithLayout(song, [.. layout.Select(e => new LayoutEntry(e.Code, e.Sung))]);

    [Fact]
    public void Part_added_in_the_editor_appears_in_a_plan_with_its_own_layout()
    {
        var song = Song(("V", "zwrotka"), ("C", "refren"), ("V", "zwrotka2"));
        var item = Customized(song, ("C", true), ("V1", true), ("V2", true)); // refren na początku

        song = Edit(song, ("V", "zwrotka"), ("C", "refren"), ("B", "mostek"), ("V", "zwrotka2"));

        Assert.Equal("refren zwrotka zwrotka2 mostek", Show(item, song));
        Assert.Contains("B", new SongPlanItemType(_library, new SectionSlideBuilder()).Present(item)!.Slides.Select(s => s.SectionCode));
    }

    [Fact]
    public void Part_removed_in_the_plan_stays_removed_after_other_edits()
    {
        var song = Song(("V", "zwrotka"), ("C", "refren"), ("V", "zwrotka2"));
        var item = Customized(song, ("V1", true), ("V2", true)); // refren usunięty w planie

        song = Edit(song, ("V", "zwrotka"), ("C", "refren"), ("V", "zwrotka2"), ("E", "koniec"));

        Assert.Equal("zwrotka zwrotka2 koniec", Show(item, song));
    }

    [Fact]
    public void Part_deleted_in_the_editor_leaves_no_empty_part_in_the_plan()
    {
        var text = Text("jeden", "dwa", "trzy");
        var item = Customized(text, ("O1", true), ("O2", false), ("O3", true));

        text = Edit(text, ("O", "dwa"), ("O", "trzy"));

        Assert.Equal("dwa(-) trzy", Show(item, text));
    }

    [Fact]
    public void Reordering_in_the_editor_keeps_skipped_parts_on_the_same_content()
    {
        var text = Text("jeden", "dwa", "trzy");
        var item = Customized(text, ("O1", true), ("O2", false), ("O3", true)); // „dwa” pominięte

        text = Edit(text, ("O", "trzy"), ("O", "dwa"), ("O", "jeden"));

        // tekst: kolejność z edytora, pominięcie zostaje przy „dwa”
        Assert.Equal("trzy dwa(-) jeden", Show(item, text));
        Assert.Equal(["Część 1", "Część 2", "Część 3"], text.Sections.Select(s => s.Name));
    }

    [Fact]
    public void Changing_the_kind_of_a_part_replaces_it_in_the_plan()
    {
        var song = Song(("V", "zwrotka"), ("V", "druga"));
        var item = Customized(song, ("V2", true), ("V1", true));

        song = Edit(song, ("V", "zwrotka"), ("C", "druga"));

        Assert.Equal("zwrotka druga", Show(item, song));
        Assert.Equal(["V1", "C"], song.Arrangement);
    }

    [Fact]
    public void Codes_stay_stable_and_new_parts_get_free_codes()
    {
        var song = Song(("V", "a"), ("V", "b"), ("C", "r"));

        song = Edit(song, ("V", "b"), ("C", "r"), ("V", "nowa"));

        Assert.Equal(["V2", "C", "V1"], song.Sections.Select(s => s.Code));
        Assert.Equal(["Zwrotka 2", "Refren", "Zwrotka 1"], song.Sections.Select(s => s.Name));
    }

    [Fact]
    public void Plan_saved_before_removed_parts_were_tracked_shows_parts_added_later()
    {
        // dane z planu użytkownika: własny układ [O2, O1], potem w edytorze dopisane „gg”
        var text = Text("Asia", "Uwielbienie jest piękne", "gg");
        var item = new SongPlanItem(Guid.NewGuid(), text.Id, ["O2", "O1"], [new("O2"), new("O1")]);

        Assert.Equal("Asia Uwielbienie jest piękne gg", Show(item, text));
    }

    [Fact]
    public void Default_layout_follows_the_file_and_removed_parts_survive_saving()
    {
        var song = Song(("V", "zwrotka"), ("C", "refren"));
        var store = new JsonPlanStore(Path.Combine(_local, "plany"));
        var removed = Customized(song, ("V1", true));
        store.Save(Plan.Create("P", null).Insert(0, removed));

        var loaded = (SongPlanItem)store.LoadAll().Single().Items.Single();

        Assert.Equal(["C"], loaded.Removed);
        Assert.Null(Customized(song, ("V1", true), ("C", true)).Layout);
        var old = JsonSerializer.Deserialize<PlanItem>("""{"type":"song","id":"00000000-0000-0000-0000-000000000001","songId":"x","arrangement":null}""", JsonPlanStore.JsonOptions);
        Assert.Null(((SongPlanItem)old!).Removed);
    }
}
