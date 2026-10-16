using System.Text.Json;

namespace Zoo;

internal sealed class RuntimeConfiguration
{
    public string TvRockFolder { get; set; } = "";
    public string TvRockUrl { get; set; } = "http://127.0.0.1:8969/user/";
    internal static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVRockZoo");
    internal static string ConfigPath => Path.Combine(Home, "connection.json");
    private static RuntimeConfiguration? _current;
    internal static RuntimeConfiguration Current => _current ??= File.Exists(ConfigPath)
        ? JsonSerializer.Deserialize<RuntimeConfiguration>(File.ReadAllText(ConfigPath)) ?? new() : new();
    internal static Uri BaseUri => new(Current.TvRockUrl);
    internal static string NormalizeUrl(string text)
    {
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)) return text.Trim();
        var builder = new UriBuilder(uri) { Query = "", Fragment = "" };
        var leaf = builder.Path.Split('/').Last();
        if (new[] { "list", "day", "now", "kwl", "kws", "reg", "new" }.Contains(leaf))
            builder.Path = builder.Path[..(builder.Path.LastIndexOf('/') + 1)];
        else if (!builder.Path.EndsWith('/')) builder.Path += "/";
        return builder.Uri.AbsoluteUri;
    }
    internal static bool IsAllowed(Uri uri) => uri.Scheme == "http" && uri.IsLoopback &&
        uri.Authority == BaseUri.Authority && uri.AbsolutePath.StartsWith(BaseUri.AbsolutePath, StringComparison.Ordinal);
    internal string? Validate()
    {
        if (!new[] { "dtv.ini", "tvrock.sch", "tvrock.key" }.All(f => File.Exists(Path.Combine(TvRockFolder, f))))
            return "dtv.ini・tvrock.sch・tvrock.key があるTVRockのフォルダーを選んでください。";
        if (!Uri.TryCreate(TvRockUrl, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.EndsWith('/'))
            return "このPCのTVRockのURLを末尾 / 付きで入力してください。例: http://127.0.0.1:8969/user/";
        return null;
    }
    internal void Save()
    {
        if (Validate() is string error) throw new InvalidDataException(error);
        Directory.CreateDirectory(Home);
        if (!File.Exists(ConfigPath)) File.WriteAllText(Path.Combine(Home, "recommend-disabled.txt"), "off");
        string temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, ConfigPath, true);
        // The active session keeps its original connection until restart.
    }
}
