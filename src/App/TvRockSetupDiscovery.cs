using Microsoft.Win32;

namespace TsScheduler;

internal sealed record TvRockSetupDetection(string Folder, string? Url, string Message);

internal static class TvRockSetupDiscovery
{
    internal static TvRockSetupDetection? Detect(string requestedFolder)
    {
        string? runningFolder = FindRunningTvRockFolder();
        string folder = string.IsNullOrWhiteSpace(requestedFolder) ? runningFolder ?? "" : requestedFolder.Trim();
        if (folder.Length == 0 || folder.StartsWith(@"\\", StringComparison.Ordinal)) return null;

        string fullFolder;
        try { fullFolder = Path.GetFullPath(folder); }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return null; }

        if (runningFolder is not null && !Path.GetFullPath(runningFolder).Equals(fullFolder, StringComparison.OrdinalIgnoreCase))
            return new(fullFolder, null, "指定フォルダーと起動中のTvRockが異なります。取り違え防止のため自動設定しません。");

        if (!new[] { "dtv.ini", "tvrock.sch", "tvrock.key" }.All(name => File.Exists(Path.Combine(fullFolder, name))))
            return null;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\TvRock", writable: false);
            if (key is null) return new(fullFolder, null, "TvRockフォルダーは確認できましたが、Web設定を読み取れません。");

            int port = Convert.ToInt32(key.GetValue("HTTPPORT", 8969), System.Globalization.CultureInfo.InvariantCulture);
            string account = Convert.ToString(key.GetValue("WEBACC", ""), System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? "";
            bool serverEnabled = Convert.ToInt32(key.GetValue("WEBSRV", 0), System.Globalization.CultureInfo.InvariantCulture) != 0;
            if (port is < 1 or > 65535 || account.Length == 0)
                return new(fullFolder, null, "TvRockのWebアカウントまたはポートを読み取れません。URLを確認してください。");

            string escapedAccount = Uri.EscapeDataString(account);
            string url = $"http://127.0.0.1:{port}/{escapedAccount}/";
            int authMode = Convert.ToInt32(key.GetValue("AUTHMODE", 0), System.Globalization.CultureInfo.InvariantCulture);
            string message = serverEnabled
                ? authMode == 0
                    ? $"TvRock設定から検出しました（ポート {port}、アカウント {account}）。URLを確認して保存してください。"
                    : $"TvRock設定から検出しました（ポート {port}、アカウント {account}）。認証が有効です。URLと認証設定を確認してください。"
                : $"TvRock設定からURLを組み立てました（ポート {port}、アカウント {account}）。TvRock番組表サーバーはOFFです。先にTvRock側でONにしてください。";
            return new(fullFolder, url, message);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or FormatException or OverflowException)
        {
            return new(fullFolder, null, "TvRockのWeb設定を読み取れませんでした。URLを確認してください。");
        }
    }

    static string? FindRunningTvRockFolder()
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("tvrock"))
        using (process)
        {
            try
            {
                string? path = process.MainModule?.FileName;
                string? folder = path is null ? null : Path.GetDirectoryName(path);
                if (folder is not null && new[] { "dtv.ini", "tvrock.sch", "tvrock.key" }.All(name => File.Exists(Path.Combine(folder, name))))
                    return Path.GetFullPath(folder);
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException) { }
        }
        return null;
    }
}
