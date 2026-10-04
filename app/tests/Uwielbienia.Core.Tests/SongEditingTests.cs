using Uwielbienia.Core.Songs;

namespace Uwielbienia.Core.Tests;

public class SongEditingTests : IDisposable
{
    private readonly MarkdownSongParser _parser = new();
    private readonly string _local = Path.Combine(Path.GetTempPath(), "uwielbienia-teksty-" + Guid.NewGuid().ToString("N"));
    private readonly SongLibrary _library;
    private readonly LocalSongEditor _editor;

    public SongEditingTests()
    {
        _library = new SongLibrary(
            new LayeredSongSource(new DirectorySongSource(RepositoryPaths.Songs), new DirectorySongSource(_local)), _parser);
        _editor = new LocalSongEditor(_local, _library);
    }

    public void Dispose()
    {
        if (Directory.Exists(_local))
            Directory.Delete(_local, recursive: true);
    }

    [Fact]
    public void Writer_reproduces_every_songbook_file()
    {
        foreach (var dir in Directory.EnumerateDirectories(RepositoryPaths.Songs))
        {
            var markdown = File.ReadAllText(Path.Combine(dir, DirectorySongSource.FileName)).Replace("\r\n", "\n");
            var song = _parser.Parse(Path.GetFileName(dir), markdown);
            Assert.Equal(markdown.TrimEnd('\n'), MarkdownSongWriter.Write(song).TrimEnd('\n'));
        }
    }

    [Fact]
    public void Editor_text_round_trips_every_songbook_song()
    {
        foreach (var song in _library.Songs)
        {
            var rebuilt = SongDraft.From(song).Build(song.Id, song.Number) with { Source = song.Source };
            Assert.Equal(MarkdownSongWriter.Write(song), MarkdownSongWriter.Write(rebuilt));
        }
    }

    [Fact]
    public void New_song_gets_number_from_1000_codes_and_chorus_after_each_verse()
    {
        var draft = SongDraft.New(SongKind.Song) with
        {
            Title = "Nowa pieśń",
            Parts =
            [
                new DraftPart("V", "", "Pierwsza zwrotka [G D]\ndrugi wers"),
                new DraftPart("V", "", "Druga zwrotka"),
                new DraftPart("C", "", "|: Refren :| {x2} [e C]"),
            ],
        };

        var song = _editor.Save(draft);

        Assert.Equal(LocalSongEditor.FirstLocalNumber, song.Number);
        Assert.Equal("1000-nowa-piesn", song.Id);
        Assert.Equal(SongOrigin.Local, song.Origin);
        Assert.Equal(["V1", "C", "V2", "C"], song.Arrangement);
        Assert.Equal("G", song.Key);
        Assert.Equal("Zwrotka 2", song.FindSection("V2")!.Name);
        var chorus = song.FindSection("C")!.Lines[0];
        Assert.Equal(("Refren", "e C", 2, true, true), (chorus.Text, chorus.Chords, chorus.Repeat, chorus.RepeatStart, chorus.RepeatEnd));
    }

    [Fact]
    public void Text_has_no_number_no_chords_and_parts_in_order()
    {
        var draft = SongDraft.New(SongKind.Text) with
        {
            Title = "Ojcze nasz",
            Category = "Modlitwa",
            Parts = [new DraftPart("O", "", "Ojcze nasz [który jesteś]"), new DraftPart("O", "Wszyscy", "Amen")],
        };

        var text = _editor.Save(draft);

        Assert.True(text.IsText);
        Assert.Null(text.Number);
        Assert.Equal("tekst-ojcze-nasz", text.Id);
        Assert.Equal(["O1", "O2"], text.Arrangement);
        Assert.Equal(("Część 1", "Część 2"), (text.Sections[0].Name, text.Sections[1].Name));
        Assert.Equal("Ojcze nasz [który jesteś]", text.Sections[0].Lines[0].Text);
        Assert.Null(text.Sections[0].Lines[0].Chords);
    }

    [Fact]
    public void Editing_songbook_song_saves_local_copy_that_can_be_reverted()
    {
        var original = _library.Songs.First(s => s.Number == 47);
        var draft = SongDraft.From(original);
        draft = draft with { Parts = [draft.Parts[0] with { Text = "Poprawiony wers" }, .. draft.Parts.Skip(1)] };

        var edited = _editor.Save(draft);

        Assert.Equal(original.Id, edited.Id);
        Assert.Equal(SongOrigin.Modified, edited.Origin);
        Assert.Equal(original.Arrangement, edited.Arrangement);
        Assert.Equal("Poprawiony wers", edited.Sections[0].Lines[0].Text);

        _editor.RevertToShared(original.Id);
        Assert.Equal(SongOrigin.Shared, _library.Find(original.Id)!.Origin);
        Assert.Equal(original.Sections[0].Lines[0].Text, _library.Find(original.Id)!.Sections[0].Lines[0].Text);
    }

    [Fact]
    public void Removing_a_part_drops_it_from_arrangement()
    {
        var original = _library.Songs.First(s => s.Arrangement.Contains("V2") && s.Arrangement.Contains("C"));
        var draft = SongDraft.From(original);
        draft = draft with { Parts = draft.Parts.Where(p => p.OriginalCode != "V2").ToList() };

        var edited = draft.Build(original.Id, original.Number);

        Assert.DoesNotContain("V2", edited.Arrangement);
        Assert.Contains("C", edited.Arrangement);
    }

    [Fact]
    public void Deleting_own_text_removes_it_from_library()
    {
        var text = _editor.Save(SongDraft.New(SongKind.Text) with { Title = "Ogłoszenia", Parts = [new DraftPart("O", "", "Zapraszamy")] });
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _editor.Delete(text.Id);

        Assert.Null(_library.Find(text.Id));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Text_without_title_is_shown_by_its_first_line()
    {
        var text = _editor.Save(SongDraft.New(SongKind.Text) with { Parts = [new DraftPart("O", "", "Pan z wami\nI z duchem twoim")] });

        Assert.Equal("", text.Title);
        Assert.Equal("Pan z wami", text.DisplayTitle);
        Assert.Equal("tekst-pan-z-wami", text.Id);
    }

    [Fact]
    public void Draft_without_title_or_text_is_invalid()
    {
        Assert.NotNull(SongDraft.New(SongKind.Song).Validate());
        Assert.NotNull((SongDraft.New(SongKind.Text) with { Title = "Pusty" }).Validate());
        Assert.NotNull(SongDraft.New(SongKind.Text).Validate());
    }

    [Fact]
    public void Copy_is_an_independent_local_song_with_the_same_content()
    {
        var original = _library.Songs.First(s => s.Number == 47);

        var copy = _editor.Copy(original);
        _editor.Save(SongDraft.From(copy) with { Title = "Zmieniona kopia" });

        Assert.NotEqual(original.Id, copy.Id);
        Assert.True(copy.Number >= LocalSongEditor.FirstLocalNumber);
        Assert.Equal(SongOrigin.Local, copy.Origin);
        Assert.Equal(original.Title, copy.Title);
        Assert.Equal(original.Arrangement, copy.Arrangement);
        Assert.Equal(original.Sections.Count, copy.Sections.Count);
        Assert.Equal(original.Title, _library.Find(original.Id)!.Title);
    }

    [Fact]
    public void Copy_of_untitled_text_stays_a_text()
    {
        var text = _editor.Save(SongDraft.New(SongKind.Text) with { Parts = [new DraftPart("O", "Część 1", "Ojcze nasz, który jesteś w niebie")] });

        var copy = _editor.Copy(text);

        Assert.True(copy.IsText);
        Assert.Null(copy.Number);
        Assert.NotEqual(text.Id, copy.Id);
        Assert.Equal("Ojcze nasz, który jesteś w niebie", copy.DisplayTitle);
    }
}
