using System.IO;
using System.Text.Json;

namespace SprintDock.Windows;

public static class LocalStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SprintDock");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "state.json");
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static SprintState? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var state = JsonSerializer.Deserialize<SprintState>(File.ReadAllText(FilePath), Options);
            if (state is null || string.IsNullOrWhiteSpace(state.Goal)) return null;
            return state;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(SprintState state)
    {
        try
        {
            state.UpdatedAtUtc = DateTime.UtcNow;
            Directory.CreateDirectory(DirectoryPath);
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, Options));
            File.Move(tempPath, FilePath, true);
        }
        catch
        {
            // A failed autosave should never interrupt a focus session.
        }
    }
}
