using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Launcher;

public sealed record ReleaseInfo(string Version, string Notes, Uri Asset, long Size, string? Sha256);

// Updates the launcher and the four engines from the newest GitHub release of the project, like the launchers of the
// other games: look at the latest release, download its zip, check it, swap the files in, restart.
// Never touched: game\, userdata\, logs\ (the zip does not contain them and nothing from them is replaced).
// Only the project's own release page is asked, with no information about the player.
public static class Updater
{
    const string Api = "https://api.github.com/repos/iMrBurn/wwe2k17-pc/releases/latest";
    const string ZipRoot = "WWE2K17-PC/";
    const string LauncherExe = "WWE2K17 Launcher.exe";
    static readonly HttpClient Http = CreateClient();

    public static Version Current =>
        typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0)) : new Version(0, 0, 0);

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WWE2K17Launcher", "1"));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    // Test hook: WWE2K17_LAUNCHER_UPDATE_API=<url> asks another address (a local test server) instead of GitHub.
    static string ApiUrl => Environment.GetEnvironmentVariable("WWE2K17_LAUNCHER_UPDATE_API") is { Length: > 0 } u ? u : Api;

    // null = up to date (or no usable release).
    public static async Task<ReleaseInfo?> CheckAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await Http.GetAsync(ApiUrl, cts.Token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
        var root = json.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        string version = tag.TrimStart('v', 'V');
        if (!Version.TryParse(version, out var latest)) throw new InvalidDataException("The release tag is not a version number: " + tag);
        if (latest <= Current) return null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            return new ReleaseInfo(version,
                root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                new Uri(asset.GetProperty("browser_download_url").GetString()!),
                asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                asset.TryGetProperty("digest", out var digest) ? digest.GetString()?.Replace("sha256:", "", StringComparison.OrdinalIgnoreCase) : null);
        }
        throw new InvalidDataException("The latest release has no zip file.");
    }

    // Downloads and checks the zip, unpacks it to update_staging\payload, then starts a script that waits for the
    // launcher to close, swaps the files (undoing everything if one fails) and starts the new launcher.
    // The caller closes the launcher afterwards.
    public static void DownloadAndStage(string root, ReleaseInfo release, Action<long, long> progress, CancellationToken cancel)
    {
        string staging = Path.Combine(root, "update_staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        string zip = Path.Combine(staging, "release.zip");
        try
        {
            using (var response = Http.GetAsync(release.Asset, HttpCompletionOption.ResponseHeadersRead, cancel).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? release.Size;
                using var input = response.Content.ReadAsStream(cancel);
                using var output = File.Create(zip);
                var buffer = new byte[1 << 20];
                long done = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    done += read;
                    progress(done, total);
                }
            }
            if (!string.IsNullOrWhiteSpace(release.Sha256))
            {
                using var s = File.OpenRead(zip);
                string actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s));
                if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded file is damaged (checksum does not match).");
            }

            string payload = Path.Combine(staging, "payload");
            using (var archive = ZipFile.OpenRead(zip))
            {
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.EndsWith('/')) continue;
                    if (!name.StartsWith(ZipRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The zip does not look like a release of this game.");
                    string rel = name[ZipRoot.Length..];
                    if (rel.StartsWith('/') || rel.Split('/').Any(p => p == ".." || p.Contains(':')))
                        throw new InvalidDataException("The zip contains an unsafe path.");
                    // never replace the player's data, whatever the zip says
                    string top = rel.Split('/')[0].ToLowerInvariant();
                    if (top is "game" or "userdata" or "logs" or "update_staging") continue;
                    string dest = Path.GetFullPath(Path.Combine(payload, rel.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dest.StartsWith(Path.GetFullPath(payload), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The zip contains an unsafe path.");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, true);
                }
            }
            File.Delete(zip);
            if (!File.Exists(Path.Combine(payload, LauncherExe)) || !File.Exists(Path.Combine(payload, "wwe2k17_native.exe")))
                throw new InvalidDataException("The zip is missing the launcher or the game files.");

            // all-or-nothing swap: every file is first moved next to its target as name.new, then each old file is
            // renamed name.old and the new one takes its place; on any failure the renames are undone.
            string script = Path.Combine(staging, "apply-update.ps1");
            File.WriteAllText(script, Script);
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                         Environment.ProcessId.ToString(), payload, root.TrimEnd('\\'), staging })
                psi.ArgumentList.Add(a);
            Process.Start(psi);
        }
        catch
        {
            try { Directory.Delete(staging, true); } catch { }
            throw;
        }
    }

    const string Script = @"param($PidToWait,$Source,$Target,$Staging)
try { Wait-Process -Id ([int]$PidToWait) -Timeout 120 -ErrorAction Stop } catch {}
$ErrorActionPreference = 'Stop'
$list = @()
$started = @{}
$failed = $null
try {
  Get-ChildItem -LiteralPath $Source -File -Recurse | ForEach-Object {
    $rel = $_.FullName.Substring($Source.Length).TrimStart('\')
    $dst = Join-Path $Target $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    Move-Item -LiteralPath $_.FullName -Destination ($dst + '.new') -Force
    $list += $dst
  }
  foreach ($dst in $list) {
    $hadOld = Test-Path -LiteralPath $dst
    if ($hadOld) { Move-Item -LiteralPath $dst -Destination ($dst + '.old') -Force }
    $started[$dst] = $hadOld
    Move-Item -LiteralPath ($dst + '.new') -Destination $dst -Force
  }
} catch {
  $failed = $_.Exception.Message
  foreach ($dst in $started.Keys) {
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Force -ErrorAction SilentlyContinue }
    if ($started[$dst]) { Move-Item -LiteralPath ($dst + '.old') -Destination $dst -Force -ErrorAction SilentlyContinue }
  }
  foreach ($dst in $list) { Remove-Item -LiteralPath ($dst + '.new') -Force -ErrorAction SilentlyContinue }
}
if ($failed) {
  New-Item -ItemType Directory -Force -Path (Join-Path $Target 'userdata') | Out-Null
  Set-Content -LiteralPath (Join-Path $Target 'userdata\update_error.txt') -Value $failed -Encoding UTF8
} else {
  foreach ($dst in $list) { Remove-Item -LiteralPath ($dst + '.old') -Force -ErrorAction SilentlyContinue }
}
Remove-Item -LiteralPath $Staging -Recurse -Force -ErrorAction SilentlyContinue
Start-Process -FilePath (Join-Path $Target 'WWE2K17 Launcher.exe') -WorkingDirectory $Target
";

    // Leftovers of an update that could not delete a file in use.
    public static void CleanOld(string root)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, "*.old", SearchOption.TopDirectoryOnly)) File.Delete(f);
            string plume = Path.Combine(root, "plume");
            if (Directory.Exists(plume))
                foreach (var f in Directory.EnumerateFiles(plume, "*.old", SearchOption.AllDirectories)) File.Delete(f);
        }
        catch { }
    }
}
