using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.IO.Path;

namespace Launcher;

// Steam Deck / small PC profile: detected when the GPU names are known, offered once, cancellable in SETTINGS.
public partial class MainWindow
{
    int physicalCores;

    string BackupPath => Path.Combine(root, "userdata", "profile_backup.json");
    string OfferDonePath => Path.Combine(root, "userdata", "profile_offer_done.txt");

    // The card the game will really use: the chosen one, else the first that isn't integrated (the native renderer
    // picks the most powerful), else the first one.
    string ChosenGpu()
    {
        if (cfg.Gpu.Length > 0) return cfg.Gpu;
        return gpuNames.FirstOrDefault(g => !MachineProfile.IsIntegrated(g)) ?? gpuNames.FirstOrDefault() ?? "";
    }

    MachineInfo DetectMachine()
    {
        string gpu = ChosenGpu();
        double ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0;
        long vram = gpu.Length > 0 ? MachineProfile.VideoMemoryMb(gpu) : -1;
        int cores = physicalCores;
        // Test hook: WWE2K17_LAUNCHER_PROFILE_FAKE="gpu name|video memory MB|physical cores|RAM GB"
        var fake = Environment.GetEnvironmentVariable("WWE2K17_LAUNCHER_PROFILE_FAKE")?.Split('|');
        if (fake is { Length: 4 })
        {
            gpu = fake[0];
            long.TryParse(fake[1], out vram);
            int.TryParse(fake[2], out cores);
            double.TryParse(fake[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ram);
        }
        bool deck = Environment.GetEnvironmentVariable("SteamDeck") == "1" || gpuNames.Append(gpu).Any(MachineProfile.IsDeckName);
        return new MachineInfo(deck, MachineProfile.IsWine(), gpu, MachineProfile.IsIntegrated(gpu), vram, cores, ram);
    }

    // Called once the GPU names are known.
    void OfferProfile()
    {
        if (busy || File.Exists(OfferDonePath) || File.Exists(BackupPath)) return;
        var machine = DetectMachine();
        var profile = MachineProfile.Choose(machine);

        var log = Environment.GetEnvironmentVariable("WWE2K17_LAUNCHER_PROFILE_LOG");
        if (!string.IsNullOrEmpty(log))
        {
            File.WriteAllText(log, $"{machine}\n{(profile == null ? "no proposal" : profile.ToString())}\n");
            return;
        }
        if (profile == null) return;

        int answer = ShowOffer(profile);
        if (answer == 0) ApplyProfile(profile);
        if (answer != 1) { try { Directory.CreateDirectory(Path.GetDirectoryName(OfferDonePath)!); File.WriteAllText(OfferDonePath, "1"); } catch { } }
    }

    // The engine the profile uses: native rendering on Vulkan (the first native engine that is built).
    Engine? ProfileEngine() => AvailableEngines.FirstOrDefault(e => e.Id == "native") ?? AvailableEngines.FirstOrDefault(e => e.Native);

    void ApplyProfile(Profile p)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath)!);
            if (!File.Exists(BackupPath)) File.WriteAllText(BackupPath, JsonSerializer.Serialize(cfg));
        }
        catch { }
        var engine = ProfileEngine();
        if (engine != null) cfg.Engine = engine.Id;
        // never raises a setting that is already lower
        if (cfg.Internal == 0 || cfg.Internal > p.Internal) cfg.Internal = p.Internal;
        if (p.Fullscreen) cfg.Fullscreen = true;
        SaveSettings();
        StatusText.Text = Tr("Préréglage appliqué. Tu peux l'annuler dans PARAMÈTRES.");
        if (screen == "settings") Rebuild();
    }

    void RestorePreset()
    {
        try
        {
            var back = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(BackupPath));
            if (back != null)
            {
                back.Language = cfg.Language;       // the language the player chose since stays
                cfg = back;
            }
            File.Delete(BackupPath);
        }
        catch { }
        if (!Engines.Any(e => e.Id == cfg.Engine && File.Exists(EnginePath(e))))
            cfg.Engine = AvailableEngines.FirstOrDefault()?.Id ?? "native";
        SaveSettings();
        ApplyLanguage();
        StatusText.Text = Tr("Réglages d'avant restaurés.");
        Rebuild();
    }

    // 0 = apply, 1 = not now (asked again next time), 2 = no thanks, don't ask again.
    int ShowOffer(Profile p)
    {
        var lines = new List<string> { Tr("Rendu natif Vulkan") };
        if (p.Fullscreen) lines.Add(Tr("Plein écran"));
        lines.Add(Tr("Rendu interne {0}p", p.Internal));

        int result = 1;
        var win = new Window
        {
            Title = "WWE 2K17 PC", Width = 520, SizeToContent = SizeToContent.Height, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            Background = Hex("#26262A"), FontFamily = FontFamily, ShowInTaskbar = false,
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = Tr(p.Kind == "deck" ? "Cela ressemble à un Steam Deck." : "Cela ressemble à un petit PC (carte graphique intégrée ou peu de mémoire)."),
            Foreground = Brushes.White, FontSize = 18, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
        });
        panel.Children.Add(new TextBlock { Text = Tr("Réglages proposés :"), Foreground = Hex("#B8B8C2"), FontSize = 13 });
        foreach (var l in lines)
            panel.Children.Add(new TextBlock { Text = "•  " + l, Foreground = Hex("#E0E0E6"), FontSize = 14, Margin = new Thickness(8, 2, 0, 0) });
        panel.Children.Add(new TextBlock
        {
            Text = Tr("Tu pourras annuler dans PARAMÈTRES > ANNULER LE PRÉRÉGLAGE."),
            Foreground = Hex("#A8A8B2"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 16),
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button Make(string text, int value, bool primary)
        {
            var b = new Button
            {
                Content = text, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), FontSize = 13,
                Background = Hex(primary ? "#B8141C" : "#44444A"), Foreground = Brushes.White, BorderThickness = new Thickness(0),
                IsDefault = primary,
            };
            b.Click += (_, _) => { result = value; win.Close(); };
            return b;
        }
        buttons.Children.Add(Make(Tr("Non merci, ne plus demander"), 2, false));
        buttons.Children.Add(Make(Tr("Pas maintenant"), 1, false));
        buttons.Children.Add(Make(Tr("Appliquer le profil"), 0, true));
        panel.Children.Add(buttons);
        win.Content = panel;
        win.ShowDialog();
        return result;
    }
}
