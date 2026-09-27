using System.Diagnostics;

static class Favorites
{
    public static IEnumerable<FileInfo> Images(string dir) => Cache.AllImages(dir);

    public static int Count(string dir) => Images(dir).Count();

    public static bool Contains(string dir, string file) =>
        File.Exists(Path.Combine(dir, Path.GetFileName(file)));

    public static bool Add(string dir, string file)
    {
        try
        {
            if (!File.Exists(file)) return false;
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, Path.GetFileName(file));
            if (!Cache.SamePath(file, dest) && !File.Exists(dest)) File.Copy(file, dest);
            return true;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return false; }
    }

    public static bool Remove(string dir, string file)
    {
        var dest = Path.Combine(dir, Path.GetFileName(file));
        if (!File.Exists(dest)) return false;
        try { File.Delete(dest); return true; }
        catch (Exception ex) { Debug.WriteLine(ex); return false; }
    }

    public static List<string> Pick(string dir, List<string> shown, int count)
    {
        var all = Images(dir).Select(f => f.FullName).ToList();
        if (all.Count == 0) return new List<string>();

        var seen = shown.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = all.Where(f => !seen.Contains(Path.GetFileName(f)))
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();

        if (picked.Count < count)
        {
            shown.Clear();
            var rest = all.Where(f => !picked.Contains(f, StringComparer.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next());
            foreach (var file in rest)
            {
                if (picked.Count >= count) break;
                picked.Add(file);
            }
        }
        return picked;
    }
}
