using System.Diagnostics;
using System.Text.Json;

class State
{
    const int MaxHistory = 5000;

    static readonly string StatePath = Path.Combine(AppSettings.AppDir, "state.json");

    public int Page { get; set; } = 1;
    public string Date { get; set; } = "";
    public List<string> Used { get; set; } = new();

    public HashSet<string> UsedSet() => Used.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void MarkUsed(string name)
    {
        Used.Remove(name);
        Used.Add(name);
        if (Used.Count > MaxHistory) Used.RemoveRange(0, Used.Count - MaxHistory);
    }

    public static State Load()
    {
        try
        {
            if (File.Exists(StatePath))
                return JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State();
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
        return new State();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.AppDir);
            var tmp = StatePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this));
            File.Move(tmp, StatePath, overwrite: true);
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }
}
