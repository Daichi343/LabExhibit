using System;
using System.Collections.Generic;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject idlePanel;
    public GameObject holdHandPanel;
    public GameObject measureReadyPanel;   // 信号6: 計測準備
    public GameObject measuringPanel;      // 信号7: 計測中
    public GameObject successPanel;
    public GameObject failurePanel;
    public GameObject nfcReadPanel;        // ★新規: 読み込み用パネル
    public GameObject nfcWritePanel;
    public GameObject donePanel;

    [Header("Voice")]
    public AudioSource voiceSource;        // 再生先（DebugRunner の AudioSource を割当）
    public bool autoSpeak = true;
    public MonoBehaviour ttsBackend;       // LocalTTS でも TTSAzure でもOK（ITTS実装）
    public float speakDelaySec = 1.0f;

    // ITTS（audioSource プロパティ & Speak() を持つインターフェース）経由で利用
    ITTS TTS => ttsBackend as ITTS;

    [TextArea] public string idleVoice = "待機中です。タグをかざしてね。";
    [TextArea] public string backToIdleVoice = "待機画面に戻ります。";          // 信号0
    [TextArea] public string holdHandVoice = "タグを検出しました。センサーに手をかざしてね。";
    [TextArea] public string measureReadyVoice = "計測の準備をします。";            // 信号6
    [TextArea] public string measuringVoice = "計測を開始します。";              // 信号7
    [TextArea] public string successVoice = "計測成功！";
    [TextArea] public string failureVoice = "エラーが発生しました。もう一度お願いします。";
    [TextArea] public string nfcReadVoice = "タグを読み込みます。";            // ★追加
    [TextArea] public string nfcWriteVoice = "タグに書き込みます。";
    [TextArea] public string doneVoice = "完了しました。";

    public enum Panel
    {
        Idle = 1,
        HoldHand = 2,
        MeasureReady = 100,
        Measuring = 3,
        Success = 4,
        Failure = 5,

        // ★読み書きで別パネルに分離
        NFCRead = 60,
        NFCWrite = 61,

        Done = 7
    }

    Dictionary<Panel, GameObject> _map;

    void Awake()
    {
        _map = new Dictionary<Panel, GameObject>
        {
            { Panel.Idle,         idlePanel },
            { Panel.HoldHand,     holdHandPanel },
            { Panel.MeasureReady, measureReadyPanel },
            { Panel.Measuring,    measuringPanel },
            { Panel.Success,      successPanel },
            { Panel.Failure,      failurePanel },
            { Panel.NFCRead,      nfcReadPanel },   // ★追加
            { Panel.NFCWrite,     nfcWritePanel },
            { Panel.Done,         donePanel },
        };
    }

    /// <summary>
    /// 画面を切替。playVoice=false で音声を抑制（あとで手動再生する時に使う）
    /// </summary>
    public void Show(Panel p, bool playVoice = true, string overrideText = null)
    {
        foreach (var kv in _map)
        {
            if (kv.Value) kv.Value.SetActive(kv.Key == p);
        }

        if (!playVoice) return;

        if (autoSpeak && TTS != null)
        {
            if (voiceSource != null) TTS.audioSource = voiceSource;

            var text = overrideText ?? GetVoiceFor(p);
            if (!string.IsNullOrWhiteSpace(text))
            {
                TTS.Speak(text, speakDelaySec);
            }
        }
    }

    string GetVoiceFor(Panel p)
    {
        switch (p)
        {
            case Panel.Idle: return idleVoice;
            case Panel.HoldHand: return holdHandVoice;
            case Panel.MeasureReady: return measureReadyVoice;
            case Panel.Measuring: return measuringVoice;
            case Panel.Success: return successVoice;
            case Panel.Failure: return failureVoice;
            case Panel.NFCRead: return nfcReadVoice;   // ★追加
            case Panel.NFCWrite: return nfcWriteVoice;
            case Panel.Done: return doneVoice;
            default: return null;
        }
    }

    /// <summary>遅延0秒で即時しゃべる（TTS未設定ならログ）</summary>
    public void SpeakNow(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (TTS != null)
        {
            if (voiceSource != null) TTS.audioSource = voiceSource;
            TTS.Speak(text, 0f);
        }
        else
        {
            Debug.Log($"[Voice NOW] {text}");
        }
    }

#if UNITY_EDITOR
    [Header("Debug Hotkeys (1-8)")]
    public bool debugHotkeys = false;

    void Update()
    {
        if (!debugHotkeys) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) Show(Panel.Idle);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Show(Panel.HoldHand);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Show(Panel.Measuring);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Show(Panel.Success);
        if (Input.GetKeyDown(KeyCode.Alpha5)) Show(Panel.Failure);
        if (Input.GetKeyDown(KeyCode.Alpha6)) Show(Panel.NFCWrite);
        if (Input.GetKeyDown(KeyCode.Alpha7)) Show(Panel.Done);
        if (Input.GetKeyDown(KeyCode.Alpha8)) Show(Panel.NFCRead);  // ★読み込み確認用
    }
#endif
}


// 正常に動いてたバージョン9-26-18:22
// using System;
// using System.Collections.Generic;
// using UnityEngine;

// public class ScreenManager : MonoBehaviour
// {
//     [Header("Panels")]
//     public GameObject idlePanel;
//     public GameObject holdHandPanel;
//     public GameObject measureReadyPanel;   // ← 追加: 信号6用（計測準備）
//     public GameObject measuringPanel;      // 既存: 信号7で使用
//     public GameObject successPanel;
//     public GameObject failurePanel;
//     public GameObject nfcWritePanel;
//     public GameObject donePanel;

//     [Header("Voice")]
//     public AudioSource voiceSource;          // 再生先（DebugRunnerのAudioSourceを割り当て）
//     public bool autoSpeak = true;
//     public MonoBehaviour ttsBackend;         // ← LocalTTS でも TTSAzure でもOK
//     public float speakDelaySec = 1.0f;

//     // ITTS を経由して利用
//     ITTS TTS => ttsBackend as ITTS;

//     [TextArea] public string idleVoice         = "待機中です。タグをかざしてね。";
//     [TextArea] public string backToIdleVoice   = "待機画面に戻ります。";         // ← 追加（信号0）
//     [TextArea] public string holdHandVoice     = "タグを検出しました。センサーに手をかざしてね。";
//     [TextArea] public string measureReadyVoice = "計測の準備をします。";           // ← 追加（信号6）
//     [TextArea] public string measuringVoice    = "計測を開始します。";             // （信号7）
//     [TextArea] public string successVoice      = "計測成功！";
//     [TextArea] public string failureVoice      = "エラーが発生しました。もう一度お願いします。";
//     [TextArea] public string nfcReadVoice      = "タグを読み込みます。";
//     [TextArea] public string nfcWriteVoice     = "タグに書き込みます。";
//     [TextArea] public string doneVoice         = "完了しました。";

//     public enum Panel
//     {
//         Idle = 1, HoldHand = 2, MeasureReady = 100, Measuring = 3, Success = 4,
//         Failure = 5, NFCWrite = 6, Done = 7
//     }

//     Dictionary<Panel, GameObject> _map;

//     void Awake()
//     {
//         _map = new Dictionary<Panel, GameObject>
//         {
//             { Panel.Idle,         idlePanel },
//             { Panel.HoldHand,     holdHandPanel },
//             { Panel.MeasureReady, measureReadyPanel }, // 追加
//             { Panel.Measuring,    measuringPanel },
//             { Panel.Success,      successPanel },
//             { Panel.Failure,      failurePanel },
//             { Panel.NFCWrite,     nfcWritePanel },
//             { Panel.Done,         donePanel },
//         };
//     }

//     /// <summary>
//     /// 画面を切替。playVoice=false で音声を抑制（あとで手動再生する時に使う）
//     /// </summary>
//     public void Show(Panel p, bool playVoice = true, string overrideText = null)
//     {
//         foreach (var kv in _map)
//         {
//             if (kv.Value) kv.Value.SetActive(kv.Key == p);
//         }

//         if (!playVoice) return;

//         if (autoSpeak && TTS != null)
//         {
//             if (voiceSource != null) TTS.audioSource = voiceSource;
//             var text = overrideText ?? GetVoiceFor(p);
//             if (!string.IsNullOrWhiteSpace(text))
//             {
//                 TTS.Speak(text, speakDelaySec);
//             }
//         }
//     }

//     string GetVoiceFor(Panel p)
//     {
//         switch (p)
//         {
//             case Panel.Idle:         return idleVoice;
//             case Panel.HoldHand:     return holdHandVoice;
//             case Panel.MeasureReady: return measureReadyVoice;   // 追加
//             case Panel.Measuring:    return measuringVoice;
//             case Panel.Success:      return successVoice;
//             case Panel.Failure:      return failureVoice;
//             case Panel.NFCWrite:     return nfcWriteVoice;
//             case Panel.Done:         return doneVoice;
//             default:                 return null;
//         }
//     }

//     /// <summary>遅延0秒で即時しゃべる（TTS未設定ならログ）</summary>
//     public void SpeakNow(string text)
//     {
//         if (string.IsNullOrWhiteSpace(text)) return;

//         if (TTS != null)
//         {
//             if (voiceSource != null) TTS.audioSource = voiceSource;
//             TTS.Speak(text, 0f);
//         }
//         else
//         {
//             Debug.Log($"[Voice NOW] {text}");
//         }
//     }

// #if UNITY_EDITOR
//     [Header("Debug Hotkeys (1-7)")]
//     public bool debugHotkeys = false;

//     void Update()
//     {
//         if (!debugHotkeys) return;

//         if (Input.GetKeyDown(KeyCode.Alpha1)) Show(Panel.Idle);
//         if (Input.GetKeyDown(KeyCode.Alpha2)) Show(Panel.HoldHand);
//         if (Input.GetKeyDown(KeyCode.Alpha3)) Show(Panel.Measuring);
//         if (Input.GetKeyDown(KeyCode.Alpha4)) Show(Panel.Success);
//         if (Input.GetKeyDown(KeyCode.Alpha5)) Show(Panel.Failure);
//         if (Input.GetKeyDown(KeyCode.Alpha6)) Show(Panel.NFCWrite);
//         if (Input.GetKeyDown(KeyCode.Alpha7)) Show(Panel.Done);
//     }
// #endif
// }

// 正常に動いてたバージョン9-26-16:30
// using System;
// using System.Collections.Generic;
// using UnityEngine;

// public class ScreenManager : MonoBehaviour
// {
//     [Header("Panels")]
//     public GameObject idlePanel;
//     public GameObject holdHandPanel;
//     public GameObject measuringPanel;
//     public GameObject successPanel;
//     public GameObject failurePanel;
//     public GameObject nfcWritePanel;
//     public GameObject donePanel;

//     [Header("Voice")]
//     public AudioSource voiceSource;          // 再生先（DebugRunner の AudioSource を割当）
//     public bool autoSpeak = true;
//     public float speakDelaySec = 1.0f;

//     // ← ここがポイント：どのTTS実装でも刺せるように MonoBehaviour で受ける
//     public MonoBehaviour ttsBackend;         // LocalTTS でも TTSAzure でも入れられる
//     ITTS TTS => ttsBackend as ITTS;          // 使う時はこのプロパティ経由で呼ぶ

//     [TextArea] public string idleVoice       = "待機中です。タグをかざしてね。";
//     [TextArea] public string holdHandVoice   = "タグを検出しました。センサーに手をかざしてね。";
//     [TextArea] public string measuringVoice  = "計測を開始します。";
//     [TextArea] public string successVoice    = "計測成功！";
//     [TextArea] public string failureVoice    = "エラーが発生しました。もう一度お願いします。";
//     [TextArea] public string nfcReadVoice    = "タグを読み込みます。";
//     [TextArea] public string nfcWriteVoice   = "タグに書き込みます。";
//     [TextArea] public string doneVoice       = "完了しました。";

//     public enum Panel { Idle = 1, HoldHand = 2, Measuring = 3, Success = 4, Failure = 5, NFCWrite = 6, Done = 7 }

//     Dictionary<Panel, GameObject> _map;

//     void Awake()
//     {
//         _map = new Dictionary<Panel, GameObject>
//         {
//             { Panel.Idle,      idlePanel },
//             { Panel.HoldHand,  holdHandPanel },
//             { Panel.Measuring, measuringPanel },
//             { Panel.Success,   successPanel },
//             { Panel.Failure,   failurePanel },
//             { Panel.NFCWrite,  nfcWritePanel },
//             { Panel.Done,      donePanel },
//         };
//     }

//     public void Show(Panel p)
//     {
//         // 表示切り替え
//         foreach (var kv in _map)
//         {
//             if (kv.Value) kv.Value.SetActive(kv.Key == p);
//         }

//         // 自動読み上げ
//         if (!autoSpeak) return;

//         // AudioSource を実装側に同期（audioSource フィールドがあれば自動で差し込む）
//         SyncAudioToBackend();

//         var tts = TTS;
//         if (tts == null) return;

//         string text = GetVoiceFor(p);
//         if (!string.IsNullOrWhiteSpace(text))
//         {
//             tts.Speak(text, speakDelaySec);
//         }
//     }

//     string GetVoiceFor(Panel p)
//     {
//         switch (p)
//         {
//             case Panel.Idle:      return idleVoice;
//             case Panel.HoldHand:  return holdHandVoice;
//             case Panel.Measuring: return measuringVoice;
//             case Panel.Success:   return successVoice;
//             case Panel.Failure:   return failureVoice;
//             case Panel.NFCWrite:  return nfcWriteVoice;
//             case Panel.Done:      return doneVoice;
//             default:              return null;
//         }
//     }

//     /// <summary>直ちに読み上げ（遅延0秒）。任意の場面で呼べます。</summary>
//     public void SpeakNow(string text)
//     {
//         if (string.IsNullOrWhiteSpace(text)) return;

//         SyncAudioToBackend();

//         var tts = TTS;
//         if (tts != null)
//         {
//             tts.Speak(text, 0f);
//         }
//         else
//         {
//             Debug.Log($"[Voice NOW] {text}");
//         }
//     }

//     /// <summary>
//     /// ttsBackend に public AudioSource audioSource フィールドがある場合、
//     /// そこへ voiceSource を差し込みます（実装クラスに依存しない安全な方法）。
//     /// </summary>
//     void SyncAudioToBackend()
//     {
//         if (ttsBackend == null || voiceSource == null) return;

//         var f = ttsBackend.GetType().GetField("audioSource");
//         if (f != null && f.FieldType == typeof(AudioSource))
//         {
//             f.SetValue(ttsBackend, voiceSource);
//         }
//     }

// #if UNITY_EDITOR
//     [Header("Debug Hotkeys (1-7)")]
//     public bool debugHotkeys = false;

//     void Update()
//     {
//         if (!debugHotkeys) return;

//         if (Input.GetKeyDown(KeyCode.Alpha1)) Show(Panel.Idle);
//         if (Input.GetKeyDown(KeyCode.Alpha2)) Show(Panel.HoldHand);
//         if (Input.GetKeyDown(KeyCode.Alpha3)) Show(Panel.Measuring);
//         if (Input.GetKeyDown(KeyCode.Alpha4)) Show(Panel.Success);
//         if (Input.GetKeyDown(KeyCode.Alpha5)) Show(Panel.Failure);
//         if (Input.GetKeyDown(KeyCode.Alpha6)) Show(Panel.NFCWrite);
//         if (Input.GetKeyDown(KeyCode.Alpha7)) Show(Panel.Done);
//     }
// #endif
// }



// AzureTTS対応後 正常に動いていたバージョン9-25-18:10
// using System;
// using System.Collections.Generic;
// using UnityEngine;

// public class ScreenManager : MonoBehaviour
// {
//     [Header("Panels")]
//     public GameObject idlePanel;
//     public GameObject holdHandPanel;
//     public GameObject measuringPanel;
//     public GameObject successPanel;
//     public GameObject failurePanel;
//     public GameObject nfcWritePanel;
//     public GameObject donePanel;

//     [Header("Voice")]
//     public AudioSource voiceSource;          // ← DebugRunner の AudioSource を割当
//     public bool autoSpeak = true;
//     public TTSAzure tts;                     // ← DebugRunner の TTSAzure を割当
//     public float speakDelaySec = 1.0f;

//     [TextArea] public string idleVoice       = "待機中です。タグをかざしてね。";
//     [TextArea] public string holdHandVoice   = "タグを検出しました。センサーに手をかざしてね。";
//     [TextArea] public string measuringVoice  = "計測を開始します。";
//     [TextArea] public string successVoice    = "計測成功！";
//     [TextArea] public string failureVoice    = "エラーが発生しました。もう一度お願いします。";
//     [TextArea] public string nfcReadVoice    = "タグを読み込みます。";
//     [TextArea] public string nfcWriteVoice   = "タグに書き込みます。";
//     [TextArea] public string doneVoice       = "完了しました。";

//     public enum Panel { Idle = 1, HoldHand = 2, Measuring = 3, Success = 4, Failure = 5, NFCWrite = 6, Done = 7 }

//     Dictionary<Panel, GameObject> _map;

//     void Awake()
//     {
//         _map = new Dictionary<Panel, GameObject>
//         {
//             { Panel.Idle,      idlePanel },
//             { Panel.HoldHand,  holdHandPanel },
//             { Panel.Measuring, measuringPanel },
//             { Panel.Success,   successPanel },
//             { Panel.Failure,   failurePanel },
//             { Panel.NFCWrite,  nfcWritePanel },
//             { Panel.Done,      donePanel },
//         };
//     }

//     public void Show(Panel p)
//     {
//         foreach (var kv in _map)
//         {
//             if (kv.Value) kv.Value.SetActive(kv.Key == p);
//         }

//         if (!autoSpeak || tts == null) return;

//         // 再生先を毎回同期
//         if (voiceSource != null) tts.audioSource = voiceSource;

//         string text = GetVoiceFor(p);
//         if (!string.IsNullOrWhiteSpace(text))
//         {
//             tts.Speak(text, speakDelaySec);
//         }
//     }

//     string GetVoiceFor(Panel p)
//     {
//         switch (p)
//         {
//             case Panel.Idle:      return idleVoice;
//             case Panel.HoldHand:  return holdHandVoice;
//             case Panel.Measuring: return measuringVoice;
//             case Panel.Success:   return successVoice;
//             case Panel.Failure:   return failureVoice;
//             case Panel.NFCWrite:  return nfcWriteVoice;
//             case Panel.Done:      return doneVoice;
//             default:              return null;
//         }
//     }

//         // 直ちにしゃべる（遅延0秒）
//     public void SpeakNow(string text)
//     {
//         if (string.IsNullOrWhiteSpace(text)) return;

//         if (tts != null)
//         {
//             // TTSAzure の Speak(text, delaySec)。0f で即時再生。
//             tts.Speak(text, 0f);
//         }
//         else
//         {
//             // TTS 未設定時はログだけ
//             Debug.Log($"[Voice NOW] {text}");
//         }
//     }

// #if UNITY_EDITOR
//     [Header("Debug Hotkeys (1-7)")]
//     public bool debugHotkeys = false;

//     void Update()
//     {
//         if (!debugHotkeys) return;

//         if (Input.GetKeyDown(KeyCode.Alpha1)) Show(Panel.Idle);
//         if (Input.GetKeyDown(KeyCode.Alpha2)) Show(Panel.HoldHand);
//         if (Input.GetKeyDown(KeyCode.Alpha3)) Show(Panel.Measuring);
//         if (Input.GetKeyDown(KeyCode.Alpha4)) Show(Panel.Success);
//         if (Input.GetKeyDown(KeyCode.Alpha5)) Show(Panel.Failure);
//         if (Input.GetKeyDown(KeyCode.Alpha6)) Show(Panel.NFCWrite);
//         if (Input.GetKeyDown(KeyCode.Alpha7)) Show(Panel.Done);
//     }
// #endif
// }

// AzureTTS対応前 正常に動いていたバージョン9-25-15:26
// using UnityEngine;
// using System.Collections;

// public class ScreenManager : MonoBehaviour
// {
//     public enum Panel { Idle, HoldHand, Measuring, Success, Failure, NFCWrite, Done }

//     [Header("Panels")]
//     public GameObject idlePanel;
//     public GameObject holdHandPanel;
//     public GameObject measuringPanel;
//     public GameObject successPanel;
//     public GameObject failurePanel;
//     public GameObject nfcWritePanel;
//     public GameObject donePanel;

//     [Header("Voice")]
//     public AudioSource voiceSource;
//     public bool autoSpeak = true;
//     public TTSAzure tts; // なくてもOK（未設定ならログだけ）

//     [TextArea] public string idleVoice      = "待機中です。タグをかざしてね。";
//     [TextArea] public string holdHandVoice  = "タグを検出しました。センサーに手をかざしてね。";
//     [TextArea] public string measuringVoice = "計測を開始します。";
//     [TextArea] public string successVoice   = "計測成功！";
//     [TextArea] public string failureVoice   = "エラーが発生しました。もう一度お願いします。";
//     [TextArea] public string nfcReadVoice   = "タグを読み込みます。";
//     [TextArea] public string nfcWriteVoice  = "タグに書き込みます。";
//     [TextArea] public string doneVoice      = "完了しました。";

//     [Header("Debug Hotkeys (1-7)")]
//     public bool debugHotkeys = true;

//     void Awake()
//     {
//         // 起動時は Idle を表示（好みで変更可）
//         Show(Panel.Idle, speak:false);
//     }

//     void SetAll(bool on)
//     {
//         idlePanel?.SetActive(on);
//         holdHandPanel?.SetActive(on);
//         measuringPanel?.SetActive(on);
//         successPanel?.SetActive(on);
//         failurePanel?.SetActive(on);
//         nfcWritePanel?.SetActive(on);
//         donePanel?.SetActive(on);
//     }

//     public void Show(Panel p, bool speak = true)
//     {
//         SetAll(false);
//         switch (p)
//         {
//             case Panel.Idle:
//                 idlePanel?.SetActive(true);
//                 if (speak) Speak(idleVoice);
//                 break;

//             case Panel.HoldHand:
//                 holdHandPanel?.SetActive(true);
//                 if (speak) Speak(holdHandVoice);
//                 break;

//             case Panel.Measuring:
//                 measuringPanel?.SetActive(true);
//                 if (speak) Speak(measuringVoice);
//                 break;

//             case Panel.Success:
//                 successPanel?.SetActive(true);
//                 if (speak) Speak(successVoice);
//                 break;

//             case Panel.Failure:
//                 failurePanel?.SetActive(true);
//                 if (speak) Speak(failureVoice);
//                 break;

//             case Panel.NFCWrite:
//                 nfcWritePanel?.SetActive(true);
//                 // 読み込み中にも使い回し可：文面は呼び出し側で切り替え
//                 // ここではデフォルトで書き込み文言
//                 if (speak) Speak(nfcWriteVoice);
//                 break;

//             case Panel.Done:
//                 donePanel?.SetActive(true);
//                 if (speak) Speak(doneVoice);
//                 break;
//         }
//     }

//     public void Speak(string text)
//     {
//         if (!autoSpeak || string.IsNullOrWhiteSpace(text))
//         {
//             Debug.Log($"[Voice TEXT] {text}");
//             return;
//         }

//         if (tts != null && tts.enabled && voiceSource != null)
//         {
//             StartCoroutine(tts.SpeakToAudioSource(text, voiceSource));
//         }
//         else
//         {
//             // TTS未設定時はログだけ
//             Debug.Log($"[Voice TEXT] {text}");
//         }
//     }

//     void Update()
//     {
//         if (!debugHotkeys || !Application.isPlaying) return;

//         if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Show(Panel.Idle);
//         else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Show(Panel.HoldHand);
//         else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Show(Panel.Measuring);
//         else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) Show(Panel.Success);
//         else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) Show(Panel.Failure);
//         else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) Show(Panel.NFCWrite);
//         else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) Show(Panel.Done);
//     }
// }



// Arudinoの最初のコード用
// using UnityEngine;

// public class ScreenManager : MonoBehaviour
// {
//     public GameObject idlePanel;
//     public GameObject holdHandPanel;
//     public GameObject measuringPanel;
//     public GameObject successPanel;
//     public GameObject failurePanel;
//     public GameObject nfcWritePanel;
//     public GameObject donePanel;

//     GameObject[] _all;

//     public enum Panel { Idle, HoldHand, Measuring, Success, Failure, NFCWrite, Done }

//     void Awake()
//     {
//         _all = new[] { idlePanel, holdHandPanel, measuringPanel, successPanel, failurePanel, nfcWritePanel, donePanel };
//         Show(Panel.Idle);
//     }

//     public void Show(Panel p)
//     {
//         for (int i = 0; i < _all.Length; i++)
//             if (_all[i]) _all[i].SetActive(i == (int)p);
//     }

//     // キーボード(1〜7)テスト用はそのままでOK（省略）

//     /// <summary> Arduinoから来るコード(1..15)をUIパネルに割り当て </summary>
//     public void ShowByCode(int code)
//     {
//         switch (code)
//         {
//             // --- センシング周り ---
//             case 6:               Show(Panel.HoldHand);    break; // 生体情報センシング開始：手をかざして
//             case 7:               Show(Panel.Measuring);   break; // 指を検出：計測中
//             case 8:               Show(Panel.Failure);     break; // センシングタイムアウト
//             case 9:               Show(Panel.Success);     break; // 計測完了

//             // --- NFC 読み/書き中はまとめて「NFCWrite」へ ---
//             case 1: case 2: case 10: case 12:
//                                Show(Panel.NFCWrite);   break;

//             // --- エラー類は Failure へ ---
//             case 3: case 4: case 5: case 11: case 13: case 14:
//                                Show(Panel.Failure);    break;

//             // --- 書き込み成功 ---
//             case 15:              Show(Panel.Done);        break;

//             default:              Show(Panel.Idle);        break;
//         }
//         Debug.Log($"[ScreenManager] code={code}");
//     }
// }

// キーボード入力確認用
// using UnityEngine;

// public class ScreenManager : MonoBehaviour
// {
//     // 1〜7 をそのまま割り当て（Arduinoのコードと合わせやすいように）
//     public enum Panel
//     {
//         None     = 0,
//         Idle     = 1,
//         HoldHand = 2,
//         Measuring= 3,
//         Success  = 4,
//         Failure  = 5,
//         NFCWrite = 6,
//         Done     = 7
//     }

//     [Header("Panels")]
//     public GameObject IdlePanel;
//     public GameObject HoldHandPanel;
//     public GameObject MeasuringPanel;
//     public GameObject SuccessPanel;
//     public GameObject FailurePanel;
//     public GameObject NFCWritePanel;
//     public GameObject DonePanel;

//     private GameObject[] _map;

//     void Awake()
//     {
//         _map = new GameObject[8]; // 0は未使用
//         _map[(int)Panel.Idle]      = IdlePanel;
//         _map[(int)Panel.HoldHand]  = HoldHandPanel;
//         _map[(int)Panel.Measuring] = MeasuringPanel;
//         _map[(int)Panel.Success]   = SuccessPanel;
//         _map[(int)Panel.Failure]   = FailurePanel;
//         _map[(int)Panel.NFCWrite]  = NFCWritePanel;
//         _map[(int)Panel.Done]      = DonePanel;
//     }

//     public void Show(Panel panel)
//     {
//         // いったん全部OFF
//         for (int i = 1; i < _map.Length; i++)
//         {
//             if (_map[i]) _map[i].SetActive(false);
//         }
//         // 指定だけON
//         if (panel != Panel.None && _map[(int)panel])
//         {
//             _map[(int)panel].SetActive(true);
//         }
//     }

//     // 便利版（1〜7のintをそのまま渡せる）
//     public void Show(int code)
//     {
//         if (System.Enum.IsDefined(typeof(Panel), code))
//             Show((Panel)code);
//         else
//             Debug.Log($"[ScreenManager] Unknown panel code: {code}");
//     }
// }