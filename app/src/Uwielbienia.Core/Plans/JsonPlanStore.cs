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

    public JsonPlanStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public IReadOnlyList<Plan> LoadAll() =>
        Directory.EnumerateFiles(_directory, "*.json")
            .Select(TryRead)
            .OfType<Plan>()
            .ToList();

    public void Save(Plan plan)
    {
        var path = PathFor(plan.Id);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(plan, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(Guid planId) => File.Delete(PathFor(planId));

    public static Plan? Import(string path) => TryRead(path);

    public static void Export(Plan plan, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(plan, JsonOptions));

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id}.json");

    private static Plan? TryRead(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Plan>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
