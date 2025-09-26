using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;

public class SerialToScreen : MonoBehaviour
{
    [Header("References")]
    public ScreenManager screenManager;

    [Header("Serial")]
    public string portName = "";          // 例: "COM3" / "/dev/tty.usbmodem1101"（空なら自動検出）
    public int baudRate = 115200;
    public int readTimeoutMs = 50;
    public bool autoConnect = true;
    public bool logRaw = true;

    [Header("Dev")]
    public bool simulateWithKeyboard = true; // 0-9, F1-F6 = 10-15

    SerialPort _port;
    Thread _thread;
    volatile bool _running;
    readonly ConcurrentQueue<int> _queue = new();

    void Start()
    {
        if (!screenManager) screenManager = GetComponent<ScreenManager>();
        TryOpen();
    }

    void OnDestroy()
    {
        _running = false;
        try { _thread?.Join(200); } catch { }
        try { _port?.Close(); } catch { }
    }

    void TryOpen()
    {
        if (!autoConnect) return;

        if (string.IsNullOrEmpty(portName))
        {
            var names = SerialPort.GetPortNames();
            if (names != null && names.Length > 0) portName = names[0];
        }

        if (string.IsNullOrEmpty(portName))
        {
            Debug.LogWarning("[Serial] Port not found. Keyboard simulate only.");
            return;
        }

        try
        {
            _port = new SerialPort(portName, baudRate);
            _port.ReadTimeout = readTimeoutMs;
            _port.Open();

            _running = true;
            _thread = new Thread(ReadLoop) { IsBackground = true };
            _thread.Start();

            Debug.Log($"[Serial] Opened {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Serial] Open failed: {e.Message}. Keyboard simulate only.");
            _port = null;
        }
    }

    void ReadLoop()
    {
        // 改行区切りで 0〜15 の整数が届く想定
        while (_running && _port != null && _port.IsOpen)
        {
            try
            {
                string line = _port.ReadLine(); // timeout 時は例外
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (logRaw) Debug.Log($"[Serial RAW] {line.Trim()}");

                if (int.TryParse(FilterDigits(line), out int code))
                {
                    _queue.Enqueue(code);
                }
            }
            catch (TimeoutException) { /* 無視 */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[Serial] Read error: {e.Message}");
            }
        }
    }

    static string FilterDigits(string s)
    {
        // "code: 7\r\n" のような行から数字だけ抽出
        var sb = new System.Text.StringBuilder(4);
        foreach (var ch in s) if (char.IsDigit(ch)) sb.Append(ch);
        return sb.Length > 0 ? sb.ToString() : s;
    }

    void Update()
    {
        // 受信処理
        while (_queue.TryDequeue(out int code))
        {
            HandleIncomingCode(code);
        }

        // キーボード・シミュレータ
        if (simulateWithKeyboard)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) HandleIncomingCode(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) HandleIncomingCode(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) HandleIncomingCode(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) HandleIncomingCode(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) HandleIncomingCode(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) HandleIncomingCode(5);
            if (Input.GetKeyDown(KeyCode.Alpha6)) HandleIncomingCode(6);
            if (Input.GetKeyDown(KeyCode.Alpha7)) HandleIncomingCode(7);
            if (Input.GetKeyDown(KeyCode.Alpha8)) HandleIncomingCode(8);
            if (Input.GetKeyDown(KeyCode.Alpha9)) HandleIncomingCode(9);
            if (Input.GetKeyDown(KeyCode.F1)) HandleIncomingCode(10);
            if (Input.GetKeyDown(KeyCode.F2)) HandleIncomingCode(11);
            if (Input.GetKeyDown(KeyCode.F3)) HandleIncomingCode(12);
            if (Input.GetKeyDown(KeyCode.F4)) HandleIncomingCode(13);
            if (Input.GetKeyDown(KeyCode.F5)) HandleIncomingCode(14);
            if (Input.GetKeyDown(KeyCode.F6)) HandleIncomingCode(15);
        }
    }

    // ====== 分岐本体：1/10 を NFCRead に、2/12 は指定どおり。他は現状維持 ======
    void HandleIncomingCode(int code)
    {
        if (screenManager == null) return;

        switch (code)
        {
            // 0: 待機へ戻す（画面=Idle、音声=backToIdleVoice を必ず鳴らす）
            case 0:
                screenManager.Show(ScreenManager.Panel.Idle, playVoice: false);
                screenManager.SpeakNow(screenManager.backToIdleVoice);
                break;

            // 1: NFCタグ読み込み開始 → ★NFCRead へ
            case 1:
                Debug.Log("[Flow] NFC read start");
                screenManager.Show(ScreenManager.Panel.NFCRead);
                screenManager.SpeakNow(screenManager.nfcReadVoice);
                break;

            // 2: NFCタグ検出 → 手をかざす
            case 2:
                Debug.Log("[Flow] NFC detected");
                screenManager.Show(ScreenManager.Panel.HoldHand, playVoice: false);
                screenManager.SpeakNow(screenManager.holdHandVoice);
                break;

            // 3: 既定外タグ（エラー）
            case 3:
                Debug.LogWarning("[Flow] Wrong NFC tag");
                screenManager.failureVoice = "このタグは使えません。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 4: 読み出し失敗（エラー）
            case 4:
                Debug.LogError("[Flow] NFC read failed");
                screenManager.failureVoice = "タグの読み出しに失敗しました。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 5: 同一タグの連続（エラー）
            case 5:
                Debug.LogWarning("[Flow] Same NFC tag blocked");
                screenManager.failureVoice = "同じタグが続けて読み込まれました。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 6: センシング開始（準備）
            case 6:
                Debug.Log("[Flow] Measuring ready");
                screenManager.Show(ScreenManager.Panel.MeasureReady, playVoice: false);
                screenManager.SpeakNow(screenManager.measureReadyVoice);
                break;

            // 7: 指検出（計測中）
            case 7:
                Debug.Log("[Flow] Finger detected");
                screenManager.Show(ScreenManager.Panel.Measuring); // measuringVoice が自動再生
                break;

            // 8: センシングTimeout（エラー）
            case 8:
                Debug.LogError("[Flow] Measuring timeout");
                screenManager.failureVoice = "計測がタイムアウトしました。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 9: 計測完了
            case 9:
                Debug.Log("[Flow] Measuring done");
                screenManager.Show(ScreenManager.Panel.Success);
                break;

            // 10: （2回目の）NFCタグ読み込み開始 → ★NFCRead へ
            case 10:
                Debug.Log("[Flow] NFC read start (2nd)");
                screenManager.Show(ScreenManager.Panel.NFCRead);
                screenManager.SpeakNow(screenManager.nfcReadVoice);
                break;

            // 11: 見つからず（エラー）
            case 11:
                Debug.LogError("[Flow] NFC not found (timeout)");
                screenManager.failureVoice = "タグが見つかりませんでした。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 12: NFCタグ書き込み開始（そのまま NFCWrite）
            case 12:
                Debug.Log("[Flow] NFC write start");
                screenManager.Show(ScreenManager.Panel.NFCWrite);
                screenManager.SpeakNow(screenManager.nfcWriteVoice);
                break;

            // 13: 書き込み中断（エラー）
            case 13:
                Debug.LogError("[Flow] NFC write interrupted");
                screenManager.failureVoice = "書き込みを中断しました。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 14: データ不整合（エラー）
            case 14:
                Debug.LogError("[Flow] NFC write mismatch");
                screenManager.failureVoice = "データが一致しません。";
                screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
                screenManager.SpeakNow(screenManager.failureVoice);
                break;

            // 15: 書き込み成功
            case 15:
                Debug.Log("[Flow] NFC write done");
                screenManager.Show(ScreenManager.Panel.Done);
                break;

            default:
                Debug.Log($"[Flow] Unknown code {code}");
                break;
        }
    }
}


// 正常に動いてたバージョン9-26-18:22
// using UnityEngine;
// using System;
// using System.IO.Ports;
// using System.Threading;
// using System.Collections.Concurrent;
// using Unity.Mathematics;

// public class SerialToScreen : MonoBehaviour
// {
//     [Header("References")]
//     public ScreenManager screenManager;

//     [Header("Serial")]
//     public string portName = "";          // 例: "COM3" / "/dev/tty.usbmodem1101"（空なら自動検出）
//     public int baudRate = 115200;
//     public int readTimeoutMs = 50;
//     public bool autoConnect = true;
//     public bool logRaw = true;

//     [Header("Dev")]
//     public bool simulateWithKeyboard = true; // 0-9, F1-F6 = 10-15

//     SerialPort _port;
//     Thread _thread;
//     volatile bool _running;
//     readonly ConcurrentQueue<int> _queue = new();

//     void Start()
//     {
//         if (!screenManager) screenManager = GetComponent<ScreenManager>();
//         TryOpen();
//     }

//     void OnDestroy()
//     {
//         _running = false;
//         try { _thread?.Join(200); } catch { }
//         try { _port?.Close(); } catch { }
//     }

//     void TryOpen()
//     {
//         if (!autoConnect) return;

//         if (string.IsNullOrEmpty(portName))
//         {
//             var names = SerialPort.GetPortNames();
//             if (names != null && names.Length > 0) portName = names[0];
//         }

//         if (string.IsNullOrEmpty(portName))
//         {
//             Debug.LogWarning("[Serial] Port not found. Keyboard simulate only.");
//             return;
//         }

//         try
//         {
//             _port = new SerialPort(portName, baudRate);
//             _port.ReadTimeout = readTimeoutMs;
//             _port.Open();

//             _running = true;
//             _thread = new Thread(ReadLoop) { IsBackground = true };
//             _thread.Start();

//             Debug.Log($"[Serial] Opened {portName} @ {baudRate}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"[Serial] Open failed: {e.Message}. Keyboard simulate only.");
//             _port = null;
//         }
//     }

//     void ReadLoop()
//     {
//         // 改行区切りで 0〜15 の整数が届く想定
//         while (_running && _port != null && _port.IsOpen)
//         {
//             try
//             {
//                 string line = _port.ReadLine(); // timeout 時は例外
//                 if (string.IsNullOrWhiteSpace(line)) continue;

//                 if (logRaw) Debug.Log($"[Serial RAW] {line.Trim()}");

//                 if (int.TryParse(FilterDigits(line), out int code))
//                 {
//                     _queue.Enqueue(code);
//                 }
//             }
//             catch (TimeoutException) { /* 無視 */ }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"[Serial] Read error: {e.Message}");
//             }
//         }
//     }

//     static string FilterDigits(string s)
//     {
//         // "code: 7\r\n" のような行から数字だけ抽出
//         var sb = new System.Text.StringBuilder(4);
//         foreach (var ch in s) if (char.IsDigit(ch)) sb.Append(ch);
//         return sb.Length > 0 ? sb.ToString() : s;
//     }

//     void Update()
//     {
//         // 受信処理
//         while (_queue.TryDequeue(out int code))
//         {
//             HandleIncomingCode(code);
//         }

//         // キーボード・シミュレータ
//         if (simulateWithKeyboard)
//         {
//             if (Input.GetKeyDown(KeyCode.Alpha0)) HandleIncomingCode(0);
//             if (Input.GetKeyDown(KeyCode.Alpha1)) HandleIncomingCode(1);
//             if (Input.GetKeyDown(KeyCode.Alpha2)) HandleIncomingCode(2);
//             if (Input.GetKeyDown(KeyCode.Alpha3)) HandleIncomingCode(3);
//             if (Input.GetKeyDown(KeyCode.Alpha4)) HandleIncomingCode(4);
//             if (Input.GetKeyDown(KeyCode.Alpha5)) HandleIncomingCode(5);
//             if (Input.GetKeyDown(KeyCode.Alpha6)) HandleIncomingCode(6);
//             if (Input.GetKeyDown(KeyCode.Alpha7)) HandleIncomingCode(7);
//             if (Input.GetKeyDown(KeyCode.Alpha8)) HandleIncomingCode(8);
//             if (Input.GetKeyDown(KeyCode.Alpha9)) HandleIncomingCode(9);
//             if (Input.GetKeyDown(KeyCode.F1)) HandleIncomingCode(10);
//             if (Input.GetKeyDown(KeyCode.F2)) HandleIncomingCode(11);
//             if (Input.GetKeyDown(KeyCode.F3)) HandleIncomingCode(12);
//             if (Input.GetKeyDown(KeyCode.F4)) HandleIncomingCode(13);
//             if (Input.GetKeyDown(KeyCode.F5)) HandleIncomingCode(14);
//             if (Input.GetKeyDown(KeyCode.F6)) HandleIncomingCode(15);
//         }
//     }

//     // ====== 分岐本体：0/6/7 分離 & エラー確実発声 ======
//     void HandleIncomingCode(int code)
//     {
//         if (screenManager == null) return;

//         switch (code)
//         {
//             // 0: 待機へ戻す（画面=Idle、音声=backToIdleVoice を必ず鳴らす）
//             case 0:
//                 screenManager.Show(ScreenManager.Panel.Idle, playVoice: false);
//                 screenManager.SpeakNow(screenManager.backToIdleVoice);
//                 break;

//             // 1: NFCタグ読み込み開始（例：NFCWrite画面で案内）
//             case 1:
//                 Debug.Log("[Flow] NFC read start");
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             // 2: NFCタグ検出 → 手をかざす
//             case 2:
//                 Debug.Log("[Flow] NFC detected");
//                 screenManager.Show(ScreenManager.Panel.HoldHand, playVoice: false);
//                 screenManager.SpeakNow(screenManager.holdHandVoice);
//                 break;

//             // 3: 既定外タグ（エラー）
//             case 3:
//                 Debug.LogWarning("[Flow] Wrong NFC tag");
//                 screenManager.failureVoice = "このタグは使えません。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 4: 読み出し失敗（エラー）
//             case 4:
//                 Debug.LogError("[Flow] NFC read failed");
//                 screenManager.failureVoice = "タグの読み出しに失敗しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 5: 同一タグの連続（エラー）
//             case 5:
//                 Debug.LogWarning("[Flow] Same NFC tag blocked");
//                 screenManager.failureVoice = "同じタグが続けて読み込まれました。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 6: センシング開始（準備） ← 新設：MeasureReady 画面＆音声
//             case 6:
//                 Debug.Log("[Flow] Measuring ready");
//                 screenManager.Show(ScreenManager.Panel.MeasureReady, playVoice: false);
//                 screenManager.SpeakNow(screenManager.measureReadyVoice);
//                 break;

//             // 7: 指検出（計測中） ← ここで Measuring 画面＆「計測を開始します」
//             case 7:
//                 Debug.Log("[Flow] Finger detected");
//                 screenManager.Show(ScreenManager.Panel.Measuring); // measuringVoice が自動再生
//                 break;

//             // 8: センシングTimeout（エラー）
//             case 8:
//                 Debug.LogError("[Flow] Measuring timeout");
//                 screenManager.failureVoice = "計測がタイムアウトしました。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 9: 計測完了
//             case 9:
//                 Debug.Log("[Flow] Measuring done");
//                 screenManager.Show(ScreenManager.Panel.Success);
//                 break;

//             // 10: 2回目の読み込み開始（必要に応じて調整）
//             case 10:
//                 Debug.Log("[Flow] NFC read start (2nd)");
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             // 11: 見つからず（エラー）
//             case 11:
//                 Debug.LogError("[Flow] NFC not found (timeout)");
//                 screenManager.failureVoice = "タグが見つかりませんでした。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 12: 書き込み開始
//             case 12:
//                 Debug.Log("[Flow] NFC write start");
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             // 13: 書き込み中断（エラー）
//             case 13:
//                 Debug.LogError("[Flow] NFC write interrupted");
//                 screenManager.failureVoice = "書き込みを中断しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 14: データ不整合（エラー）
//             case 14:
//                 Debug.LogError("[Flow] NFC write mismatch");
//                 screenManager.failureVoice = "データが一致しません。";
//                 screenManager.Show(ScreenManager.Panel.Failure, playVoice: false);
//                 screenManager.SpeakNow(screenManager.failureVoice);
//                 break;

//             // 15: 書き込み成功
//             case 15:
//                 Debug.Log("[Flow] NFC write done");
//                 screenManager.Show(ScreenManager.Panel.Done);
//                 break;

//             default:
//                 Debug.Log($"[Flow] Unknown code {code}");
//                 break;
//         }
//     }
// }



// 正常に動いていたバージョン9-26-16:30
// using System;
// using System.IO.Ports;
// using System.Threading;
// using System.Collections.Concurrent;
// using UnityEngine;

// public class SerialToScreen : MonoBehaviour
// {
//     [Header("References")]
//     public ScreenManager screenManager;

//     [Header("Serial")]
//     public string portName = "";    // 例: /dev/tty.usbmodem1101（空なら自動検出）
//     public int baudRate = 115200;
//     public int readTimeoutMs = 50;
//     public bool autoConnect = true;
//     public bool logRaw = true;

//     [Header("Dev")]
//     public bool simulateWithKeyboard = true;

//     SerialPort _port;
//     Thread _thread;
//     volatile bool _running;
//     readonly ConcurrentQueue<int> _queue = new();

//     void Start()
//     {
//         if (!screenManager) screenManager = GetComponent<ScreenManager>();
//         TryOpen();
//     }

//     void OnDestroy()
//     {
//         _running = false;
//         try { _thread?.Join(200); } catch { }
//         try { _port?.Close(); } catch { }
//     }

//     void TryOpen()
//     {
//         if (simulateWithKeyboard)
//         {
//             Debug.LogWarning("[Serial] Port not found. Keyboard simulate only.");
//             return;
//         }

//         try
//         {
//             if (autoConnect && string.IsNullOrEmpty(portName))
//             {
//                 // mac 用の簡易検出
//                 foreach (var n in SerialPort.GetPortNames())
//                 {
//                     if (n.Contains("usbmodem") || n.Contains("usbserial")) { portName = n; break; }
//                 }
//             }

//             if (string.IsNullOrEmpty(portName))
//             {
//                 Debug.LogWarning("[Serial] No port specified.");
//                 simulateWithKeyboard = true;
//                 return;
//             }

//             _port = new SerialPort(portName, baudRate);
//             _port.ReadTimeout = readTimeoutMs;
//             _port.DtrEnable = true;
//             _port.Open();

//             _running = true;
//             _thread = new Thread(ReadLoop) { IsBackground = true };
//             _thread.Start();

//             Debug.Log($"[Serial] Opened {portName}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"[Serial] Open failed: {e.Message}");
//             simulateWithKeyboard = true;
//         }
//     }

//     void ReadLoop()
//     {
//         while (_running)
//         {
//             try
//             {
//                 string line = _port.ReadLine(); // 改行区切りを想定
//                 if (logRaw) Debug.Log($"[Serial-Raw] {line}");
//                 if (int.TryParse(line.Trim(), out int code))
//                 {
//                     _queue.Enqueue(code);
//                 }
//             }
//             catch (TimeoutException) { /* 無視 */ }
//             catch (Exception e)
//             {
//                 Debug.LogError($"[Serial] Read error: {e.Message}");
//                 break;
//             }
//         }
//     }

//     void Update()
//     {
//         // シミュレーション（1〜9, 0, Q/W/E/R/T などで拡張してもOK）
//         if (simulateWithKeyboard)
//         {
//             if (Input.GetKeyDown(KeyCode.Alpha1)) _queue.Enqueue(1);
//             if (Input.GetKeyDown(KeyCode.Alpha2)) _queue.Enqueue(2);
//             if (Input.GetKeyDown(KeyCode.Alpha3)) _queue.Enqueue(3);
//             if (Input.GetKeyDown(KeyCode.Alpha4)) _queue.Enqueue(4);
//             if (Input.GetKeyDown(KeyCode.Alpha5)) _queue.Enqueue(5);
//             if (Input.GetKeyDown(KeyCode.Alpha6)) _queue.Enqueue(6);
//             if (Input.GetKeyDown(KeyCode.Alpha7)) _queue.Enqueue(7);
//             if (Input.GetKeyDown(KeyCode.Alpha8)) _queue.Enqueue(8);
//             if (Input.GetKeyDown(KeyCode.Alpha9)) _queue.Enqueue(9);
//             if (Input.GetKeyDown(KeyCode.Alpha0)) _queue.Enqueue(10);
//             if (Input.GetKeyDown(KeyCode.Q))       _queue.Enqueue(11);
//             if (Input.GetKeyDown(KeyCode.W))       _queue.Enqueue(12);
//             if (Input.GetKeyDown(KeyCode.E))       _queue.Enqueue(13);
//             if (Input.GetKeyDown(KeyCode.R))       _queue.Enqueue(14);
//             if (Input.GetKeyDown(KeyCode.T))       _queue.Enqueue(15);
//         }

//         while (_queue.TryDequeue(out int code))
//         {
//             HandleIncomingCode(code);
//         }
//     }

//     // Arduino からの 1〜15 に対応して画面を切替
//     void HandleIncomingCode(int code)
//     {
//         if (screenManager == null) return;

//         switch (code)
//         {
//             case 1: // NFCタグ読み込み開始
//                 Debug.Log("[Flow] NFC read start");
//                 screenManager.nfcReadVoice = "タグを読み込みます。";
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 2: // NFCタグ検出
//                 Debug.Log("[Flow] NFC detected");
//                 screenManager.Show(ScreenManager.Panel.HoldHand);
//                 screenManager.SpeakNow(screenManager.holdHandVoice);
//                 break;

//             case 3: // 既定外タグ
//                 Debug.LogWarning("[Flow] Wrong NFC tag");
//                 screenManager.failureVoice = "このタグは使えません。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 4: // データ読み出し失敗
//                 Debug.LogError("[Flow] NFC read failed");
//                 screenManager.failureVoice = "タグの読み出しに失敗しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 5: // 同一タグの連続
//                 Debug.LogWarning("[Flow] Same NFC tag blocked");
//                 screenManager.failureVoice = "同じタグが続けて使えません。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 6: // センシング開始
//                 Debug.Log("[Flow] Measuring start");
//                 screenManager.Show(ScreenManager.Panel.Measuring);
//                 break;

//             case 7: // 指を検出
//                 Debug.Log("[Flow] Finger detected");
//                 screenManager.Show(ScreenManager.Panel.Measuring);
//                 break;

//             case 8: // センシングTimeout
//                 Debug.LogError("[Flow] Measuring timeout");
//                 screenManager.failureVoice = "計測がタイムアウトしました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 9: // 計測完了
//                 Debug.Log("[Flow] Measuring done");
//                 screenManager.Show(ScreenManager.Panel.Success);
//                 break;

//             case 10: // 2回目の読み込み開始
//                 Debug.Log("[Flow] NFC read start (2nd)");
//                 screenManager.nfcWriteVoice = "もう一度タグを読み込みます。";
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 11: // 見つからず
//                 Debug.LogError("[Flow] NFC not found (timeout)");
//                 screenManager.failureVoice = "タグが見つかりませんでした。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 12: // 書き込み開始
//                 Debug.Log("[Flow] NFC write start");
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 13: // 書き込み中断
//                 Debug.LogError("[Flow] NFC write interrupted");
//                 screenManager.failureVoice = "書き込みが中断しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 14: // 不整合
//                 Debug.LogError("[Flow] NFC write mismatch");
//                 screenManager.failureVoice = "データが一致しません。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 15: // 書き込み成功
//                 Debug.Log("[Flow] NFC write done");
//                 screenManager.Show(ScreenManager.Panel.Done);
//                 break;

//             default:
//                 Debug.Log($"[Flow] Unknown code {code}");
//                 break;
//         }
//     }
// }



// 正常に動いていたバージョン9-25-15:26
// using UnityEngine;
// using System;
// using System.IO.Ports;
// using System.Threading;
// using System.Collections.Concurrent;

// public class SerialToScreen : MonoBehaviour
// {
//     [Header("References")]
//     public ScreenManager screenManager;

//     [Header("Serial")]
//     public string portName = "";       // 例: /dev/tty.usbmodem1101（空なら自動）
//     public int baudRate = 115200;
//     public int readTimeoutMs = 50;
//     public bool autoConnect = true;
//     public bool logRaw = true;

//     [Header("Dev")]
//     public bool simulateWithKeyboard = true;

//     SerialPort _port;
//     Thread _thread;
//     volatile bool _running;
//     readonly ConcurrentQueue<int> _queue = new ConcurrentQueue<int>();

//     void Start()
//     {
//         if (!screenManager) screenManager = GetComponent<ScreenManager>();
//         TryOpen();
//     }

//     void TryOpen()
//     {
//         try
//         {
//             if (autoConnect && string.IsNullOrEmpty(portName))
//             {
//                 foreach (var n in SerialPort.GetPortNames())
//                 {
// #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
//                     if (n.Contains("usbmodem") || n.Contains("usbserial")) { portName = n; break; }
// #else
//                     portName = n; break;
// #endif
//                 }
//             }

//             if (!string.IsNullOrEmpty(portName))
//             {
//                 _port = new SerialPort(portName, baudRate);
//                 _port.ReadTimeout = readTimeoutMs;
//                 _port.Open();
//                 _running = true;
//                 _thread = new Thread(ReadLoop) { IsBackground = true };
//                 _thread.Start();
//                 Debug.Log($"[Serial] Opened {portName}");
//             }
//             else
//             {
//                 Debug.LogWarning("[Serial] Port not found. Keyboard simulate only.");
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"[Serial] Open failed: {e.Message}");
//         }
//     }

//     void ReadLoop()
//     {
//         while (_running)
//         {
//             try
//             {
//                 var line = _port.ReadLine(); // 例: "7\n"
//                 if (logRaw) Debug.Log($"[Serial RAW] {line}");
//                 if (int.TryParse(line.Trim(), out var code))
//                     _queue.Enqueue(code);
//             }
//             catch (TimeoutException) { /* 無視 */ }
//             catch (Exception e)
//             {
//                 Debug.LogError($"[Serial] Read error: {e.Message}");
//                 Thread.Sleep(100);
//             }
//         }
//     }

//     void OnDestroy()
//     {
//         _running = false;
//         try { _thread?.Join(200); } catch { }
//         try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
//     }

//     void Update()
//     {
//         // キーボードで簡易シミュレーション
//         if (simulateWithKeyboard)
//         {
//             int code = -1;
//             if (Input.GetKeyDown(KeyCode.Alpha1)) code = 1;
//             else if (Input.GetKeyDown(KeyCode.Alpha2)) code = 2;
//             else if (Input.GetKeyDown(KeyCode.Alpha3)) code = 3;
//             else if (Input.GetKeyDown(KeyCode.Alpha4)) code = 4;
//             else if (Input.GetKeyDown(KeyCode.Alpha5)) code = 5;
//             else if (Input.GetKeyDown(KeyCode.Alpha6)) code = 6;
//             else if (Input.GetKeyDown(KeyCode.Alpha7)) code = 7;
//             else if (Input.GetKeyDown(KeyCode.Alpha8)) code = 8;
//             else if (Input.GetKeyDown(KeyCode.Alpha9)) code = 9;
//             else if (Input.GetKeyDown(KeyCode.Alpha0)) code = 10;
//             else if (Input.GetKeyDown(KeyCode.Q)) code = 11;
//             else if (Input.GetKeyDown(KeyCode.W)) code = 12;
//             else if (Input.GetKeyDown(KeyCode.E)) code = 13;
//             else if (Input.GetKeyDown(KeyCode.R)) code = 14;
//             else if (Input.GetKeyDown(KeyCode.T)) code = 15;
//             if (code != -1) HandleIncomingCode(code);
//         }

//         // シリアル→メインスレッドに適用
//         while (_queue.TryDequeue(out var c))
//             HandleIncomingCode(c);
//     }

//     void HandleIncomingCode(int code)
//     {
//         if (screenManager == null) return;

//         switch (code)
//         {
//             case 1: // NFCタグ読み込み開始
//                 Debug.Log("[Flow] NFC read start");
//                 screenManager.nfcWriteVoice = "タグを読み込みます。";
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 2: // NFCタグ検出
//                 Debug.Log("[Flow] NFC detected");
//                 screenManager.Show(ScreenManager.Panel.HoldHand);
//                 screenManager.Speak(screenManager.holdHandVoice);
//                 break;

//             case 3: // 既定外タグ
//                 Debug.LogWarning("[Flow] Wrong NFC tag");
//                 screenManager.failureVoice = "このタグは使えません。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 4: // 読み出し失敗
//                 Debug.LogError("[Flow] NFC read failed");
//                 screenManager.failureVoice = "タグの読み出しに失敗しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 5: // 同一タグの連続
//                 Debug.LogWarning("[Flow] Same NFC tag blocked");
//                 screenManager.failureVoice = "同じタグが続けて読み込まれました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 6: // センシング開始
//                 Debug.Log("[Flow] Measuring start");
//                 screenManager.Show(ScreenManager.Panel.Measuring);
//                 break;

//             case 7: // 指を検出
//                 Debug.Log("[Flow] Finger detected");
//                 // 画面は Measuring 続行。必要なら音声だけ
//                 screenManager.Speak("指を検出しました。");
//                 break;

//             case 8: // センシングTimeout
//                 Debug.LogError("[Flow] Measuring timeout");
//                 screenManager.failureVoice = "計測がタイムアウトしました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 9: // 計測完了
//                 Debug.Log("[Flow] Measuring done");
//                 screenManager.Show(ScreenManager.Panel.Success);
//                 break;

//             case 10: // 2回目の読み込み開始
//                 Debug.Log("[Flow] NFC read start (2nd)");
//                 screenManager.nfcWriteVoice = "もう一度タグを読み込みます。";
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 11: // 見つからず
//                 Debug.LogError("[Flow] NFC not found (timeout)");
//                 screenManager.failureVoice = "タグが見つかりませんでした。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 12: // 書き込み開始
//                 Debug.Log("[Flow] NFC write start");
//                 screenManager.nfcWriteVoice = "タグに書き込みます。";
//                 screenManager.Show(ScreenManager.Panel.NFCWrite);
//                 break;

//             case 13: // 書き込み中に外した
//                 Debug.LogError("[Flow] NFC write interrupted");
//                 screenManager.failureVoice = "書き込みに失敗しました。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 14: // 不整合
//                 Debug.LogError("[Flow] NFC write mismatch");
//                 screenManager.failureVoice = "データが一致しません。";
//                 screenManager.Show(ScreenManager.Panel.Failure);
//                 break;

//             case 15: // 書き込み成功
//                 Debug.Log("[Flow] NFC write done");
//                 screenManager.Show(ScreenManager.Panel.Done);
//                 break;

//             default:
//                 Debug.Log($"[Flow] Unknown code {code}");
//                 break;
//         }
//     }
// }



// 最初のArudino確認用
// using UnityEngine;
// using System;
// using System.IO.Ports;
// using System.Threading;
// using System.Collections.Concurrent;

// public class SerialToScreen : MonoBehaviour
// {
//     [Header("References")]
//     public ScreenManager screenManager;

//     [Header("Serial")]
//     public string portName = "";          // 例: /dev/tty.usbmodem1101（空なら自動探索）
//     public int baudRate = 115200;
//     public int readTimeoutMs = 50;
//     public bool autoConnect = true;
//     public bool logRaw = true;

//     SerialPort _port;
//     Thread _thread;
//     volatile bool _running;
//     readonly ConcurrentQueue<int> _queue = new ConcurrentQueue<int>();

//     void Start()
//     {
//         if (!screenManager) screenManager = GetComponent<ScreenManager>();
//         TryOpen();
//     }

//     void TryOpen()
//     {
//         try
//         {
//             if (autoConnect && string.IsNullOrEmpty(portName))
//             {
//                 foreach (var n in SerialPort.GetPortNames())
//                 {
//                     // mac は "usbmodem" / "usbserial" が多い
//                     if (n.Contains("usbmodem") || n.Contains("usbserial"))
//                     {
//                         portName = n; break;
//                     }
//                 }
//                 if (string.IsNullOrEmpty(portName))
//                     Debug.LogWarning("[SerialToScreen] シリアルポートが見つかりません。Inspectorで portName を手動指定してください。");
//             }
//             if (string.IsNullOrEmpty(portName)) return;

//             _port = new SerialPort(portName, baudRate);
//             _port.NewLine = "\n";
//             _port.ReadTimeout = readTimeoutMs;
//             _port.DtrEnable = true;
//             _port.RtsEnable = true;
//             _port.Open();

//             _running = true;
//             _thread = new Thread(ReadLoop) { IsBackground = true };
//             _thread.Start();

//             Debug.Log($"[SerialToScreen] Opened {portName} @ {baudRate}");
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError($"[SerialToScreen] Open failed: {e.Message}");
//         }
//     }

//     void ReadLoop()
//     {
//         while (_running && _port != null && _port.IsOpen)
//         {
//             try
//             {
//                 var line = _port.ReadLine();    // Arduino側は Serial.println(数字); を送る
//                 var trimmed = line.Trim();
//                 if (logRaw) Debug.Log($"[UART] {trimmed}");
//                 if (int.TryParse(trimmed, out var code))
//                     _queue.Enqueue(code);
//             }
//             catch (TimeoutException) { /* 無視 */ }
//             catch (System.Exception e)
//             {
//                 Debug.LogWarning($"[SerialToScreen] Read error: {e.Message}");
//                 Thread.Sleep(100);
//             }
//         }
//     }

//     void Update()
//     {
//         // メインスレッドでUI反映
//         while (_queue.TryDequeue(out var code))
//             screenManager?.ShowByCode(code);
//     }

//     void OnDisable() => Close();
//     void OnApplicationQuit() => Close();

//     void Close()
//     {
//         _running = false;
//         try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
//         try { if (_thread != null && _thread.IsAlive) _thread.Join(200); } catch { }
//     }
// }


// キーボード確認用
// using UnityEngine;
// using System;
// using System.IO.Ports;
// using System.Threading;
// using System.Collections.Concurrent;

// public class SerialToScreen : MonoBehaviour
// {
//     [Header("References")]
//     public ScreenManager screenManager;

//     [Header("Serial")]
//     public string portName = "";          // 例: /dev/tty.usbmodem1101（空なら自動探索）
//     public int baudRate = 115200;
//     public int readTimeoutMs = 50;
//     public bool autoConnect = true;
//     public bool logRaw = false;

//     [Header("Dev")]
//     public bool simulateWithKeyboard = true; // ← キーボードで 1〜7 を受け付ける

//     private SerialPort _port;
//     private Thread _thread;
//     private volatile bool _running;
//     private readonly ConcurrentQueue<int> _queue = new ConcurrentQueue<int>();

//     void Start()
//     {
//         if (!screenManager) screenManager = GetComponent<ScreenManager>();
//         TryOpen();
//     }

//     void TryOpen()
//     {
//         if (simulateWithKeyboard) return; // シミュレーション時は開かない

//         try
//         {
//             if (autoConnect && string.IsNullOrEmpty(portName))
//             {
//                 foreach (var n in SerialPort.GetPortNames())
//                 {
//                     // mac は "usbmodem" / "usbserial" を含むことが多い
//                     if (n.Contains("usbmodem") || n.Contains("usbserial"))
//                     {
//                         portName = n;
//                         break;
//                     }
//                 }
//             }

//             if (string.IsNullOrEmpty(portName))
//             {
//                 Debug.LogWarning("[SerialToScreen] Port name is empty. Enable 'Simulate With Keyboard' or set Port Name.");
//                 return;
//             }

//             _port = new SerialPort(portName, baudRate);
//             _port.ReadTimeout = readTimeoutMs;
//             _port.NewLine = "\n";
//             _port.Open();

//             _running = true;
//             _thread = new Thread(ReadLoop) { IsBackground = true };
//             _thread.Start();

//             Debug.Log($"[SerialToScreen] Opened {portName} @ {baudRate}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogWarning($"[SerialToScreen] Open failed: {e.Message}");
//         }
//     }

//     void ReadLoop()
//     {
//         while (_running && _port != null)
//         {
//             try
//             {
//                 string line = _port.ReadLine()?.Trim();
//                 if (string.IsNullOrEmpty(line)) continue;

//                 if (logRaw) Debug.Log($"[Serial RAW] {line}");

//                 // 「1」や「S,1」などから最初の数字を拾う
//                 if (int.TryParse(line, out int code))
//                 {
//                     _queue.Enqueue(code);
//                 }
//                 else
//                 {
//                     var tokens = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
//                     foreach (var t in tokens)
//                     {
//                         if (int.TryParse(t, out code))
//                         {
//                             _queue.Enqueue(code);
//                             break;
//                         }
//                     }
//                 }
//             }
//             catch (TimeoutException) { /* 無視 */ }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"[SerialToScreen] read error: {e.Message}");
//                 Thread.Sleep(100);
//             }
//         }
//     }

//     void HandleIncomingCode(int code)
//     {
//         if (!screenManager) return;

//         // ★ ここが今回のポイント：int → enum に変換して Show する
//         if (Enum.IsDefined(typeof(ScreenManager.Panel), code))
//         {
//             screenManager.Show((ScreenManager.Panel)code);
//         }
//         else
//         {
//             Debug.Log($"[SerialToScreen] Unknown code: {code}");
//         }
//     }

//     void Update()
//     {
//         // キーボード・シミュレーション（Gameビューにフォーカスして 1〜7）
//         if (simulateWithKeyboard)
//         {
//             int code = -1;
//             if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) code = 1;
//             else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) code = 2;
//             else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) code = 3;
//             else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) code = 4;
//             else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) code = 5;
//             else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) code = 6;
//             else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) code = 7;

//             if (code != -1) HandleIncomingCode(code);
//         }
//         else
//         {
//             // シリアルから来たキューを処理
//             while (_queue.TryDequeue(out var code))
//                 HandleIncomingCode(code);
//         }
//     }

//     void OnApplicationQuit() => Close();
//     void OnDestroy()         => Close();

//     void Close()
//     {
//         _running = false;
//         try { if (_thread != null && _thread.IsAlive) _thread.Join(200); } catch { }
//         if (_port != null)
//         {
//             try { if (_port.IsOpen) _port.Close(); } catch { }
//             _port.Dispose();
//             _port = null;
//         }
//     }
// }