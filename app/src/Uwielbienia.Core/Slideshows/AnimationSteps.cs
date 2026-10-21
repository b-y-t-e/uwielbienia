using System.IO.Compression;
using System.Xml.Linq;

namespace Uwielbienia.Core.Slideshows;

/// <summary>
/// Kroki animacji jako osobne slajdy: slajd, na którym elementy pojawiają się (albo znikają) po kolejnych kliknięciach,
/// staje się kilkoma slajdami — po jednym na każdy stan, jak w PowerPoincie po każdym kliknięciu. Pracuje na kopii pliku
/// (OOXML: <c>.pptx .ppsx .pptm .ppsm</c>), więc działa z każdym eksporterem.
/// Uwzględnia wejścia i wyjścia z głównej sekwencji (całe kształty i akapity tekstu); wyróżnienia i ruch — w stanie
/// końcowym, wyzwalacze (kliknięcie w kształt) — pominięte.
/// </summary>
public static class AnimationSteps
{
    public static readonly IReadOnlyList<string> Extensions = [".pptx", ".ppsx", ".pptm", ".ppsm"];

    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string SlideRelType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide";
    private const string NotesRelType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide";

    /// <summary>Wejście (<c>Show</c>) albo wyjście kształtu lub akapitów (<paramref name="Paragraphs"/>; <c>null</c> = cały kształt) po kliknięciu <paramref name="Step"/>.</summary>
    private sealed record Change(int Step, string ShapeId, (int First, int Last)? Paragraphs, bool Show);

    /// <summary>
    /// Zapisuje w <paramref name="target"/> kopię prezentacji z krokami animacji jako slajdami. <c>false</c> = plik nie ma
    /// kroków albo nie jest OOXML — wtedy <paramref name="target"/> nie powstaje.
    /// </summary>
    public static bool TryExpand(string source, string target)
    {
        if (!IsOoxml(source))
            return false;
        try
        {
            File.Copy(source, target, overwrite: true);
            bool expanded;
            using (var zip = ZipFile.Open(target, ZipArchiveMode.Update))
                expanded = Expand(zip);
            if (!expanded)
                File.Delete(target);
            return expanded;
        }
        catch (Exception)
        {
            // nietypowy plik — eksport bez kroków, jak dawniej
            try
            {
                File.Delete(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // zostaje w folderze tymczasowym eksportu, który i tak jest usuwany
            }
            return false;
        }
    }

    /// <summary>Prezentacja ma kroki animacji (kliknięcia w głównej sekwencji jakiegoś slajdu).</summary>
    public static bool HasSteps(string file)
    {
        if (!IsOoxml(file))
            return false;
        try
        {
            using var zip = ZipFile.OpenRead(file);
            return zip.Entries
                .Where(e => IsSlidePart(e.FullName))
                .Any(e =>
                {
                    using var stream = e.Open();
                    Changes(XDocument.Load(stream, LoadOptions.PreserveWhitespace), out var steps);
                    return steps > 0;
                });
        }
        catch (Exception)
        {
            // każdy błąd nietypowego pliku = eksport z oryginału
            return false;
        }
    }

    private static bool IsOoxml(string file) => Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());

    private const string SlidePartPrefix = "ppt/slides/slide";

    private static bool IsSlidePart(string name) =>
        name.StartsWith(SlidePartPrefix, StringComparison.Ordinal) && name.EndsWith(".xml", StringComparison.Ordinal);

    /// <summary>Slajd prezentacji: wpis na liście, część pliku, treść, zmiany widoczności i liczba kliknięć.</summary>
    private sealed record SlideEntry(XElement SlideId, string Part, XDocument Slide, List<Change> Changes, int Steps);

    /// <summary>Części pakietu zmieniane przy dodawaniu slajdów i następne wolne identyfikatory.</summary>
    private sealed class Package(ZipArchive zip)
    {
        public ZipArchive Zip { get; } = zip;
        public XDocument Presentation { get; } = Read(zip, "ppt/presentation.xml");
        public XDocument Relations { get; } = Read(zip, "ppt/_rels/presentation.xml.rels");
        public XDocument ContentTypes { get; } = Read(zip, "[Content_Types].xml");
        public long NextSlideId { get; set; }
        public int NextRelation { get; set; }
        public int NextPart { get; set; }
    }

    private static bool Expand(ZipArchive zip)
    {
        var package = new Package(zip);
        if (package.Presentation.Root!.Element(P + "sldIdLst") is not { } slideIds)
            return false;
        var slides = SlidesOf(package, slideIds);
        if (slides.All(s => s.Steps == 0))
            return false;
        package.NextSlideId = slideIds.Elements(P + "sldId").Select(e => (long)e.Attribute("id")!).DefaultIfEmpty(255).Max() + 1;
        package.NextRelation = package.Relations.Root!.Elements().Count() + 1;
        package.NextPart = zip.Entries.Select(e => e.FullName).Where(IsSlidePart)
            .Select(n => int.TryParse(n[SlidePartPrefix.Length..^4], out var i) ? i : 0).DefaultIfEmpty(0).Max() + 1;

        var firstNumber = (int?)package.Presentation.Root.Attribute("firstSlideNum") ?? 1;
        foreach (var (slide, index) in slides.Select((s, i) => (s, i)))
        {
            // kopie kroków przesuwają numerację — numer slajdu zostaje taki jak w oryginale
            FreezeSlideNumber(slide.Slide, firstNumber + index);
            for (var step = 0; step < slide.Steps; step++)
                AddStepSlide(package, slide, StateAt(slide.Slide, slide.Changes, step));
            // oryginał = stan po ostatnim kliknięciu
            Write(zip, slide.Part, StateAt(slide.Slide, slide.Changes, slide.Steps));
        }

        // sekcje (PowerPoint 2010+) wyliczają identyfikatory slajdów — nowych w nich nie ma; do eksportu niepotrzebne
        package.Presentation.Root.Descendants().Where(e => e.Name.LocalName == "sectionLst").ToList().ForEach(e => e.Parent?.Remove());
        Write(zip, "ppt/presentation.xml", package.Presentation);
        Write(zip, "ppt/_rels/presentation.xml.rels", package.Relations);
        Write(zip, "[Content_Types].xml", package.ContentTypes);
        return true;
    }

    private static List<SlideEntry> SlidesOf(Package package, XElement slideIds)
    {
        var relations = package.Relations.Root!.Elements(Rel + "Relationship").ToList();
        var slides = new List<SlideEntry>();
        foreach (var slideId in slideIds.Elements(P + "sldId"))
        {
            var relId = (string?)slideId.Attribute(R + "id");
            var target = relations.FirstOrDefault(r => (string?)r.Attribute("Id") == relId)?.Attribute("Target")?.Value
                ?? throw new InvalidDataException(relId);
            var part = "ppt/" + target.TrimStart('/').Replace("ppt/", "", StringComparison.Ordinal);
            var slide = Read(package.Zip, part);
            var changes = Changes(slide, out var steps);
            slides.Add(new SlideEntry(slideId, part, slide, changes, steps));
        }
        return slides;
    }

    /// <summary>Kopia slajdu w stanie <paramref name="state"/> wstawiona na liście przed oryginałem.</summary>
    private static void AddStepSlide(Package package, SlideEntry original, XDocument state)
    {
        var name = $"{SlidePartPrefix}{package.NextPart++}.xml";
        Write(package.Zip, name, state);
        if (package.Zip.GetEntry(RelsOf(original.Part)) is not null)
        {
            // notatki mają odwołanie tylko do jednego slajdu — kopia ich nie potrzebuje
            var rels = Read(package.Zip, RelsOf(original.Part));
            rels.Root!.Elements(Rel + "Relationship").Where(r => (string?)r.Attribute("Type") == NotesRelType).Remove();
            Write(package.Zip, RelsOf(name), rels);
        }
        package.ContentTypes.Root!.Add(new XElement(Ct + "Override",
            new XAttribute("PartName", "/" + name),
            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.presentationml.slide+xml")));
        var rel = $"rIdKrok{package.NextRelation++}";
        package.Relations.Root!.Add(new XElement(Rel + "Relationship",
            new XAttribute("Id", rel), new XAttribute("Type", SlideRelType), new XAttribute("Target", "slides/" + Path.GetFileName(name))));
        original.SlideId.AddBeforeSelf(new XElement(P + "sldId", new XAttribute("id", package.NextSlideId++), new XAttribute(R + "id", rel)));
    }

    /// <summary>Pola numeru slajdu zamienione na zwykły tekst z numerem <paramref name="number"/>.</summary>
    private static void FreezeSlideNumber(XDocument slide, int number)
    {
        foreach (var field in slide.Descendants(A + "fld").Where(f => (string?)f.Attribute("type") == "slidenum").ToList())
            field.ReplaceWith(new XElement(A + "r", field.Element(A + "rPr"), new XElement(A + "t", number)));
    }

    /// <summary>Zmiany widoczności z głównej sekwencji; <paramref name="steps"/> = liczba kliknięć.</summary>
    private static List<Change> Changes(XDocument slide, out int steps)
    {
        steps = 0;
        var changes = new List<Change>();
        var main = slide.Descendants(P + "cTn").FirstOrDefault(c => (string?)c.Attribute("nodeType") == "mainSeq");
        if (main?.Element(P + "childTnLst") is not { } groups)
            return changes;
        // kliknięcie jest krokiem dopiero, gdy coś pojawia się lub znika (samo wyróżnienie / ruch — widać w stanie końcowym)
        var clicked = false;
        foreach (var group in groups.Elements(P + "par"))
        {
            var effects = group.Descendants(P + "cTn").Where(c => c.Attribute("presetClass") is not null).ToList();
            // grupa zaczyna się kliknięciem albo sama (po poprzedniej / razem z nią) — wtedy należy do tego samego kroku
            if (effects.FirstOrDefault() is { } first && (string?)first.Attribute("nodeType") == "clickEffect")
                clicked = true;
            foreach (var effect in effects)
            {
                var kind = (string?)effect.Attribute("presetClass");
                if (kind is not ("entr" or "exit") || effect.Descendants(P + "spTgt").FirstOrDefault() is not { } shape)
                    continue;
                // element SmartArt / seria wykresu ani inna część kształtu poza akapitami — nie da się pokazać osobno
                if (IsPartOfGraphic(shape))
                    continue;
                var range = shape.Descendants(P + "pRg").FirstOrDefault();
                (int, int)? paragraphs = range is null ? null : ((int)range.Attribute("st")!, (int)range.Attribute("end")!);
                if (clicked)
                {
                    steps++;
                    clicked = false;
                }
                changes.Add(new Change(steps, (string)shape.Attribute("spid")!, paragraphs, kind == "entr"));
            }
        }
        return changes;
    }

    private static bool IsPartOfGraphic(XElement shape) =>
        shape.Elements().Any(e => e.Name != P + "txEl" || e.Element(P + "pRg") is null);

    /// <summary>Slajd po <paramref name="step"/> kliknięciach: ukryte kształty usunięte, ukryte akapity przezroczyste, bez animacji.</summary>
    private static XDocument StateAt(XDocument original, List<Change> changes, int step)
    {
        var slide = new XDocument(original);
        slide.Root!.Element(P + "timing")?.Remove();
        foreach (var target in changes.GroupBy(c => (c.ShapeId, c.Paragraphs)))
        {
            // przed pierwszą zmianą widoczny jest, gdy pierwsza zmiana to wyjście; potem — wg ostatniej wykonanej
            var visible = !target.First().Show;
            foreach (var change in target.Where(c => c.Step <= step))
                visible = change.Show;
            if (visible)
                continue;
            if (Shape(slide, target.Key.ShapeId) is not { } shape)
                continue;
            if (target.Key.Paragraphs is { } range)
                HideParagraphs(shape, range.First, range.Last);
            else
                shape.Remove();
        }
        return slide;
    }

    private static XElement? Shape(XDocument slide, string id) =>
        slide.Descendants(P + "cNvPr").FirstOrDefault(c => (string?)c.Attribute("id") == id)?.Parent?.Parent;

    /// <summary>Tekst i punktor niewidoczne, ale zajmują miejsce — reszta tekstu nie przeskakuje.</summary>
    private static void HideParagraphs(XElement shape, int first, int last)
    {
        var paragraphs = shape.Element(P + "txBody")?.Elements(A + "p").ToList() ?? [];
        for (var i = first; i <= last && i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            foreach (var run in paragraph.Elements().Where(e => e.Name == A + "r" || e.Name == A + "fld" || e.Name == A + "br"))
            {
                var properties = run.Element(A + "rPr");
                if (properties is null)
                {
                    properties = new XElement(A + "rPr");
                    run.AddFirst(properties);
                }
                NoFill(properties);
            }
            var paragraphProperties = paragraph.Element(A + "pPr");
            if (paragraphProperties is null)
            {
                paragraphProperties = new XElement(A + "pPr");
                paragraph.AddFirst(paragraphProperties);
            }
            NoBullet(paragraphProperties);
        }
    }

    private static readonly string[] Fills = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    /// <summary>Obrys, cień / poświata, podświetlenie i własne podkreślenie też byłyby widoczne — bez nich.</summary>
    private static readonly string[] Visible = ["ln", "effectLst", "effectDag", "highlight", "uLnTx", "uLn", "uFillTx", "uFill"];

    private static void NoFill(XElement runProperties)
    {
        runProperties.Elements().Where(e => Fills.Contains(e.Name.LocalName) || Visible.Contains(e.Name.LocalName)).Remove();
        // kolejność wg schematu: ln, wypełnienie, … — wypełnienie jest teraz pierwsze
        runProperties.AddFirst(new XElement(A + "noFill"));
    }

    private static readonly string[] Bullets = ["buNone", "buAutoNum", "buChar", "buBlip"];

    private static void NoBullet(XElement paragraphProperties)
    {
        paragraphProperties.Elements().Where(e => Bullets.Contains(e.Name.LocalName)).Remove();
        // kolejność wg schematu: …, punktor, tabLst, defRPr, extLst
        var after = paragraphProperties.Elements().FirstOrDefault(e => e.Name.LocalName is "tabLst" or "defRPr" or "extLst");
        if (after is not null)
            after.AddBeforeSelf(new XElement(A + "buNone"));
        else
            paragraphProperties.Add(new XElement(A + "buNone"));
    }

    private static string RelsOf(string part) => $"{Path.GetDirectoryName(part)!.Replace('\\', '/')}/_rels/{Path.GetFileName(part)}.rels";

    private static XDocument Read(ZipArchive zip, string name)
    {
        using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException(name)).Open();
        // spacja jako osobny fragment tekstu (<a:t> </a:t>) to sam biały znak — bez tego znika i słowa się sklejają
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void Write(ZipArchive zip, string name, XDocument document)
    {
        zip.GetEntry(name)?.Delete();
        using var stream = zip.CreateEntry(name).Open();
        document.Save(stream, SaveOptions.DisableFormatting);
    }
}

/// <summary>Eksport z krokami animacji jako osobnymi slajdami (<see cref="AnimationSteps"/>) — przed właściwym eksporterem.</summary>
public sealed class AnimationStepsExporter(ISlideExporter inner) : ISlideExporter, ISlideExportVersion
{
    /// <summary>
    /// Znacznik eksportu z krokami animacji (pusty = eksport sprzed nich; „kroki” = z nimi, ale bez spacji stojących osobno
    /// — sklejone słowa, eksport od nowa).
    /// </summary>
    public string Current => "kroki-2";

    /// <summary>Eksport sprzed kroków animacji jest nieaktualny tylko dla prezentacji z animacjami.</summary>
    public bool IsOutdated(string presentation, string exported) =>
        exported != Current && AnimationSteps.HasSteps(presentation);

    public string Name => inner.Name;

    public bool IsAvailable => inner.IsAvailable;

    public async Task ExportAsync(string presentation, string outDir, int width, IProgress<string> progress)
    {
        // folder tymczasowy poza outDir (tam mają być tylko slajdy); ta sama nazwa pliku co oryginał
        var temp = Path.Combine(Path.GetTempPath(), "Uwielbienia-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        try
        {
            var steps = Path.Combine(temp, Path.GetFileName(presentation));
            if (!await Task.Run(() => AnimationSteps.TryExpand(presentation, steps)).ConfigureAwait(false))
            {
                await inner.ExportAsync(presentation, outDir, width, progress).ConfigureAwait(false);
                return;
            }
            try
            {
                await inner.ExportAsync(steps, outDir, width, progress).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // przerobiona kopia nie do otwarcia — zwykłe slajdy z oryginału
                ClearDirectory(outDir);
                await inner.ExportAsync(presentation, outDir, width, progress).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // program eksportu jeszcze trzyma plik — zostaje w katalogu tymczasowym
            }
        }
    }

    private static void ClearDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (var file in Directory.GetFiles(directory))
            File.Delete(file);
        foreach (var child in Directory.GetDirectories(directory))
            Directory.Delete(child, recursive: true);
    }
}
