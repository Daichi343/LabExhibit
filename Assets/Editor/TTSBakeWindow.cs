#if UNITY_EDITOR
using UnityEditor;                  // ← Editor API
using UnityEngine;                  // ← Application, Object など
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;           // ← Process を使う（Debug は使わない）

// Debug の衝突を避けるため、Unity の Debug に別名を付けて使います
using UDebug = UnityEngine.Debug;

public static class TTSBakeWindow
{
    [MenuItem("Tools/TTS/シーンのセリフ一括生成（macOS say）")]
    public static void Bake()
    {
#if UNITY_EDITOR_OSX
        var sm = Object.FindFirstObjectByType<ScreenManager>();
        if (sm == null) { UDebug.LogError("ScreenManager が見つかりません。"); return; }

        // シーン内のセリフを列挙
        var lines = new[]
        {
            sm.idleVoice, sm.holdHandVoice, sm.measuringVoice,
            sm.successVoice, sm.failureVoice, sm.nfcReadVoice,
            sm.nfcWriteVoice, sm.doneVoice
        };

        // 出力先（StreamingAssets/tts）
        var outDir = Path.Combine(Application.streamingAssetsPath, "tts");
        Directory.CreateDirectory(outDir);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var hash = Hash(line);
            var aiff = Path.Combine(outDir, hash + ".aiff");
            var wav  = Path.Combine(outDir, hash + ".wav");

            // 1) say で AIFF を作成（日本語は Nanami など適宜変更可）
            Run("/usr/bin/say", $"-v \"Kyoko\" -o \"{aiff}\" \"{EscapeCli(line)}\"");
            // 2) afconvert で 16kHz モノラルの WAV に変換
            Run("/usr/bin/afconvert", $"-f WAVE -d LEI16 \"{aiff}\" \"{wav}\"");
            if (File.Exists(aiff)) File.Delete(aiff);

            UDebug.Log($"[Bake] \"{line}\" -> {Path.GetFileName(wav)}");
        }

        AssetDatabase.Refresh();
#else
        UDebug.LogWarning("このメニューは macOS 専用です。");
#endif
    }

#if UNITY_EDITOR_OSX
    // 外部コマンド実行（say / afconvert）
    static void Run(string exe, string args)
    {
        var p = new Process();
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
        p.WaitForExit();

        if (p.ExitCode != 0)
            UDebug.LogWarning($"cmd error: {exe} {args}\n{p.StandardError.ReadToEnd()}");
    }
#endif

    // 文字列→MD5（ファイル名に使用）
    static string Hash(string s)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = md5.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    // コマンド引数用の簡易エスケープ
    static string EscapeCli(string s) => s.Replace("\"", "\\\"");
}
#endif