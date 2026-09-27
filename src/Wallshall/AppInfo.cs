using System.Reflection;

static class AppInfo
{
    public const string Name = "Wallshall";

    public static string Version { get; } = ReadVersion();
    public static string Build { get; } = ReadBuild();

    public static string Display => Build == "" ? $"{Name} {Version}" : $"{Name} {Version} · сборка {Build}";

    static string Informational() =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";

    static string ReadVersion()
    {
        var text = Informational();
        if (text != "") return text.Split('+')[0];
        return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    static string ReadBuild()
    {
        var parts = Informational().Split('+');
        if (parts.Length < 2 || parts[1] == "") return "";

        var meta = parts[1];
        if (meta.StartsWith("build.", StringComparison.OrdinalIgnoreCase)) return meta["build.".Length..];
        return meta.Length > 7 ? meta[..7] : meta;
    }
}
