using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Launcher;

// Launcher / game updates from the project's GitHub releases (see Updater.cs).
public partial class MainWindow
{
    enum UpdateStatus { Idle, Checking, Current, Available, Failed }
    UpdateStatus updateStatus = UpdateStatus.Idle;
    ReleaseInfo? release;
    string updateError = "";
    bool closeAfterJob;

    static readonly string[] EngineProcesses = { "wwe2k17", "wwe2k17_native", "wwe2k17_d3d12", "wwe2k17_d3d11" };

    // Called once at startup.
    void StartUpdateCheck()
    {
        Updater.CleanOld(root);
        string err = Path.Combine(root, "userdata", "update_error.txt");
        if (File.Exists(err))
        {
            try { StatusText.Text = Tr("La dernière mise à jour a échoué et a été annulée : {0}", File.ReadAllText(err).Trim()); File.Delete(err); } catch { }
        }
        if (cfg.CheckUpdates && Environment.GetEnvironmentVariable("WWE2K17_LAUNCHER_DRYRUN") == null) _ = CheckUpdateAsync();
    }

    async Task CheckUpdateAsync()
    {
        if (updateStatus == UpdateStatus.Checking || busy) return;
        updateStatus = UpdateStatus.Checking;
        if (screen == "main") Rebuild();
        try
        {
            release = await Updater.CheckAsync();
            updateStatus = release == null ? UpdateStatus.Current : UpdateStatus.Available;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidDataException or KeyNotFoundException)
        {
            updateStatus = UpdateStatus.Failed;
            updateError = ex.Message;
        }
        if (screen == "main") Rebuild();
    }

    string UpdateValue() => updateStatus switch
    {
        UpdateStatus.Checking => "VÉRIFICATION…",
        UpdateStatus.Current => "À JOUR",
        UpdateStatus.Available => Tr("{0} DISPONIBLE", release!.Version),
        UpdateStatus.Failed => "ÉCHEC",
        _ => "VÉRIFIER",
    };

    string UpdateInfo() => updateStatus switch
    {
        UpdateStatus.Available => Tr("Version {0} disponible ({1} Mo). Entrée pour la télécharger et l'installer : le jeu, les sauvegardes et tes réglages sont gardés. Le launcher redémarre.",
            release!.Version, release.Size >> 20),
        UpdateStatus.Current => Tr("Tu as la dernière version ({0}).", Updater.Current.ToString(3)),
        UpdateStatus.Failed => Tr("Impossible de vérifier les mises à jour : {0}", updateError),
        UpdateStatus.Checking => Tr("Vérification de la dernière version sur GitHub…"),
        _ => Tr("Cherche une nouvelle version sur GitHub (version actuelle : {0}).", Updater.Current.ToString(3)),
    };

    void UpdateAction()
    {
        if (updateStatus != UpdateStatus.Available || release == null) { _ = CheckUpdateAsync(); return; }
        if (proc != null || EngineProcesses.Any(n => Process.GetProcessesByName(n).Length > 0))
        {
            StatusText.Text = Tr("Ferme le jeu avant de mettre à jour.");
            return;
        }
        var r = release;
        closeAfterJob = true;
        RunJob(Tr("Téléchargement de la mise à jour {0}", r.Version), (p, ct) =>
        {
            Updater.DownloadAndStage(root, r, p, ct);
            return Tr("Mise à jour prête : le launcher redémarre.");
        });
    }
}
