using System.Text.Json;
using System.Text.Json.Serialization;

namespace Uwielbienia.Core.Plans;

public interface IPlanStore
{
    IReadOnlyList<Plan> LoadAll();

    void Save(Plan plan);

    void Delete(Guid planId);
}

/// <summary>Każdy plan w osobnym pliku <c>{id}.json</c> — łatwo skopiować lub przesłać komuś.</summary>
public sealed class JsonPlanStore : IPlanStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _directory;

    /// <summary>Ostatnio przeczytana lub zapisana wersja każdego planu — gdy plik jest chwilowo zajęty, plan nie znika z listy.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Plan> _lastKnown = new(StringComparer.OrdinalIgnoreCase);

    public JsonPlanStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public IReadOnlyList<Plan> LoadAll() =>
        Directory.EnumerateFiles(_directory, "*.json")
            .Select(ReadOrLastKnown)
            .OfType<Plan>()
            .ToList();

    public void Save(Plan plan)
    {
        var path = PathFor(plan.Id);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(plan, JsonOptions));
        File.Move(temp, path, overwrite: true);
        _lastKnown[path] = plan;
    }

    public void Delete(Guid planId)
    {
        File.Delete(PathFor(planId));
        _lastKnown.TryRemove(PathFor(planId), out _);
    }

    public static Plan? Import(string path) => TryRead(path);

    public static void Export(Plan plan, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(plan, JsonOptions));

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id}.json");

    private Plan? ReadOrLastKnown(string path)
    {
        try
        {
            var plan = Read(path);
            if (plan is not null)
                _lastKnown[path] = plan;
            return plan;
        }
        catch (Exception ex) when (FileErrors.IsFileAccess(ex))
        {
            // plik chwilowo zajęty (antywirus, synchronizacja) — ostatnia znana wersja
            return _lastKnown.GetValueOrDefault(path);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static Plan? Read(string path) => JsonSerializer.Deserialize<Plan>(File.ReadAllText(path), JsonOptions);

    private static Plan? TryRead(string path)
    {
        try
        {
            return Read(path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
