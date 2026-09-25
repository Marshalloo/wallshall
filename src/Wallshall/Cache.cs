using System.Diagnostics;

static class Cache
{
    const string Prefix = "wallhaven-";

    static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

    public static IEnumerable<FileInfo> Images(string dir) =>
        Directory.Exists(dir)
            ? new DirectoryInfo(dir).EnumerateFiles(Prefix + "*")
                .Where(f => Extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            : Enumerable.Empty<FileInfo>();

    public static List<string> PickUnused(string dir, HashSet<string> used, int count) =>
        Images(dir).Where(f => !used.Contains(f.Name))
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(f => f.FullName)
            .ToList();

    public static List<string> FillUp(string dir, List<string> chosen, int count)
    {
        var result = new List<string>(chosen);
        var rest = Images(dir).Select(f => f.FullName)
            .Where(f => !result.Contains(f, StringComparer.OrdinalIgnoreCase))
            .OrderBy(_ => Random.Shared.Next());

        foreach (var file in rest)
        {
            if (result.Count >= count) break;
            result.Add(file);
        }
        return result;
    }

    public static void Cleanup(string dir, List<string> used, int keep)
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < used.Count; i++) order[used[i]] = i;

        var stale = Images(dir)
            .Where(f => order.ContainsKey(f.Name))
            .OrderByDescending(f => order[f.Name])
            .Skip(keep);
        foreach (var file in stale) Delete(file.FullName);

        if (Directory.Exists(dir))
            foreach (var part in Directory.EnumerateFiles(dir, Prefix + "*.part")) Delete(part);
    }

    public static void DeleteAll(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, Prefix + "*"))
            if (Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) ||
                file.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                Delete(file);
    }

    public static void Move(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Images(from).ToList())
        {
            try
            {
                var dest = Path.Combine(to, file.Name);
                if (File.Exists(dest)) file.Delete();
                else file.MoveTo(dest);
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'),
                      StringComparison.OrdinalIgnoreCase);

    public static void Delete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
