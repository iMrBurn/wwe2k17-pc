using System.IO;

namespace Launcher;

// Prepares the player's own game files, like tools\setup.ps1 does from a terminal:
//   game\                    the whole disc, plus Title Update 2's default.xexp beside default.xex
//   userdata\title_update\   the whole Title Update 2 (the game reads its pac files through update:\)
// Nothing here downloads anything or ships game data.
public sealed class GameSetup
{
    public const uint TitleId = 0x545408BA;
    public const uint TitleUpdateContent = 0x000B0000;

    readonly string _root;
    public GameSetup(string root) => _root = root;

    public string GameFolder => Path.Combine(_root, "game");
    public string UpdateFolder => Path.Combine(_root, "userdata", "title_update");

    // The engine is recompiled from the game with Title Update 1.04 (image 0.0.4.1): its default.xexp. Other
    // updates (TU2 = 1.02) patch different code, so the engine would run the wrong image.
    const string ExpectedXexpSha256 = "A144CBE9A842841B86AD0E99F3C0A88F9F30561231CF6A6E525040CAA513840D";

    public enum UpdateState { Missing, Wrong, Ok }

    public bool HasGame => File.Exists(Path.Combine(GameFolder, "default.xex"));
    public bool HasTitleUpdate =>
        File.Exists(Path.Combine(UpdateFolder, "default.xexp")) && File.Exists(Path.Combine(GameFolder, "default.xexp"));

    public UpdateState CheckUpdate()
    {
        string patch = Path.Combine(UpdateFolder, "default.xexp");
        string beside = Path.Combine(GameFolder, "default.xexp");
        if (!File.Exists(patch) || !File.Exists(beside)) return UpdateState.Missing;
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var a = File.OpenRead(patch);
            using var b = File.OpenRead(beside);
            string ha = Convert.ToHexString(sha.ComputeHash(a)), hb = Convert.ToHexString(sha.ComputeHash(b));
            return ha == ExpectedXexpSha256 && hb == ExpectedXexpSha256 ? UpdateState.Ok : UpdateState.Wrong;
        }
        catch (IOException) { return UpdateState.Wrong; }
    }

    // Extracts an Xbox 360 disc image into game\ (about 8.7 GB, a few minutes).
    public void InstallIso(string iso, Action<long, long> progress, CancellationToken cancel)
    {
        using var reader = XdvdfsReader.Open(iso)
            ?? throw new InvalidDataException("Ce fichier n'est pas une image de disque Xbox 360.");
        if (!reader.Exists("default.xex"))
            throw new InvalidDataException("Cette image ne contient pas default.xex : ce n'est pas le disque du jeu.");
        reader.ExtractAll(GameFolder, progress, cancel);
        ApplyUpdateExecutable();
    }

    // Copies an already extracted disc folder (the one that holds default.xex) into game\.
    public void InstallFolder(string folder, Action<long, long> progress, CancellationToken cancel)
    {
        if (!File.Exists(Path.Combine(folder, "default.xex")))
            throw new InvalidDataException("Ce dossier ne contient pas default.xex.");
        if (Path.GetFullPath(folder).TrimEnd('\\').Equals(Path.GetFullPath(GameFolder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("C'est déjà le dossier du jeu.");
        var files = new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
        long total = files.Sum(f => f.Length), done = 0;
        string src = Path.GetFullPath(folder);
        var buffer = new byte[1 << 20];
        foreach (var f in files)
        {
            cancel.ThrowIfCancellationRequested();
            string target = Path.Combine(GameFolder, Path.GetRelativePath(src, f.FullName));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = f.OpenRead();
            using var output = File.Create(target);
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
                output.Write(buffer, 0, read);
                done += read;
                progress(done, total);
            }
        }
        ApplyUpdateExecutable();
    }

    // Unpacks the Title Update package (tu00000001_00000000, version 1.04). Returns its display name.
    public string InstallTitleUpdate(string package, Action<long, long> progress)
    {
        using var pkg = StfsPackage.Open(package);
        if (pkg.TitleId != TitleId)
            throw new InvalidDataException("Cette mise à jour n'est pas pour WWE 2K17.");
        if (pkg.ContentType != TitleUpdateContent)
            throw new InvalidDataException("Ce fichier n'est pas un Title Update.");
        if (Directory.Exists(UpdateFolder)) Directory.Delete(UpdateFolder, true);
        pkg.ExtractTo(UpdateFolder, progress);
        ApplyUpdateExecutable();
        return pkg.DisplayName;
    }

    // The loader only applies the update when default.xexp sits next to default.xex.
    void ApplyUpdateExecutable()
    {
        string patch = Path.Combine(UpdateFolder, "default.xexp");
        if (File.Exists(patch) && Directory.Exists(GameFolder))
            File.Copy(patch, Path.Combine(GameFolder, "default.xexp"), true);
    }
}
