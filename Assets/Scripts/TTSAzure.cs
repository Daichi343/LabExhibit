using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public class TTSAzure : MonoBehaviour
{
    [Header("Azure Speech")]
    [Tooltip("例: japaneast / eastus など")]
    public string region = "japaneast";

    [Tooltip("Azure の Speech キー")]
    public string subscriptionKey = "";   // 空のままでも動作（生成はスキップしログのみ）

    [Tooltip("例: ja-JP-NanamiNeural / ja-JP-AoiNeural 等")]
    public string voice = "ja-JP-NanamiNeural";

    // 出力は mp3 推奨（後段の AudioClip も MPEG を想定）
    const string outputFormat = "audio-16khz-128kbitrate-mono-mp3";

    [Header("Playback")]
    // public AudioSource audioSource;       // 再生先 変更前
    public AudioSource audioSource { get; set; }   // 変更後
    public bool cacheAudio = true;        // 同じテキストは再利用
    public float defaultDelaySec = 1.0f;  // 画面切替後の待ち時間

    string Endpoint => $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";

    string CacheDir
    {
        get
        {
            var dir = Path.Combine(Application.persistentDataPath, "tts_cache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// テキストをTTSして保存し、delay後に再生
    /// </summary>
    public void Speak(string text, float delaySec = -1f)
    {
        if (delaySec < 0) delaySec = defaultDelaySec;
        StartCoroutine(CoSpeak(text, delaySec));
    }

    IEnumerator CoSpeak(string text, float delaySec)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        string filePath = Path.Combine(CacheDir, Hash(text, voice, outputFormat) + ".mp3");

        // 生成 or キャッシュ利用
        if (!File.Exists(filePath))
        {
            if (string.IsNullOrEmpty(subscriptionKey))
            {
                Debug.LogWarning("[TTS] Azureのキー未設定のため音声生成をスキップします（キーを設定すると保存・再生されます）。");
                yield break;
            }

            // SSML を作成
            string ssml =
$@"<speak version=""1.0"" xml:lang=""ja-JP"">
  <voice name=""{voice}"">
    <prosody rate=""+0%"" pitch=""+0%"">{EscapeXml(text)}</prosody>
  </voice>
</speak>";

            using (var req = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] body = Encoding.UTF8.GetBytes(ssml);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/ssml+xml");
                req.SetRequestHeader("X-Microsoft-OutputFormat", outputFormat);
                req.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[TTS] 生成失敗: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                try
                {
                    File.WriteAllBytes(filePath, req.downloadHandler.data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TTS] 保存失敗: {e.Message}");
                    yield break;
                }
            }
        }

        // mp3 を読み込んで再生
        if (!audioSource)
        {
            Debug.LogWarning("[TTS] AudioSource 未割り当てのため再生をスキップします。");
            yield break;
        }

        // file:/// URL に変換（OS差異を吸収）
        var uri = new Uri(filePath);
        using (var dl = UnityWebRequestMultimedia.GetAudioClip(uri.AbsoluteUri, AudioType.MPEG))
        {
            yield return dl.SendWebRequest();

            if (dl.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[TTS] 再生用読み込み失敗: {dl.error}");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(dl);
            audioSource.clip = clip;

            if (delaySec > 0) yield return new WaitForSeconds(delaySec);
            audioSource.Play();
        }
    }

    // ===== helpers =====
    static string Hash(params string[] parts)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(string.Join("|", parts));
        var hash = md5.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    static string EscapeXml(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

// 試作用 正常に動いていたバージョン9-25-15:26
// using UnityEngine;
// using UnityEngine.Networking;
// using System.Text;
// using System.IO;
// using System.Collections;

// public class TTSAzure : MonoBehaviour
// {
//     public enum Backend { None, Azure }
//     public Backend backend = Backend.Azure;

//     [Header("Azure Speech")]
//     public string subscriptionKey = "";          // ★あなたのキー
//     public string region = "japaneast";          // ★あなたのリージョン
//     public string voice = "ja-JP-NanamiNeural";  // 好みの日本語音声
//     public string outputFormat = "audio-16khz-128kbitrate-mono-mp3";

//     public IEnumerator SpeakToAudioSource(string text, AudioSource target)
//     {
//         if (backend != Backend.Azure || string.IsNullOrEmpty(subscriptionKey) || string.IsNullOrEmpty(region))
//         {
//             Debug.Log($"[TTS] {text}");
//             yield break;
//         }

//         var ssml = $@"<speak version='1.0' xml:lang='ja-JP'>
//   <voice name='{voice}'>{System.Security.SecurityElement.Escape(text)}</voice>
// </speak>";

//         var url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
//         var body = Encoding.UTF8.GetBytes(ssml);

//         using (var req = new UnityWebRequest(url, "POST"))
//         {
//             req.uploadHandler = new UploadHandlerRaw(body);
//             req.downloadHandler = new DownloadHandlerBuffer();
//             req.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);
//             req.SetRequestHeader("Content-Type", "application/ssml+xml");
//             req.SetRequestHeader("X-Microsoft-OutputFormat", outputFormat);
//             req.SetRequestHeader("User-Agent", "Unity-TTS");

//             yield return req.SendWebRequest();

//             if (req.result != UnityWebRequest.Result.Success)
//             {
//                 Debug.LogError($"[TTS] request failed: {req.error}");
//                 yield break;
//             }

//             var bytes = req.downloadHandler.data;
//             var path = Path.Combine(Application.temporaryCachePath, "tts_temp.mp3");
//             File.WriteAllBytes(path, bytes);

//             using (var load = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG))
//             {
//                 yield return load.SendWebRequest();
//                 if (load.result != UnityWebRequest.Result.Success)
//                 {
//                     Debug.LogError($"[TTS] load mp3 failed: {load.error}");
//                     yield break;
//                 }
//                 var clip = DownloadHandlerAudioClip.GetContent(load);
//                 target.clip = clip;
//                 target.Play();
//             }
//         }
//     }
// }