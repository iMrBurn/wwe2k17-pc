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
using Path = System.IO.Path;

namespace Launcher;

public class LauncherSettings
{
    public string Resolution { get; set; } = "1600x900";
    public bool Fullscreen { get; set; } = false;
    public bool VSync { get; set; } = false;
    public int Scale { get; set; } = 1;        // internal resolution multiplier (resolution_scale)
    public int Aniso { get; set; } = -1;       // -1 = game default, 0 = off, 1..5 = 1x..16x
    public bool Vibration { get; set; } = true;
    public bool Mute { get; set; } = false;
    public string Extra { get; set; } = "";
}

public partial class MainWindow : Window
{
    static readonly string[] Resolutions = { "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" };
    static readonly string[] AnisoNames = { "PAR DÉFAUT", "DÉSACTIVÉ", "1×", "2×", "4×", "8×", "16×" };
    static string OnOff(bool b) => b ? "OUI" : "NON";
    static Brush Hex(string h) => (Brush)new BrushConverter().ConvertFromString(h)!;
    static Color HexC(string h) => (Color)ColorConverter.ConvertFromString(h);

    string root = "", exe = "", settingsPath = "";
    string? currentLog;
    LauncherSettings cfg = new();
    Process? proc;
    bool loading = true, hasExe;

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
    }
    readonly List<Item> menu = new(), options = new();
    int screen, selMenu, selOpt;
    TextBox extraBox = null!;

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
        LoadArt();
        BuildMenu();
        BuildOptions();
        BuildBackdrop();
        RefreshChecks();
        CompositionTarget.Rendering += OnFrame;

        var ver = Path.Combine(root, "VERSION");
        VersionText.Text = "LAUNCHER 0.3" + (File.Exists(ver) ? "  ·  PORT " + File.ReadAllText(ver).Trim().ToUpperInvariant() : "");

        var tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        tick.Tick += (_, _) => TailLog();
        tick.Start();

        loading = false;
        Loaded += (_, _) => { Focus(); ShowScreen(0); };
    }

    // ---- paths / settings / optional artwork ---------------------------------
    void FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var a = Path.Combine(dir.FullName, "out", "build", "win-amd64-release", "wwe2k17.exe");
            var b = Path.Combine(dir.FullName, "wwe2k17.exe");
            if (File.Exists(a)) { root = dir.FullName; exe = a; return; }
            if (File.Exists(b)) { root = dir.FullName; exe = b; return; }
        }
        root = AppContext.BaseDirectory;
        exe = Path.Combine(root, "wwe2k17.exe");
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

    // Optional files the user can drop in launcher_art\ : hero.png (right side), logo.png (top-left).
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

    // ---- bars (2K17 list style) ---------------------------------------------
    Item MakeBar(Panel parent, string label, string info, bool hasValue, bool text)
    {
        var l = new TextBlock { Text = label, FontSize = 19, VerticalAlignment = VerticalAlignment.Center };
        var v = new TextBlock { FontSize = 17, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var g = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        g.Children.Add(l);
        var item = new Item { Label = l, Value = v, Info = info, IsText = text };
        if (text)
        {
            extraBox = new TextBox
            {
                Width = 230, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 14,
                FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Text = cfg.Extra, Foreground = Brushes.White,
                CaretBrush = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Right
            };
            extraBox.TextChanged += (_, _) => { cfg.Extra = extraBox.Text; SaveSettings(); };
            g.Children.Add(extraBox);
        }
        else if (hasValue) g.Children.Add(v);

        item.Bar = new Border
        {
            Height = 36, Margin = new Thickness(0, 0, 0, 7), Child = g, Cursor = Cursors.Hand,
            BorderThickness = new Thickness(2)
        };
        parent.Children.Add(item.Bar);
        return item;
    }

    void BuildMenu()
    {
        void Add(string label, string info, Action run, Func<bool>? en = null)
        {
            var it = MakeBar(MenuPanel, label, info, false, false);
            it.Run = run;
            if (en != null) it.Enabled = en;
            int i = menu.Count;
            it.Bar.MouseEnter += (_, _) => { if (screen == 0) SelectMenu(i); };
            it.Bar.MouseLeftButtonUp += (_, _) => { if (screen == 0) { SelectMenu(i); if (it.Enabled()) it.Run(); } };
            menu.Add(it);
        }
        Add("JOUER", "Lance WWE 2K17 avec tes paramètres.", Play, () => hasExe || proc != null);
        Add("PARAMÈTRES", "Mode d'affichage, résolution et options avancées.", () => ShowScreen(1));
        Add("SAUVEGARDES", "Ouvre le dossier des sauvegardes et données (userdata).", () => OpenDir("userdata"));
        Add("JOURNAUX", "Ouvre le dossier des logs : utile si le jeu plante.", OpenLogs);
        Add("DOSSIER DU JEU", "Ouvre le dossier du projet dans l'explorateur.", OpenFolder);
        Add("QUITTER", "Ferme le launcher.", Close);
    }

    void BuildOptions()
    {
        void Add(string label, string info, Func<string>? get, Action<int>? change, bool text = false)
        {
            var it = MakeBar(OptionsPanel, label, info, get != null, text);
            it.Get = get; it.Change = change;
            int i = options.Count;
            it.Bar.MouseEnter += (_, _) => { if (screen == 1) SelectOpt(i); };
            it.Bar.MouseLeftButtonUp += (_, _) => { if (screen == 1) { SelectOpt(i); ActivateOpt(it, 1); } };
            options.Add(it);
        }
        Add("AFFICHAGE", "Fenêtré ou plein écran.", () => cfg.Fullscreen ? "PLEIN ÉCRAN" : "FENÊTRÉ", _ => cfg.Fullscreen = !cfg.Fullscreen);
        Add("RÉSOLUTION", "Taille de la fenêtre du jeu (720p, 900p, 1080p, 1440p, 4K).", () => cfg.Resolution.Replace("x", " × "), d =>
        {
            int i = Array.IndexOf(Resolutions, cfg.Resolution);
            cfg.Resolution = Resolutions[(i + d + Resolutions.Length) % Resolutions.Length];
        });
        Add("V-SYNC", "Synchronise l'image avec l'écran pour éviter les déchirures. Peut ajouter un peu de latence.",
            () => OnOff(cfg.VSync), _ => cfg.VSync = !cfg.VSync);
        Add("RÉSOLUTION INTERNE", "Multiplie la finesse du rendu (1× = comme la Xbox 360). Plus haut = plus net mais plus lourd.",
            () => cfg.Scale + "×", d => cfg.Scale = ((cfg.Scale - 1 + d + 3) % 3) + 1);
        Add("FILTRAGE ANISOTROPE", "Textures plus nettes vues de biais.",
            () => AnisoNames[cfg.Aniso + 1], d => cfg.Aniso = ((cfg.Aniso + 1 + d + AnisoNames.Length) % AnisoNames.Length) - 1);
        Add("VIBRATION", "Vibration de la manette.", () => OnOff(cfg.Vibration), _ => cfg.Vibration = !cfg.Vibration);
        Add("COUPER LE SON", "Lance le jeu sans son.", () => OnOff(cfg.Mute), _ => cfg.Mute = !cfg.Mute);
        Add("OPTIONS AVANCÉES", "Arguments en plus pour le jeu (facultatif). Entrée pour écrire, Échap pour finir.", null, null, text: true);
        int backIdx = options.Count;
        var back = MakeBar(OptionsPanel, "RETOUR", "Revenir au menu principal.", false, false);
        back.Run = () => ShowScreen(0);
        back.Bar.MouseEnter += (_, _) => { if (screen == 1) SelectOpt(backIdx); };
        back.Bar.MouseLeftButtonUp += (_, _) => { if (screen == 1) ShowScreen(0); };
        options.Add(back);
    }

    // ---- selection styling ---------------------------------------------------
    void Style(Item it, bool on)
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
        it.Value.Foreground = fg;
    }

    void SelectMenu(int i)
    {
        selMenu = Math.Clamp(i, 0, menu.Count - 1);
        for (int k = 0; k < menu.Count; k++) Style(menu[k], k == selMenu);
        InfoText.Text = menu[selMenu].Info;
    }

    void SelectOpt(int i)
    {
        selOpt = Math.Clamp(i, 0, options.Count - 1);
        for (int k = 0; k < options.Count; k++)
        {
            var it = options[k];
            Style(it, k == selOpt);
            if (it.Get != null) it.Value.Text = (k == selOpt ? "◄  " : "") + it.Get() + (k == selOpt ? "  ►" : "");
        }
        if (extraBox != null) extraBox.Foreground = Brushes.White;
        InfoText.Text = options[selOpt].Info;
    }

    void ActivateOpt(Item it, int dir)
    {
        if (it.IsText) { extraBox.Focus(); extraBox.CaretIndex = extraBox.Text.Length; return; }
        if (it.Change == null) { it.Run(); return; }
        it.Change(dir);
        SaveSettings();
        SelectOpt(selOpt);
    }

    void ShowScreen(int s)
    {
        screen = s;
        MenuPanel.Visibility = s == 0 ? Visibility.Visible : Visibility.Collapsed;
        OptionsPanel.Visibility = s == 1 ? Visibility.Visible : Visibility.Collapsed;
        ScreenTitle.Text = s == 0 ? "MENU PRINCIPAL" : "PARAMÈTRES";
        LogoBox.Visibility = s == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (s == 0) SelectMenu(selMenu); else SelectOpt(selOpt);
        Keyboard.ClearFocus();
        Focus();
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
            case Key.Up when screen == 0: SelectMenu(selMenu - 1); break;
            case Key.Down when screen == 0: SelectMenu(selMenu + 1); break;
            case Key.Enter or Key.Space when screen == 0: if (menu[selMenu].Enabled()) menu[selMenu].Run(); break;
            case Key.Up when screen == 1: SelectOpt(selOpt - 1); break;
            case Key.Down when screen == 1: SelectOpt(selOpt + 1); break;
            case Key.Left when screen == 1: ActivateOpt(options[selOpt], -1); break;
            case Key.Right or Key.Enter when screen == 1: ActivateOpt(options[selOpt], 1); break;
            case Key.Escape when screen == 1: ShowScreen(0); break;
            default: return;
        }
        e.Handled = true;
    }

    // ---- readiness -----------------------------------------------------------
    void RefreshChecks()
    {
        Checks.Children.Clear();
        hasExe = File.Exists(exe);
        bool hasGame = File.Exists(Path.Combine(root, "game", "default.xex"));
        var tu = Path.Combine(root, "userdata", "title_update");
        bool hasTu = Directory.Exists(tu) && Directory.EnumerateFileSystemEntries(tu).Any();
        AddCheck("MOTEUR", hasExe);
        AddCheck("DONNÉES DU JEU", hasGame);
        AddCheck("TITLE UPDATE 2", hasTu);
        if (!hasExe) StatusText.Text = "Le moteur n'est pas compilé : lance tools\\build.ps1.";
        UpdatePlay();
    }

    void AddCheck(string name, bool ok)
    {
        var c = ok ? HexC("#3DDC84") : HexC("#FF5A6B");
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 3) };
        row.Children.Add(new TextBlock { Text = name, Foreground = Hex("#B8B8C2"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        row.Children.Add(new Rectangle { Width = 8, Height = 8, Fill = new SolidColorBrush(c), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(45) });
        Checks.Children.Add(row);
    }

    void UpdatePlay()
    {
        var p = menu[0];
        p.Label.Text = proc != null ? "ARRÊTER LE JEU" : "JOUER";
        p.Info = proc != null ? "Le jeu tourne. Force sa fermeture si besoin." : "Lance WWE 2K17 avec tes paramètres.";
        if (screen == 0) SelectMenu(selMenu);
    }

    // ---- launching -------------------------------------------------------------
    void Play()
    {
        if (proc != null) { try { proc.Kill(true); } catch { } return; }
        RefreshChecks();
        if (!hasExe) return;

        var res = cfg.Resolution.Split('x');
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        Directory.CreateDirectory(Path.Combine(root, "userdata"));
        currentLog = Path.Combine(root, "logs", $"run-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var psi = new ProcessStartInfo(exe) { WorkingDirectory = root, UseShellExecute = false };
        void A(string s) => psi.ArgumentList.Add(s);
        A("--game_data_root=" + Path.Combine(root, "game"));
        A("--user_data_root=" + Path.Combine(root, "userdata"));
        A("--update_data_root=" + Path.Combine(root, "userdata", "title_update"));
        A("--fullscreen=" + (cfg.Fullscreen ? "true" : "false"));
        A("--window_width=" + res[0]);
        A("--window_height=" + res[1]);
        A("--log_file=" + currentLog);
        A("--gpu_vsync=" + (cfg.VSync ? "true" : "false"));
        if (cfg.Scale > 1) A("--resolution_scale=" + cfg.Scale);
        if (cfg.Aniso >= 0) A("--anisotropic_override=" + cfg.Aniso);
        A("--vibration=" + (cfg.Vibration ? "true" : "false"));
        if (cfg.Mute) A("--audio_mute=true");
        foreach (var extra in cfg.Extra.Split(' ', StringSplitOptions.RemoveEmptyEntries)) A(extra);

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
                    : $"Le jeu s'est arrêté (code {code}) après {span:mm\\:ss}. Regarde les journaux.";
                UpdatePlay();
            });
            StatusText.Text = $"Jeu lancé (PID {p.Id}). Bon match !";
            UpdatePlay();
        }
        catch (Exception ex)
        {
            proc = null;
            StatusText.Text = "Impossible de lancer le jeu : " + ex.Message;
            UpdatePlay();
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

    void OpenLogs() => OpenDir("logs");

    void OpenDir(string sub)
    {
        var d = Path.Combine(root, sub);
        Directory.CreateDirectory(d);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{d}\"") { UseShellExecute = true });
    }

    void OpenFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"") { UseShellExecute = true });

    void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---- backdrop: smoke, stadium lights, dust ---------------------------------
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
        // two rows of 3 lamps, top-right, like the 2K17 menus
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
        CompositionTarget.Rendering -= OnFrame;
        base.OnClosed(e);
    }
}
