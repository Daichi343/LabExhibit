using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public class LocalTTS : MonoBehaviour, ITTS
{
    // ← フィールドではなくプロパティにする
    public AudioSource audioSource { get; set; }

    public bool  cacheAudio       = true;
    public float defaultDelaySec  = 1f;

    string CacheDir => Path.Combine(Application.streamingAssetsPath, "tts");

    public void Speak(string text, float delaySec = -1f)
    {
        StartCoroutine(CoSpeak(text, delaySec < 0 ? defaultDelaySec : delaySec));
    }

    IEnumerator CoSpeak(string text, float delaySec)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var filePath = Path.Combine(CacheDir, Hash(text) + ".wav");
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[LocalTTS] file not found: {filePath}");
            yield break;
        }
        if (audioSource == null)
        {
            Debug.LogWarning("[LocalTTS] AudioSource 未割当のため再生をスキップします。");
            yield break;
        }

        var uri = "file://" + filePath;
        using var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LocalTTS] 読み込み失敗: {req.error}");
            yield break;
        }

        var clip = DownloadHandlerAudioClip.GetContent(req);
        audioSource.clip = clip;
        if (delaySec > 0f) yield return new WaitForSeconds(delaySec);
        audioSource.Play();
    }

    static string Hash(string s)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash  = md5.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}


// 正常に動いてたバージョン9-26-16:30
// using System.Collections;
// using System.IO;
// using UnityEngine;
// using UnityEngine.Networking;

// [DisallowMultipleComponent]
// public class LocalTTS : MonoBehaviour, ITTS
// {
//     [Header("Playback")]
//     public AudioSource audioSource;       // 再生先
//     public float defaultDelaySec = 1.0f;  // 既定遅延（ページ切替後1秒など）
//     public string folder = "tts";         // StreamingAssets/tts

//     string Root => Path.Combine(Application.streamingAssetsPath, folder);

//     public void Speak(string text, float delaySec = -1f)
//     {
//         if (string.IsNullOrWhiteSpace(text)) return;
//         if (delaySec < 0) delaySec = defaultDelaySec;

//         var file = Path.Combine(Root, $"{Hash(text)}.wav");   // 事前生成したファイル名（ハッシュ）
//         StartCoroutine(CoPlay(file, delaySec));
//     }

//     IEnumerator CoPlay(string filePath, float delaySec)
//     {
//         if (!File.Exists(filePath))
//         {
//             Debug.LogWarning($"[LocalTTS] file not found: {filePath}");
//             yield break;
//         }
//         using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.WAV);
//         yield return req.SendWebRequest();
//         if (req.result != UnityWebRequest.Result.Success) { Debug.LogError(req.error); yield break; }

//         var clip = DownloadHandlerAudioClip.GetContent(req);
//         if (!audioSource) yield break;
//         audioSource.clip = clip;
//         if (delaySec > 0) yield return new WaitForSeconds(delaySec);
//         audioSource.Play();
//     }

//     static string Hash(string s)
//     {
//         using var md5 = System.Security.Cryptography.MD5.Create();
//         var bytes = System.Text.Encoding.UTF8.GetBytes(s);
//         var h = md5.ComputeHash(bytes);
//         var sb = new System.Text.StringBuilder(h.Length * 2);
//         foreach (var b in h) sb.Append(b.ToString("x2"));
//         return sb.ToString();
//     }
// }