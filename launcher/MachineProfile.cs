using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Launcher;

// What the launcher knows about the machine, and the settings it proposes for a Steam Deck or a small PC
// (same idea as the WWE '12 launcher). A normal PC gets no proposal.
public sealed record MachineInfo(bool Deck, bool Wine, string Gpu, bool Integrated, long VramMb, int Cores, double RamGb);

public sealed record Profile(string Kind, int Internal, bool Fullscreen, int Score);

public static class MachineProfile
{
    static readonly string[] DeckNames = { "vangogh", "van gogh", "custom gpu 0405", "custom gpu 0932", "steam deck" };

    public static bool IsDeckName(string name)
    {
        string n = name.ToLowerInvariant();
        return DeckNames.Any(n.Contains);
    }

    // Integrated GPUs are recognised by name (Windows does not say it directly).
    public static bool IsIntegrated(string name)
    {
        string n = name.ToLowerInvariant();
        if (IsDeckName(n) || n.Contains("custom gpu")) return true;
        if (n.Contains("intel")) return !Regex.IsMatch(n, @"arc\s*(pro\s*)?[ab]\d");
        if (n.Contains("radeon") || n.Contains("amd"))
        {
            if (Regex.IsMatch(n, @"\brx\b|vega\s*(56|64|frontier|vii)|radeon\s+pro\s+w")) return false;
            return Regex.IsMatch(n, @"graphics\s*$|vega\s*\d+|radeon\s*(\(tm\)\s*)?\d{3}m|radeon\s*(\(tm\)\s*)?r[2-7]\b");
        }
        return false;
    }

    public static bool IsWine()
    {
        try
        {
            return NativeLibrary.TryGetExport(NativeLibrary.Load("ntdll.dll"), "wine_get_version", out _);
        }
        catch { return false; }
    }

    // Dedicated video memory of the named card, from the display driver's registry entry (the WMI value stops at 4 GB).
    // -1 when it can't be read.
    public static long VideoMemoryMb(string gpuName)
    {
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls == null) return -1;
            foreach (var sub in cls.GetSubKeyNames())
            {
                using var k = cls.OpenSubKey(sub);
                if (k?.GetValue("DriverDesc") is not string desc || !desc.Equals(gpuName, StringComparison.OrdinalIgnoreCase)) continue;
                object? v = k.GetValue("HardwareInformation.qwMemorySize");
                long bytes = v switch
                {
                    long l => l,
                    byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                    _ => -1,
                };
                if (bytes > 0) return bytes >> 20;
            }
        }
        catch { }
        return -1;
    }

    // null = a normal PC, nothing to propose.
    public static Profile? Choose(MachineInfo m)
    {
        if (m.Deck) return new Profile("deck", 720, true, 0);
        int score = (m.Integrated ? 2 : 0)
                  + (!m.Integrated && m.VramMb >= 0 && m.VramMb < 3584 ? 2 : 0)
                  + (m.Cores > 0 && m.Cores <= 4 ? 1 : 0)
                  + (m.RamGb <= 8 ? 1 : 0);
        if (score < 2) return null;
        int internalHeight = score >= 5 || m.RamGb <= 4 ? 480 : score >= 3 ? 540 : 720;
        return new Profile("small", internalHeight, false, score);
    }
}
