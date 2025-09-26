using UnityEngine;

public interface ITTS
{
    // 再生先の AudioSource（ScreenManager から渡す）
    AudioSource audioSource { get; set; }

    // text を delaySec 秒後に再生（delaySec < 0 は各実装の既定値）
    void Speak(string text, float delaySec = -1f);
}

// 正常に動いてたバージョン9-26-16:30
// public interface ITTS
// {
//     /// <summary>
//     /// テキストを再生。delaySec < 0 の場合は実装側の既定値を使う。
//     /// </summary>
//     void Speak(string text, float delaySec = -1f);
// }