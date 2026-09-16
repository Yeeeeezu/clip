using System.Runtime.InteropServices;
using System.Text;

namespace Clip;

// clipboard read/write/history from the cli.
// history stored in %APPDATA%\clip\history.txt (plain text, one entry per line, base64 encoded)

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static class Program
{
    static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "clip");
    static readonly string HistoryPath = Path.Combine(HistoryDir, "history.txt");
    const int MaxHistory = 100;

    static void Main(string[] args)
    {
        if (args.Length == 0) { ReadClip(); return; }

        switch (args[0])
        {
            case "get" or "read":
                ReadClip();
                break;
            case "set" or "write" or "copy":
                if (args.Length < 2)
                {
                    // read from stdin
                    string stdin = Console.In.ReadToEnd();
                    WriteClip(stdin.TrimEnd('\n', '\r'));
                }
                else
                    WriteClip(string.Join(" ", args[1..]));
                break;
            case "ls" or "list" or "history":
                ShowHistory(args.Length > 1 && int.TryParse(args[1], out int n) ? n : 20);
                break;
            case "clear":
                ClearHistory();
                break;
            default:
                // treat as value to copy
                WriteClip(string.Join(" ", args));
                break;
        }
    }

    static void ReadClip()
    {
        string text = GetClipboardText();
        if (string.IsNullOrEmpty(text)) { Console.Error.WriteLine(Dim("  (clipboard is empty)")); return; }
        Console.Write(text);
        if (!text.EndsWith('\n')) Console.WriteLine();
    }

    static void WriteClip(string text)
    {
        SetClipboardText(text);
        AppendHistory(text);
        Console.Error.WriteLine($"  {Green("✓")} copied ({text.Length} chars)");
    }

    static void ShowHistory(int limit)
    {
        var entries = LoadHistory();
        if (entries.Count == 0) { Console.WriteLine(Dim("  (no history)")); return; }

        var recent = entries.TakeLast(limit).Reverse().ToList();
        Console.WriteLine();
        for (int i = 0; i < recent.Count; i++)
        {
            string preview = recent[i].Length > 80
                ? recent[i][..77].Replace('\n', ' ') + "..."
                : recent[i].Replace('\n', ' ');
            Console.WriteLine($"  {Dim($"{i + 1,3}.")} {preview}");
        }
        Console.WriteLine($"\n  {Dim($"{recent.Count} of {entries.Count} entries shown")}");
    }

    static void ClearHistory()
    {
        Console.Write("  type 'yes' to clear clipboard history: ");
        string? input = Console.ReadLine();
        if (input?.Trim().ToLower() == "yes")
        {
            if (File.Exists(HistoryPath)) File.Delete(HistoryPath);
            Console.Error.WriteLine($"  {Green("✓")} history cleared");
        }
        else Console.Error.WriteLine(Dim("  aborted"));
    }

    // ---- history file ----

    static List<string> LoadHistory()
    {
        if (!File.Exists(HistoryPath)) return new();
        var result = new List<string>();
        foreach (var line in File.ReadLines(HistoryPath))
        {
            try { result.Add(Encoding.UTF8.GetString(Convert.FromBase64String(line))); }
            catch { }
        }
        return result;
    }

    static void AppendHistory(string text)
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            var existing = LoadHistory();
            existing.RemoveAll(e => e == text); // dedup
            existing.Add(text);
            if (existing.Count > MaxHistory) existing = existing[^MaxHistory..];
            File.WriteAllLines(HistoryPath, existing.Select(e => Convert.ToBase64String(Encoding.UTF8.GetBytes(e))));
        }
        catch { }
    }

    // ---- win32 clipboard (STA required) ----

    static string GetClipboardText()
    {
        string result = "";
        var thread = new Thread(() =>
        {
            try
            {
                if (OpenClipboard(nint.Zero))
                {
                    nint h = GetClipboardData(13); // CF_UNICODETEXT
                    if (h != nint.Zero)
                    {
                        nint ptr = GlobalLock(h);
                        if (ptr != nint.Zero)
                        {
                            result = Marshal.PtrToStringUni(ptr) ?? "";
                            GlobalUnlock(h);
                        }
                    }
                    CloseClipboard();
                }
            }
            catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    static void SetClipboardText(string text)
    {
        var thread = new Thread(() =>
        {
            try
            {
                if (OpenClipboard(nint.Zero))
                {
                    EmptyClipboard();
                    int size = (text.Length + 1) * 2;
                    nint h = GlobalAlloc(0x0042, (nuint)size); // GMEM_MOVEABLE | GMEM_ZEROINIT
                    nint ptr = GlobalLock(h);
                    Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                    GlobalUnlock(h);
                    SetClipboardData(13, h);
                    CloseClipboard();
                }
            }
            catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [DllImport("user32")] static extern bool OpenClipboard(nint hWnd);
    [DllImport("user32")] static extern bool CloseClipboard();
    [DllImport("user32")] static extern bool EmptyClipboard();
    [DllImport("user32")] static extern nint GetClipboardData(uint uFormat);
    [DllImport("user32")] static extern nint SetClipboardData(uint uFormat, nint hMem);
    [DllImport("kernel32")] static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);
    [DllImport("kernel32")] static extern nint GlobalLock(nint hMem);
    [DllImport("kernel32")] static extern bool GlobalUnlock(nint hMem);

    static string Green(string s) => $"\x1b[32m{s}\x1b[0m";
    static string Dim(string s) => $"\x1b[2m{s}\x1b[0m";
}
