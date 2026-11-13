using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace Launcher;

public class LauncherSettings
{
    public string Resolution { get; set; } = "1600x900";
    public bool Fullscreen { get; set; } = false;
    public bool VSync { get; set; } = false;
    public int Scale { get; set; } = 1;        // internal resolution multiplier (resolution_scale)
    public int Aniso { get; set; } = -1;       // -1 = game default, 0 = off, 1..5 = 1x..16x
    public string Language { get; set; } = "english";
    public bool Vibration { get; set; } = true;
    public bool Mute { get; set; } = false;
    public string Extra { get; set; } = "";
    // Graphics engine: native = re:Blue renderer on Vulkan, d3d12 / d3d11 = native on Direct3D, xenos = GPU emulation.
    public string Engine { get; set; } = "native";
    public int Internal { get; set; } = 0;     // native renderer: internal height (svr_render_scale), 0 = auto
    public int Aa { get; set; } = 0;           // native renderer: edge smoothing 0 off, 1 on, 2 strong (svr_fxaa)
    public bool Upscaler { get; set; } = false;
    public bool Fps60 { get; set; } = true;    // wwe2k17_60fps
    public bool Shadows { get; set; } = true;  // wwe2k17_shadows
    public string Gpu { get; set; } = "";      // native renderer: Vulkan name of the GPU, "" = automatic
}

record Engine(string Id, string Name, string Exe, bool Native, string Info);

public partial class MainWindow : Window
{
    static readonly string[] Resolutions = { "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" };
    static readonly string[] AnisoNames = { "PAR DÉFAUT", "DÉSACTIVÉ", "1×", "2×", "4×", "8×", "16×" };
    // Xbox 360 language ids (--user_language): the languages the game has text for.
    static readonly (string Key, string Name, int Id)[] Languages =
    {
        ("english", "ENGLISH", 1), ("french", "FRANÇAIS", 4), ("german", "DEUTSCH", 3),
        ("spanish", "ESPAÑOL", 5), ("italian", "ITALIANO", 6),
    };
    static readonly Engine[] Engines =
    {
        new("native", "NATIF · VULKAN", "wwe2k17_native.exe", true, "Rendu natif Vulkan : le plus rapide et le plus complet (recommandé)."),
        new("d3d12", "NATIF · DIRECT3D 12", "wwe2k17_d3d12.exe", true, "Rendu natif Direct3D 12 : essaie-le si Vulkan pose problème avec ta carte."),
        new("d3d11", "NATIF · DIRECT3D 11", "wwe2k17_d3d11.exe", true, "Rendu natif Direct3D 11 : pour les vieilles cartes graphiques."),
        new("xenos", "ÉMULATION XENOS", "wwe2k17.exe", false, "Émulation de la puce de la Xbox 360 : plus lente, sert de référence."),
    };
    // Internal heights of the native renderer (svr_render_scale); 0 = automatic from the screen and the GPU.
    static readonly int[] InternalHeights = { 0, 540, 720, 1080, 1440, 2160 };
    static readonly string[] AaNames = { "NON", "OUI", "FORT" };
    static string OnOff(bool b) => b ? "OUI" : "NON";
    List<string> gpuNames = new();

    Engine CurrentEngine => Engines.FirstOrDefault(e => e.Id == cfg.Engine) ?? Engines[0];
    string EnginePath(Engine e) => Path.Combine(exeDir, e.Exe);
    IEnumerable<Engine> AvailableEngines => Engines.Where(e => File.Exists(EnginePath(e)));
    static Brush Hex(string h) => (Brush)new BrushConverter().ConvertFromString(h)!;
    static Color HexC(string h) => (Color)ColorConverter.ConvertFromString(h);

    string root = "", exeDir = "", settingsPath = "";
    string? currentLog;
    LauncherSettings cfg = new();
    Process? proc;
    bool loading = true, hasExe;
    GameSetup setup = null!;
    DlcStore dlc = null!;

    // background job (install from ISO, title update, DLC)
    bool busy;
    CancellationTokenSource? jobCancel;

    class Item
    {
        public Border Bar = null!;
        public TextBlock Label = null!, Value = null!;
        public string Info = "";
        public Action Run = () => { };
        public Action<int>? Change;
        public Func<string>? Get;
        public bool IsText;
        public Func<bool> Enabled = () => true;
        public Brush? ValueColor;   // fixed colour for the value (diagnostic rows)
    }
    GameSetup.UpdateState updateState;
    readonly List<Item> items = new();
    readonly Dictionary<string, int> selByScreen = new();
    string screen = "main";
    int sel;
    TextBox? extraBox;

    readonly Random rng = new();
    readonly List<(Ellipse e, double speed, double drift, double phase)> dust = new();
    readonly List<(Ellipse e, double speed, double phase, double baseX)> smoke = new();
    readonly List<Ellipse> lights = new();
    readonly DateTime t0 = DateTime.Now;
    DateTime last = DateTime.Now;

    public MainWindow()
    {
        InitializeComponent();
        FindRoot();
        LoadSettings();
        setup = new GameSetup(root);
        dlc = new DlcStore(Path.Combine(root, "userdata"));
        LoadArt();
        BuildBackdrop();
        CompositionTarget.Rendering += OnFrame;

        var ver = Path.Combine(root, "VERSION");
        VersionText.Text = "LAUNCHER 0.5" + (File.Exists(ver) ? "  ·  PORT " + File.ReadAllText(ver).Trim().ToUpperInvariant() : "");

        var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        tick.Tick += (_, _) => TailLog();
        tick.Start();

        loading = false;
        RefreshChecks();
        LoadGpuNames();
        Loaded += (_, _) => { Focus(); ShowScreen("main"); };
    }

    // ---- paths / settings / optional artwork ---------------------------------
    void FindRoot()
    {
        // Development: the launcher runs from inside the repository, the engines are in out\build\win-amd64-release.
        // Portable: the engines sit next to the launcher.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            foreach (var e in Engines)
            {
                var a = Path.Combine(dir.FullName, "out", "build", "win-amd64-release");
                if (File.Exists(Path.Combine(a, e.Exe))) { root = dir.FullName; exeDir = a; return; }
                if (File.Exists(Path.Combine(dir.FullName, e.Exe))) { root = dir.FullName; exeDir = dir.FullName; return; }
            }
        }
        root = AppContext.BaseDirectory;
        exeDir = root;
    }

    void LoadSettings()
    {
        settingsPath = Path.Combine(root, "userdata", "launcher.json");
        try
        {
            if (File.Exists(settingsPath))
                cfg = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(settingsPath)) ?? new();
        }
        catch { cfg = new(); }
        if (!Resolutions.Contains(cfg.Resolution)) cfg.Resolution = "1600x900";
        if (!Languages.Any(l => l.Key == cfg.Language)) cfg.Language = "english";
        cfg.Scale = Math.Clamp(cfg.Scale, 1, 3);
        cfg.Aniso = Math.Clamp(cfg.Aniso, -1, 5);
        cfg.Aa = Math.Clamp(cfg.Aa, 0, 2);
        if (!InternalHeights.Contains(cfg.Internal)) cfg.Internal = 0;
        // an engine that isn't built here can't be the choice: fall back to the first one that is
        if (!Engines.Any(e => e.Id == cfg.Engine && File.Exists(EnginePath(e))))
            cfg.Engine = AvailableEngines.FirstOrDefault()?.Id ?? "native";
    }

    void SaveSettings()
    {
        if (loading) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(cfg));
        }
        catch { }
    }

    // Optional files in launcher_art\ : hero.png (right side) and logo.png (top-left). Never shipped.
    void LoadArt()
    {
        BitmapImage? Load(string name)
        {
            var p = Path.Combine(root, "launcher_art", name);
            if (!File.Exists(p)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(p);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }
        var hero = Load("hero.png");
        if (hero != null) { HeroImage.Source = hero; HeroImage.Visibility = Visibility.Visible; }
        var logo = Load("logo.png");
        if (logo != null) { LogoImage.Source = logo; LogoImage.Visibility = Visibility.Visible; WweText.Visibility = Visibility.Collapsed; }
    }

    // ---- list building ------------------------------------------------------------
    Item Add(string label, string info, Action run, Func<string>? get = null, Action<int>? change = null,
             Func<bool>? enabled = null, bool text = false)
    {
        var l = new TextBlock { Text = label, FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
        var v = new TextBlock { FontSize = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var g = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        g.Children.Add(l);
        var item = new Item { Label = l, Value = v, Info = info, Run = run, Get = get, Change = change, IsText = text };
        if (enabled != null) item.Enabled = enabled;
        if (text)
        {
            extraBox = new TextBox
            {
                Width = 230, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 14,
                FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Text = cfg.Extra, Foreground = Brushes.White,
                CaretBrush = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Right
            };
            extraBox.TextChanged += (_, _) => { cfg.Extra = extraBox!.Text; SaveSettings(); };
            g.Children.Add(extraBox);
        }
        else if (get != null) g.Children.Add(v);

        item.Bar = new Border { Height = 34, Margin = new Thickness(0, 0, 0, 6), Child = g, Cursor = Cursors.Hand, BorderThickness = new Thickness(2) };
        int idx = items.Count;
        item.Bar.MouseEnter += (_, _) => Select(idx);
        item.Bar.MouseLeftButtonUp += (_, _) => { Select(idx); Activate(item, 1); };
        items.Add(item);
        ItemsPanel.Children.Add(item.Bar);
        return item;
    }

    // Where Échap / RETOUR goes from each screen.
    static string ParentOf(string s) => s is "display" or "graphics" or "gameopts" ? "settings" : "main";

    void Back() => Add("RETOUR", "Revenir à l'écran précédent.", () => ShowScreen(ParentOf(screen)));

    void ShowScreen(string name)
    {
        selByScreen[screen] = sel;
        screen = name;
        items.Clear();
        ItemsPanel.Children.Clear();
        extraBox = null;
        LogoBox.Visibility = name == "main" ? Visibility.Visible : Visibility.Collapsed;
        switch (name)
        {
            case "settings": BuildSettings(); ScreenTitle.Text = "PARAMÈTRES"; break;
            case "display": BuildDisplay(); ScreenTitle.Text = "AFFICHAGE"; break;
            case "graphics": BuildGraphics(); ScreenTitle.Text = "GRAPHISMES"; break;
            case "gameopts": BuildGameOptions(); ScreenTitle.Text = "JEU"; break;
            case "files": BuildFiles(); ScreenTitle.Text = "FICHIERS DU JEU"; break;
            case "dlc": BuildDlc(); ScreenTitle.Text = "CONTENU TÉLÉCHARGEABLE"; break;
            case "diag": BuildDiag(); ScreenTitle.Text = "DIAGNOSTIC"; break;
            default: BuildMain(); ScreenTitle.Text = "MENU PRINCIPAL"; break;
        }
        Select(selByScreen.TryGetValue(name, out var s) ? s : 0);
        Keyboard.ClearFocus();
        Focus();
    }

    void Rebuild() => ShowScreen(screen);

    void BuildMain()
    {
        Add("JOUER", "Lance WWE 2K17 avec tes paramètres.", Play, enabled: () => (hasExe && setup.HasGame && !busy) || proc != null)
            .Label.Text = proc != null ? "ARRÊTER LE JEU" : "JOUER";
        Add("PARAMÈTRES", "Affichage, graphismes, langue, son et manette.", () => ShowScreen("settings"));
        Add("FICHIERS DU JEU", "Installe le jeu depuis ton ISO, ajoute le Title Update 1.04, ouvre les dossiers.", () => ShowScreen("files"));
        Add("DLC", "Ajoute ou retire les contenus téléchargeables (DLC).", () => ShowScreen("dlc"));
        Add("DIAGNOSTIC", "Vérifie que tout est en place et copie un rapport à joindre en cas de problème.", () => ShowScreen("diag"));
        Add("QUITTER", "Ferme le launcher.", Close);
    }

    void BuildSettings()
    {
        Add("AFFICHAGE", "Fenêtré ou plein écran, résolution de la fenêtre, V-Sync, carte graphique.", () => ShowScreen("display"));
        Add("GRAPHISMES", "Moteur graphique, finesse du rendu, anticrénelage, filtrage, ombres.", () => ShowScreen("graphics"));
        Add("JEU", "Langue, 60 images/s, vibration, son, options avancées.", () => ShowScreen("gameopts"));
        Back();
    }

    void BuildDisplay()
    {
        Add("AFFICHAGE", "Fenêtré ou plein écran.", () => { }, () => cfg.Fullscreen ? "PLEIN ÉCRAN" : "FENÊTRÉ", _ => cfg.Fullscreen = !cfg.Fullscreen);
        Add("RÉSOLUTION", "Taille de la fenêtre du jeu (720p, 900p, 1080p, 1440p, 4K).", () => { }, () => cfg.Resolution.Replace("x", " × "), d =>
        {
            int i = Array.IndexOf(Resolutions, cfg.Resolution);
            cfg.Resolution = Resolutions[(i + d + Resolutions.Length) % Resolutions.Length];
        });
        Add("V-SYNC", "Synchronise l'image avec l'écran pour éviter les déchirures. Peut ajouter un peu de latence.", () => { },
            () => OnOff(cfg.VSync), _ => cfg.VSync = !cfg.VSync);
        if (CurrentEngine.Native)
        {
            var choices = new List<string> { "" };
            choices.AddRange(gpuNames);
            if (cfg.Gpu.Length > 0 && !choices.Contains(cfg.Gpu)) choices.Add(cfg.Gpu);
            Add("CARTE GRAPHIQUE", "AUTO choisit la plus puissante. Change-la si ton PC a deux cartes (portable) et que le jeu prend la mauvaise.", () => { },
                () => cfg.Gpu.Length == 0 ? "AUTO" : cfg.Gpu.ToUpperInvariant(), d =>
                {
                    int i = Math.Max(0, choices.IndexOf(cfg.Gpu));
                    cfg.Gpu = choices[(i + d + choices.Count) % choices.Count];
                });
        }
        Back();
    }

    void BuildGraphics()
    {
        var avail = AvailableEngines.ToList();
        Add("MOTEUR GRAPHIQUE", avail.Count == 0 ? "Aucun moteur compilé : lance tools\\build.ps1." : CurrentEngine.Info, () => { },
            () => avail.Count == 0 ? "AUCUN" : CurrentEngine.Name, d =>
            {
                if (avail.Count == 0) return;
                int i = Math.Max(0, avail.FindIndex(e => e.Id == cfg.Engine));
                cfg.Engine = avail[(i + d + avail.Count) % avail.Count].Id;
                SaveSettings();
                Rebuild();           // the rows below depend on the engine
            });
        if (CurrentEngine.Native)
        {
            Add("RENDU INTERNE", "Finesse du rendu. AUTO adapte à ton écran et à ta carte. Plus haut = plus net mais plus lourd (540p/720p = cartes faibles).", () => { },
                () => cfg.Internal == 0 ? "AUTO" : cfg.Internal + "p", d =>
                {
                    int i = Array.IndexOf(InternalHeights, cfg.Internal);
                    cfg.Internal = InternalHeights[(i + d + InternalHeights.Length) % InternalHeights.Length];
                });
            Add("ANTICRÉNELAGE", "Adoucit les bords crénelés (FXAA).", () => { }, () => AaNames[cfg.Aa], d => cfg.Aa = (cfg.Aa + d + 3) % 3);
            Add("MISE À L'ÉCHELLE", "Quand le rendu interne est plus petit que la fenêtre, l'agrandit avec un filtre plus net que le flou habituel.", () => { },
                () => OnOff(cfg.Upscaler), _ => cfg.Upscaler = !cfg.Upscaler);
        }
        else
        {
            Add("RÉSOLUTION INTERNE", "Multiplie la finesse du rendu (1× = comme la Xbox 360). Plus haut = plus net mais plus lourd.", () => { },
                () => cfg.Scale + "×", d => cfg.Scale = ((cfg.Scale - 1 + d + 3) % 3) + 1);
        }
        Add("FILTRAGE ANISOTROPE", "Textures plus nettes vues de biais.", () => { },
            () => AnisoNames[cfg.Aniso + 1], d => cfg.Aniso = ((cfg.Aniso + 1 + d + AnisoNames.Length) % AnisoNames.Length) - 1);
        Add("OMBRES DU RING", "Ombres sur le tapis. Désactive-les sur une carte graphique faible pour gagner des images.", () => { },
            () => OnOff(cfg.Shadows), _ => cfg.Shadows = !cfg.Shadows);
        Back();
    }

    void BuildGameOptions()
    {
        Add("LANGUE DU JEU", "Langue des textes du jeu (le launcher reste en français).", () => { },
            () => Languages.First(l => l.Key == cfg.Language).Name, d =>
            {
                int i = Array.FindIndex(Languages, l => l.Key == cfg.Language);
                cfg.Language = Languages[(i + d + Languages.Length) % Languages.Length].Key;
            });
        Add("60 IMAGES/S", "Entrées, victoires, rediffusions et cinématiques à 60 images/s au lieu de 30 comme sur Xbox 360.", () => { },
            () => OnOff(cfg.Fps60), _ => cfg.Fps60 = !cfg.Fps60);
        Add("VIBRATION", "Vibration de la manette.", () => { }, () => OnOff(cfg.Vibration), _ => cfg.Vibration = !cfg.Vibration);
        Add("COUPER LE SON", "Lance le jeu sans son.", () => { }, () => OnOff(cfg.Mute), _ => cfg.Mute = !cfg.Mute);
        Add("OPTIONS AVANCÉES", "Arguments en plus pour le jeu (facultatif). Entrée pour écrire, Échap pour finir.", () => { }, text: true);
        Back();
    }

    // Read-only checks plus a report to paste in a bug report.
    void BuildDiag()
    {
        foreach (var (label, value, ok, info) in DiagLines())
        {
            var it = Add(label, info, () => { }, () => value);
            it.Bar.Cursor = Cursors.Arrow;
            it.ValueColor = Hex(ok ? "#3DDC84" : "#FF5A6B");
        }
        Add("COPIER LE RAPPORT", "Copie un rapport complet (versions, fichiers, dernières lignes du journal) à coller dans un message.", CopyReport);
        Add("OUVRIR LE DERNIER JOURNAL", "Ouvre le dernier fichier log dans le Bloc-notes.", OpenLastLog);
        Back();
    }

    void BuildFiles()
    {
        Add("INSTALLER DEPUIS UN ISO", "Choisis ton image disque WWE 2K17 (.iso) : le launcher extrait le jeu dans game\\ (environ 9 Go).",
            InstallIso, () => setup.HasGame ? "INSTALLÉ" : "À FAIRE", enabled: () => !busy);
        Add("INSTALLER DEPUIS UN DOSSIER", "Choisis le dossier du disque déjà extrait (celui qui contient default.xex) : il est copié dans game\\.",
            InstallFolder, enabled: () => !busy);
        Add("INSTALLER LE TITLE UPDATE 1.04", "Choisis le fichier tu00000001_00000000 (version 1.04, environ 109 Mo) : le launcher l'extrait et l'applique au jeu.",
            InstallTitleUpdate, () => updateState switch
            {
                GameSetup.UpdateState.Ok => "1.04 INSTALLÉ",
                GameSetup.UpdateState.Wrong => "MAUVAISE VERSION",
                _ => "À FAIRE",
            }, enabled: () => !busy);
        Add("OUVRIR LES SAUVEGARDES", "Ouvre le dossier des sauvegardes et données (userdata).", () => OpenDir("userdata"));
        Add("OUVRIR LES JOURNAUX", "Ouvre le dossier des logs : utile si le jeu plante.", () => OpenDir("logs"));
        Add("OUVRIR LE DOSSIER DU JEU", "Ouvre le dossier du projet dans l'explorateur.", () => OpenDir(""));
        Back();
    }

    void BuildDlc()
    {
        Add("AJOUTER DES FICHIERS DLC", "Choisis un ou plusieurs paquets DLC Xbox 360 (les fichiers sans extension, noms en chiffres et lettres).",
            AddDlcFiles, enabled: () => !busy);
        Add("AJOUTER UN DOSSIER DE DLC", "Choisis un dossier : tous les paquets DLC trouvés dedans (sous-dossiers compris) sont ajoutés.",
            AddDlcFolder, enabled: () => !busy);
        List<DlcStore.InstalledDlc> installed;
        try { installed = dlc.List(); } catch { installed = new(); }
        foreach (var d in installed)
        {
            var d1 = d;
            Add(d.DisplayName.Length > 0 ? d.DisplayName.ToUpperInvariant() : d.Name.ToUpperInvariant(),
                $"{d.SizeBytes / 1048576} Mo installés. Entrée pour retirer ce DLC.", () => RemoveDlc(d1), () => "INSTALLÉ", enabled: () => !busy);
        }
        if (installed.Count == 0) { /* nothing yet: the info line explains */ }
        Back();
    }

    // ---- selection styling ---------------------------------------------------------
    void StyleItem(Item it, bool on)
    {
        bool en = it.Enabled();
        if (on)
        {
            it.Bar.Background = new LinearGradientBrush(HexC("#B8141C"), HexC("#5C0A10"), 0);
            it.Bar.BorderBrush = Hex("#E8383F");
            it.Bar.Effect = new DropShadowEffect { Color = HexC("#E5162B"), BlurRadius = 20, ShadowDepth = 0, Opacity = 0.55 };
        }
        else
        {
            it.Bar.Background = Hex("#59000000");
            it.Bar.BorderBrush = Hex(en ? "#B8B8BC" : "#5E5E62");
            it.Bar.Effect = null;
        }
        var fg = Hex(on ? "#FFFFFF" : en ? "#DCDCE0" : "#6A6A70");
        it.Label.Foreground = fg;
        it.Value.Foreground = on ? fg : (it.ValueColor ?? fg);
        if (it.IsText && extraBox != null) extraBox.Foreground = Brushes.White;
        if (it.Get != null)
            it.Value.Text = (on && it.Change != null ? "◄  " : "") + it.Get() + (on && it.Change != null ? "  ►" : "");
    }

    void Select(int i)
    {
        if (items.Count == 0) return;
        sel = Math.Clamp(i, 0, items.Count - 1);
        for (int k = 0; k < items.Count; k++) StyleItem(items[k], k == sel);
        InfoText.Text = items[sel].Info;
        if (screen == "dlc" && sel == 2 && items.Count == 3) InfoText.Text = "Aucun DLC installé pour l'instant.";
    }

    void Activate(Item it, int dir)
    {
        if (it.IsText) { extraBox?.Focus(); if (extraBox != null) extraBox.CaretIndex = extraBox.Text.Length; return; }
        if (!it.Enabled()) return;
        if (it.Change != null)
        {
            it.Change(dir);
            SaveSettings();
            Select(sel);
        }
        else it.Run();
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (extraBox != null && extraBox.IsKeyboardFocused)
        {
            if (e.Key is Key.Escape or Key.Enter or Key.Up or Key.Down) { Keyboard.ClearFocus(); Focus(); e.Handled = true; }
            return;
        }
        switch (e.Key)
        {
            case Key.Up: Select(sel - 1); break;
            case Key.Down: Select(sel + 1); break;
            case Key.Left when items[sel].Change != null: Activate(items[sel], -1); break;
            case Key.Right when items[sel].Change != null: Activate(items[sel], 1); break;
            case Key.Enter or Key.Space: Activate(items[sel], 1); break;
            case Key.Escape:
                if (busy) { jobCancel?.Cancel(); }
                else if (screen != "main") ShowScreen(ParentOf(screen));
                break;
            default: return;
        }
        e.Handled = true;
    }

    // ---- readiness -----------------------------------------------------------------
    void RefreshChecks()
    {
        Checks.Children.Clear();
        hasExe = File.Exists(EnginePath(CurrentEngine));
        updateState = setup.CheckUpdate();
        AddCheck("MOTEUR", hasExe);
        AddCheck("DONNÉES DU JEU", setup.HasGame);
        AddCheck("TITLE UPDATE 1.04", updateState == GameSetup.UpdateState.Ok);
        if (!busy)
        {
            if (!hasExe) StatusText.Text = "Le moteur n'est pas compilé : lance tools\\build.ps1.";
            else if (!setup.HasGame) StatusText.Text = "Le jeu n'est pas installé : va dans FICHIERS DU JEU.";
            else if (updateState == GameSetup.UpdateState.Missing) StatusText.Text = "Il manque le Title Update 1.04 : va dans FICHIERS DU JEU.";
            else if (updateState == GameSetup.UpdateState.Wrong)
                StatusText.Text = "Ce n'est pas le Title Update 1.04 : le moteur est fait pour la 1.04, réinstalle-la dans FICHIERS DU JEU.";
        }
    }

    // ---- diagnostic ----------------------------------------------------------------------
    IEnumerable<(string Label, string Value, bool Ok, string Info)> DiagLines()
    {
        int built = Engines.Count(e => File.Exists(EnginePath(e)));
        yield return ("MOTEURS GRAPHIQUES", built + " / " + Engines.Length, File.Exists(EnginePath(CurrentEngine)),
            "Moteurs compilés : " + string.Join(", ", Engines.Where(e => File.Exists(EnginePath(e))).Select(e => e.Name.ToLowerInvariant())) + ".");
        yield return ("DONNÉES DU JEU", setup.HasGame ? "PRÉSENTES" : "ABSENTES", setup.HasGame, "Dossier game\\ avec default.xex.");
        yield return ("TITLE UPDATE", updateState switch
        {
            GameSetup.UpdateState.Ok => "1.04",
            GameSetup.UpdateState.Wrong => "MAUVAISE VERSION",
            _ => "ABSENT",
        }, updateState == GameSetup.UpdateState.Ok, "Le moteur est compilé pour la version 1.04 du Title Update.");
        int dlcCount; try { dlcCount = dlc.List().Count; } catch { dlcCount = 0; }
        yield return ("DLC INSTALLÉS", dlcCount + " / 6", true, "Les contenus téléchargeables installés dans userdata.");
        long freeGb = 0;
        try { freeGb = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace >> 30; } catch { }
        double ramGb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0;
        yield return ("PC", $"{ramGb:0.#} GO RAM · {freeGb} GO LIBRES", ramGb >= 7.5 && freeGb >= 5,
            "Mémoire vive (8 Go minimum conseillés) et place libre sur le disque du jeu (les caches de shaders grossissent).");
        yield return ("CARTE GRAPHIQUE", gpuNames.Count == 0 ? "?" : gpuNames[0].ToUpperInvariant(), gpuNames.Count > 0,
            gpuNames.Count > 1 ? "Cartes vues : " + string.Join(", ", gpuNames) : "Carte détectée par Windows.");
    }

    string BuildReport()
    {
        var sb = new System.Text.StringBuilder();
        string ver = File.Exists(Path.Combine(root, "VERSION")) ? File.ReadAllText(Path.Combine(root, "VERSION")).Trim() : "?";
        sb.AppendLine($"WWE 2K17 PC {ver} · launcher 0.5 · {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Windows {Environment.OSVersion.Version} · {Environment.ProcessorCount} threads");
        foreach (var (label, value, _, _) in DiagLines()) sb.AppendLine($"{label}: {value}");
        foreach (var e in Engines) sb.AppendLine($"  {e.Exe}: {(File.Exists(EnginePath(e)) ? "présent" : "absent")}{(e.Id == cfg.Engine ? " (choisi)" : "")}");
        sb.AppendLine($"Réglages: moteur {cfg.Engine}, {cfg.Resolution}, plein écran {cfg.Fullscreen}, V-Sync {cfg.VSync}, " +
                      $"interne {(CurrentEngine.Native ? (cfg.Internal == 0 ? "auto" : cfg.Internal + "p") : cfg.Scale + "x")}, AA {cfg.Aa}, " +
                      $"upscaler {cfg.Upscaler}, aniso {cfg.Aniso}, 60 ips {cfg.Fps60}, ombres {cfg.Shadows}, langue {cfg.Language}, gpu '{cfg.Gpu}'");
        var log = LastLog();
        if (log != null)
        {
            sb.AppendLine($"--- {Path.GetFileName(log)} (dernières lignes) ---");
            try
            {
                using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                int n = (int)Math.Min(fs.Length, 6000);
                fs.Seek(-n, SeekOrigin.End);
                var buf = new byte[n];
                int read = fs.Read(buf, 0, n);
                var lines = System.Text.Encoding.UTF8.GetString(buf, 0, read).Split('\n', StringSplitOptions.TrimEntries);
                foreach (var l in lines.TakeLast(25)) sb.AppendLine(l);
            }
            catch (IOException) { }
        }
        return sb.ToString();
    }

    string? LastLog()
    {
        var d = Path.Combine(root, "logs");
        return Directory.Exists(d)
            ? new DirectoryInfo(d).EnumerateFiles("run-*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()?.FullName
            : null;
    }

    void CopyReport()
    {
        try { Clipboard.SetText(BuildReport()); StatusText.Text = "Rapport copié : colle-le dans ton message."; }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        { StatusText.Text = "Impossible de copier : " + ex.Message; }
    }

    void OpenLastLog()
    {
        var log = LastLog();
        if (log == null) { StatusText.Text = "Aucun journal pour l'instant : lance le jeu une fois."; return; }
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{log}\"") { UseShellExecute = true });
    }

    // GPU names as Windows reports them (the native renderer matches them against Vulkan's device names).
    async void LoadGpuNames()
    {
        try
        {
            var names = await Task.Run(() =>
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_VideoController).Name\"")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi)!;
                string text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            });
            gpuNames = names;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { }
    }

    void AddCheck(string name, bool ok)
    {
        var c = ok ? HexC("#3DDC84") : HexC("#FF5A6B");
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 3) };
        row.Children.Add(new TextBlock { Text = name, Foreground = Hex("#B8B8C2"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        row.Children.Add(new Rectangle { Width = 8, Height = 8, Fill = new SolidColorBrush(c), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(45) });
        Checks.Children.Add(row);
    }

    // ---- background jobs ---------------------------------------------------------------
    // Runs `work` off the UI thread with a progress bar; Échap cancels it.
    async void RunJob(string label, Func<Action<long, long>, CancellationToken, string?> work)
    {
        if (busy) return;
        busy = true;
        jobCancel = new CancellationTokenSource();
        ProgressTrack.Visibility = Visibility.Visible;
        ProgressFill.Width = 0;
        StatusText.Text = label + " (Échap pour annuler)";
        double lastShown = -1;
        void Progress(long done, long total)
        {
            double f = total > 0 ? (double)done / total : 0;
            if (f - lastShown < 0.002 && f < 1) return;
            lastShown = f;
            Dispatcher.BeginInvoke(() =>
            {
                ProgressFill.Width = ProgressTrack.Width * Math.Clamp(f, 0, 1);
                StatusText.Text = $"{label} : {f * 100:0}%  (Échap pour annuler)";
            });
        }
        string result;
        try
        {
            var token = jobCancel.Token;
            result = await Task.Run(() => work(Progress, token)) ?? "Terminé.";
        }
        catch (OperationCanceledException) { result = "Annulé."; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        { result = "Erreur : " + ex.Message; }
        busy = false;
        jobCancel = null;
        ProgressTrack.Visibility = Visibility.Collapsed;
        StatusText.Text = result;
        RefreshChecks();
        if (!StatusText.Text.StartsWith("Erreur")) StatusText.Text = result;
        Rebuild();
    }

    void InstallIso()
    {
        var dlg = new OpenFileDialog { Title = "Choisis l'image disque de WWE 2K17", Filter = "Image disque Xbox 360 (*.iso)|*.iso|Tous les fichiers|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        if (!ConfirmOverwrite()) return;
        string iso = dlg.FileName;
        RunJob("Extraction du disque", (p, ct) => { setup.InstallIso(iso, p, ct); return "Jeu installé dans game\\. Ajoute maintenant le Title Update 2."; });
    }

    void InstallFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choisis le dossier du disque extrait (celui qui contient default.xex)" };
        if (dlg.ShowDialog(this) != true) return;
        if (!ConfirmOverwrite()) return;
        string dir = dlg.FolderName;
        RunJob("Copie du jeu", (p, ct) => { setup.InstallFolder(dir, p, ct); return "Jeu copié dans game\\."; });
    }

    bool ConfirmOverwrite() =>
        !setup.HasGame || MessageBox.Show(this, "Le dossier game\\ contient déjà un jeu. Les fichiers identiques seront remplacés. Continuer ?",
            "WWE 2K17 PC", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    void InstallTitleUpdate()
    {
        var dlg = new OpenFileDialog { Title = "Choisis le Title Update 2 (tu00000001_00000000)", Filter = "Tous les fichiers|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        string pkg = dlg.FileName;
        RunJob("Installation du Title Update", (p, ct) =>
        {
            string name = setup.InstallTitleUpdate(pkg, p);
            return $"Title Update installé ({name}).";
        });
    }

    void AddDlcFiles()
    {
        var dlg = new OpenFileDialog { Title = "Choisis les paquets DLC de WWE 2K17", Multiselect = true, Filter = "Tous les fichiers|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        InstallDlc(dlg.FileNames);
    }

    void AddDlcFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choisis le dossier qui contient les DLC" };
        if (dlg.ShowDialog(this) != true) return;
        var found = new DirectoryInfo(dlg.FolderName).EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => f.Length > 0x1000 && StfsPackage.HasPackageMagic(f.FullName)).Select(f => f.FullName).ToArray();
        if (found.Length == 0) { StatusText.Text = "Aucun paquet DLC trouvé dans ce dossier."; return; }
        InstallDlc(found);
    }

    void InstallDlc(string[] files)
    {
        RunJob($"Installation de {files.Length} DLC", (p, ct) =>
        {
            int ok = 0;
            var problems = new List<string>();
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                var r = dlc.Install(f, p);
                if (r.Ok) ok++; else problems.Add($"{r.File} : {r.Message}");
            }
            return $"{ok} DLC installé(s)." + (problems.Count > 0 ? " Problème : " + problems[0] : "");
        });
    }

    void RemoveDlc(DlcStore.InstalledDlc d)
    {
        if (MessageBox.Show(this, $"Retirer « {d.DisplayName} » ?", "WWE 2K17 PC", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { dlc.Remove(d.Name); StatusText.Text = "DLC retiré."; }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        { StatusText.Text = "Impossible de retirer ce DLC : " + ex.Message; }
        Rebuild();
    }

    // ---- launching -------------------------------------------------------------------------
    void Play()
    {
        if (proc != null) { try { proc.Kill(true); } catch { } return; }
        RefreshChecks();
        if (!hasExe || !setup.HasGame) return;

        var res = cfg.Resolution.Split('x');
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        Directory.CreateDirectory(Path.Combine(root, "userdata"));
        currentLog = Path.Combine(root, "logs", $"run-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var engine = CurrentEngine;
        var psi = new ProcessStartInfo(EnginePath(engine)) { WorkingDirectory = root, UseShellExecute = false };
        void A(string s) => psi.ArgumentList.Add(s);
        string B(bool v) => v ? "true" : "false";
        // shared by every engine
        A("--game_data_root=" + Path.Combine(root, "game"));
        A("--user_data_root=" + Path.Combine(root, "userdata"));
        A("--update_data_root=" + Path.Combine(root, "userdata", "title_update"));
        A("--cache_root=" + Path.Combine(root, "userdata", "cache"));
        A("--fullscreen=" + B(cfg.Fullscreen));
        A("--window_width=" + res[0]);
        A("--window_height=" + res[1]);
        A("--log_file=" + currentLog);
        A("--user_language=" + Languages.First(l => l.Key == cfg.Language).Id);
        A("--vibration=" + B(cfg.Vibration));
        A("--wwe2k17_60fps=" + B(cfg.Fps60));
        A("--wwe2k17_shadows=" + B(cfg.Shadows));
        if (cfg.Mute) A("--audio_mute=true");
        if (engine.Native)
        {
            // re:Blue renderer: its own options (the Xenos ones don't exist there)
            A("--bd_vsync=" + B(cfg.VSync));
            if (cfg.Internal > 0) A("--svr_render_scale=" + cfg.Internal);
            A("--svr_fxaa=" + cfg.Aa);
            A("--svr_upscaler=" + (cfg.Upscaler ? 1 : 0));
            if (cfg.Aniso >= 0) A("--bd_anisotropy=" + (cfg.Aniso == 0 ? 0 : 1 << (cfg.Aniso - 1)));
            if (cfg.Gpu.Length > 0) A("--svr_gpu_device=" + cfg.Gpu);
        }
        else
        {
            A("--gpu_vsync=" + B(cfg.VSync));
            if (cfg.Scale > 1) A("--resolution_scale=" + cfg.Scale);
            if (cfg.Aniso >= 0) A("--anisotropic_override=" + cfg.Aniso);
        }
        foreach (var extra in cfg.Extra.Split(' ', StringSplitOptions.RemoveEmptyEntries)) A(extra);

        // Test hook: write the command line to this file instead of starting the game.
        var dry = Environment.GetEnvironmentVariable("WWE2K17_LAUNCHER_DRYRUN");
        if (!string.IsNullOrEmpty(dry))
        {
            File.WriteAllLines(dry, new[] { psi.FileName }.Concat(psi.ArgumentList));
            StatusText.Text = "Essai : ligne de commande écrite, le jeu n'est pas lancé.";
            return;
        }

        try
        {
            var p = Process.Start(psi) ?? throw new InvalidOperationException("démarrage impossible");
            proc = p;
            p.EnableRaisingEvents = true;
            var started = DateTime.Now;
            p.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                int code = p.ExitCode;
                var span = DateTime.Now - started;
                proc = null;
                StatusText.Text = code == 0
                    ? $"Jeu fermé normalement ({span:mm\\:ss})."
                    : $"Le jeu s'est arrêté (code {code}) après {span:mm\\:ss}. Va dans DIAGNOSTIC pour copier un rapport ou ouvrir le journal.";
                Rebuild();
            });
            StatusText.Text = $"Jeu lancé (PID {p.Id}). Bon match !";
            Rebuild();
        }
        catch (Exception ex)
        {
            proc = null;
            StatusText.Text = "Impossible de lancer le jeu : " + ex.Message;
            Rebuild();
        }
    }

    void TailLog()
    {
        if (currentLog == null || !File.Exists(currentLog)) return;
        try
        {
            using var fs = new FileStream(currentLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            int n = (int)Math.Min(fs.Length, 4096);
            fs.Seek(-n, SeekOrigin.End);
            var buf = new byte[n];
            int read = fs.Read(buf, 0, n);
            var lines = System.Text.Encoding.UTF8.GetString(buf, 0, read)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length > 0) LogLine.Text = lines[^1];
        }
        catch { }
    }

    void OpenDir(string sub)
    {
        var d = sub.Length == 0 ? root : Path.Combine(root, sub);
        Directory.CreateDirectory(d);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{d}\"") { UseShellExecute = true });
    }

    void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---- backdrop: smoke, stadium lights, dust -----------------------------------------------
    void BuildBackdrop()
    {
        for (int i = 0; i < 5; i++)
        {
            double w = 420 + rng.NextDouble() * 300;
            var e = new Ellipse
            {
                Width = w, Height = w * 0.55, IsHitTestVisible = false,
                Fill = new RadialGradientBrush(HexC("#30FFFFFF"), HexC("#00FFFFFF"))
            };
            Canvas.SetTop(e, 80 + rng.NextDouble() * 460);
            Smoke.Children.Add(e);
            smoke.Add((e, 6 + rng.NextDouble() * 10, rng.NextDouble() * 6.28, rng.NextDouble() * 1000 - 200));
        }
        for (int row = 0; row < 2; row++)
            for (int k = 0; k < 6; k++)
            {
                double x = 560 + (k % 3) * 100 + (k / 3) * 270;
                var e = new Ellipse
                {
                    Width = 9, Height = 9, Fill = Brushes.White,
                    Effect = new BlurEffect { Radius = 5 + row * 3 }, Opacity = 0.5
                };
                Canvas.SetLeft(e, x);
                Canvas.SetTop(e, 70 + row * 12);
                Lights.Children.Add(e);
                lights.Add(e);
                if (row == 1) break;
            }
        for (int i = 0; i < 22; i++)
        {
            double size = 1.5 + rng.NextDouble() * 2.5;
            var e = new Ellipse { Width = size, Height = size, Fill = Brushes.White };
            Canvas.SetLeft(e, rng.NextDouble() * 1080);
            Canvas.SetTop(e, rng.NextDouble() * 640);
            Smoke.Children.Add(e);
            dust.Add((e, 6 + rng.NextDouble() * 18, rng.NextDouble() * 16 - 8, rng.NextDouble() * 6.28));
        }
    }

    void OnFrame(object? s, EventArgs a)
    {
        var now = DateTime.Now;
        double dt = Math.Min((now - last).TotalSeconds, 0.1);
        last = now;
        double t = (now - t0).TotalSeconds;

        foreach (var (e, speed, phase, baseX) in smoke)
        {
            double x = Canvas.GetLeft(e);
            if (double.IsNaN(x)) x = baseX;
            x += speed * dt;
            if (x > 1120) x = -e.Width;
            Canvas.SetLeft(e, x);
            e.Opacity = 0.55 + 0.35 * Math.Sin(t * 0.25 + phase);
        }
        foreach (var (e, speed, drift, phase) in dust)
        {
            double y = Canvas.GetTop(e) - speed * dt;
            double x = Canvas.GetLeft(e) + Math.Sin(t * 0.8 + phase) * drift * dt;
            if (y < -10) { y = 650; x = rng.NextDouble() * 1080; }
            Canvas.SetTop(e, y);
            Canvas.SetLeft(e, x);
            e.Opacity = 0.1 + 0.3 * (0.5 + 0.5 * Math.Sin(t * 1.2 + phase));
        }
        for (int i = 0; i < lights.Count; i++) lights[i].Opacity = 0.45 + 0.2 * Math.Sin(t * 0.9 + i);
    }

    protected override void OnClosed(EventArgs e)
    {
        jobCancel?.Cancel();
        CompositionTarget.Rendering -= OnFrame;
        base.OnClosed(e);
    }
}
