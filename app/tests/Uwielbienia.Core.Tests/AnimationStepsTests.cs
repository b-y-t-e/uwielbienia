using System.IO.Compression;
using System.Xml.Linq;
using Uwielbienia.Core.Slideshows;

namespace Uwielbienia.Core.Tests;

/// <summary>Kroki animacji (kolejne kliknięcia) jako osobne slajdy — na małej prezentacji OOXML zbudowanej w teście.</summary>
public sealed class AnimationStepsTests : IDisposable
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly string _temp = Directory.CreateTempSubdirectory("kroki-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private const string Shapes = """
        <p:sp><p:nvSpPr><p:cNvPr id="2" name="Tytuł"/></p:nvSpPr></p:sp>
        <p:sp><p:nvSpPr><p:cNvPr id="3" name="Obraz"/></p:nvSpPr></p:sp>
        <p:sp><p:nvSpPr><p:cNvPr id="4" name="Punkty"/></p:nvSpPr>
          <p:txBody><a:p><a:r><a:t>pierwszy</a:t></a:r><a:r><a:t> </a:t></a:r><a:r><a:t>akapit</a:t></a:r></a:p><a:p><a:pPr><a:buChar char="•"/></a:pPr><a:r><a:rPr><a:solidFill><a:srgbClr val="000000"/></a:solidFill></a:rPr><a:t>drugi</a:t></a:r></a:p></p:txBody></p:sp>
        """;

    /// <summary>Kliknięcie 1: obraz (3) i razem z nim drugi akapit (4); kliknięcie 2: tytuł (2) znika.</summary>
    private const string Timing = """
        <p:timing><p:tnLst><p:par><p:cTn id="1" nodeType="tmRoot"><p:childTnLst><p:seq><p:cTn id="2" nodeType="mainSeq"><p:childTnLst>
          <p:par><p:cTn id="3"><p:childTnLst><p:par><p:cTn id="4"><p:childTnLst>
            <p:par><p:cTn id="5" presetClass="entr" nodeType="clickEffect"><p:childTnLst><p:set><p:cBhvr><p:tgtEl><p:spTgt spid="3"/></p:tgtEl></p:cBhvr></p:set></p:childTnLst></p:cTn></p:par>
            <p:par><p:cTn id="6" presetClass="entr" nodeType="withEffect"><p:childTnLst><p:set><p:cBhvr><p:tgtEl><p:spTgt spid="4"><p:txEl><p:pRg st="1" end="1"/></p:txEl></p:spTgt></p:tgtEl></p:cBhvr></p:set></p:childTnLst></p:cTn></p:par>
          </p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par>
          <p:par><p:cTn id="7"><p:childTnLst><p:par><p:cTn id="8"><p:childTnLst>
            <p:par><p:cTn id="9" presetClass="exit" nodeType="clickEffect"><p:childTnLst><p:set><p:cBhvr><p:tgtEl><p:spTgt spid="2"/></p:tgtEl></p:cBhvr></p:set></p:childTnLst></p:cTn></p:par>
          </p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par>
        </p:childTnLst></p:cTn></p:seq></p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>
        """;

    private string Presentation(string name, bool animated)
    {
        var file = Path.Combine(_temp, name);
        Write(file, animated);
        return file;
    }

    /// <summary>Jednoslajdowa prezentacja (z dwoma kliknięciami animacji, gdy <paramref name="animated"/>).</summary>
    internal static void Write(string file, bool animated)
    {
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        void Add(string part, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(part).Open());
            writer.Write(xml);
        }
        Add("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/ppt/slides/slide1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slide+xml"/></Types>""");
        Add("ppt/presentation.xml", """<p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><p:sldIdLst><p:sldId id="256" r:id="rId1"/></p:sldIdLst></p:presentation>""");
        Add("ppt/_rels/presentation.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml"/></Relationships>""");
        Add("ppt/slides/slide1.xml", $"""<p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><p:cSld><p:spTree>{Shapes}</p:spTree></p:cSld>{(animated ? Timing : "")}</p:sld>""");
        Add("ppt/slides/_rels/slide1.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide" Target="../notesSlides/notesSlide1.xml"/></Relationships>""");
    }

    /// <summary>Slajdy w kolejności pokazu: identyfikatory kształtów i czy drugi akapit jest widoczny.</summary>
    private static List<(string[] Shapes, bool SecondParagraph)> Slides(string file)
    {
        using var zip = ZipFile.OpenRead(file);
        XDocument Read(string part)
        {
            using var stream = zip.GetEntry(part)!.Open();
            return XDocument.Load(stream);
        }
        var rels = Read("ppt/_rels/presentation.xml.rels").Root!.Elements().ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        return Read("ppt/presentation.xml").Descendants(P + "sldId").Select(id =>
        {
            var slide = Read("ppt/" + rels[(string)id.Attribute(r + "id")!]);
            Assert.Null(slide.Root!.Element(P + "timing"));
            var shapes = slide.Descendants(P + "cNvPr").Select(c => (string)c.Attribute("id")!).ToArray();
            var second = slide.Descendants(A + "p").Skip(1).FirstOrDefault();
            return (shapes, second is not null && second.Descendants(A + "noFill").Any() == false);
        }).ToList();
    }

    [Fact]
    public void Every_click_becomes_a_slide_with_the_state_after_it()
    {
        var expanded = Path.Combine(_temp, "kroki.pptx");
        var source = Presentation("animacje.pptx", animated: true);

        Assert.True(AnimationSteps.HasSteps(source));
        Assert.True(AnimationSteps.TryExpand(source, expanded));

        var slides = Slides(expanded);
        Assert.Equal(3, slides.Count);
        Assert.Equal(["2", "4"], slides[0].Shapes);       // przed kliknięciem: bez obrazu, drugi akapit ukryty …
        Assert.False(slides[0].SecondParagraph);
        Assert.Equal(["2", "3", "4"], slides[1].Shapes);  // … po pierwszym: obraz i akapit
        Assert.True(slides[1].SecondParagraph);
        Assert.Equal(["3", "4"], slides[2].Shapes);       // po drugim: tytuł zniknął
    }

    [Fact]
    public void Presentation_without_click_steps_or_in_another_format_is_left_alone()
    {
        var expanded = Path.Combine(_temp, "kroki.pptx");

        Assert.False(AnimationSteps.HasSteps(Presentation("bez.pptx", animated: false)));
        Assert.False(AnimationSteps.TryExpand(Path.Combine(_temp, "bez.pptx"), expanded));
        Assert.False(AnimationSteps.TryExpand(Presentation("stary.ppt", animated: true), expanded));
        Assert.False(File.Exists(expanded));
    }

    [Fact]
    public void Broken_animation_section_leaves_the_presentation_alone_and_the_source_unchanged()
    {
        var source = Path.Combine(_temp, "zla.pptx");
        Write(source, animated: true);
        using (var zip = ZipFile.Open(source, ZipArchiveMode.Update))
        {
            var slide = zip.GetEntry("ppt/slides/slide1.xml")!;
            string xml;
            using (var reader = new StreamReader(slide.Open()))
                xml = reader.ReadToEnd();
            slide.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("ppt/slides/slide1.xml").Open());
            writer.Write(xml.Replace("<p:pRg st=\"1\" end=\"1\"/>", "<p:pRg/>", StringComparison.Ordinal));
        }
        var expanded = Path.Combine(_temp, "kroki.pptx");

        Assert.False(AnimationSteps.HasSteps(source));
        Assert.False(AnimationSteps.TryExpand(source, expanded));
        Assert.False(File.Exists(expanded));
    }

    [Fact]
    public void Spaces_in_their_own_text_runs_survive_expanding()
    {
        var source = Presentation("spacje.pptx", animated: true);
        var target = Path.Combine(_temp, "kroki.pptx");

        Assert.True(AnimationSteps.TryExpand(source, target));

        using var zip = ZipFile.OpenRead(target);
        var slides = zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, slides.Count);
        Assert.All(slides, entry =>
        {
            using var reader = new StreamReader(entry.Open());
            Assert.Contains("<a:t> </a:t>", reader.ReadToEnd());
        });
    }

    [Fact]
    public void Expanding_does_not_change_the_source_file()
    {
        var source = Presentation("animacje.pptx", animated: true);
        var before = File.ReadAllBytes(source);

        Assert.True(AnimationSteps.TryExpand(source, Path.Combine(_temp, "kroki.pptx")));

        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void Step_slides_keep_the_slide_number_of_the_original()
    {
        var source = Presentation("numer.pptx", animated: true);
        using (var zip = ZipFile.Open(source, ZipArchiveMode.Update))
        {
            var slide = zip.GetEntry("ppt/slides/slide1.xml")!;
            string xml;
            using (var reader = new StreamReader(slide.Open()))
                xml = reader.ReadToEnd();
            slide.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("ppt/slides/slide1.xml").Open());
            writer.Write(xml.Replace("<a:t>pierwszy</a:t></a:r>", "<a:t>pierwszy</a:t></a:r><a:fld id=\"{1}\" type=\"slidenum\"><a:t>‹#›</a:t></a:fld>", StringComparison.Ordinal));
        }
        var expanded = Path.Combine(_temp, "kroki.pptx");

        Assert.True(AnimationSteps.TryExpand(source, expanded));

        using var result = ZipFile.OpenRead(expanded);
        var slides = result.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, slides.Count);
        Assert.All(slides, e =>
        {
            using var stream = e.Open();
            var slide = XDocument.Load(stream);
            Assert.Empty(slide.Descendants(A + "fld"));
            Assert.Contains(slide.Descendants(A + "t"), t => t.Value == "1");
        });
    }

    /// <summary>Eksporter, który nie otwiera przerobionej kopii (plik spoza folderu testu).</summary>
    private sealed class RejectingCopyExporter(string acceptedFolder) : ISlideExporter
    {
        public string Name => "Fake";

        public bool IsAvailable => true;

        public Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
        {
            File.WriteAllText(Path.Combine(outDir, "slide001.png"), "");
            if (Path.GetDirectoryName(presentation) != acceptedFolder)
                throw new InvalidOperationException("nie do otwarcia");
            File.WriteAllText(Path.Combine(outDir, "oryginal.png"), "");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Export_falls_back_to_the_original_when_the_expanded_copy_fails()
    {
        var source = Presentation("animacje.pptx", animated: true);
        var outDir = Directory.CreateDirectory(Path.Combine(_temp, "slajdy")).FullName;

        await new AnimationStepsExporter(new RejectingCopyExporter(_temp)).ExportAsync(source, outDir, 1920, new Progress<string>());

        Assert.Equal(["oryginal.png", "slide001.png"], Directory.GetFiles(outDir).Select(Path.GetFileName).Order());
    }
}
