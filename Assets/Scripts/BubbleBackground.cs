using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BubbleBackground : MonoBehaviour
{
    [Header("References")]
    public RectTransform spawnArea;     // 未設定なら自分を使用
    public GameObject bubblePrefab;     // Image(UI) もしくは子に Image を持つプレハブ

    [Header("Spawn")]
    public int   maxBubbles      = 25;
    public float spawnInterval   = 0.30f;
    public float margin          = 20f;   // 端からの余白
    public bool  spawnFromBottom = false; // 画面下の外から湧かせたいとき

    [Header("Motion / Look")]
    public Vector2 sizeRange   = new Vector2(80, 160);
    public Vector2 speedYRange = new Vector2(20, 40);
    public float   driftAmp    = 40f;   // 左右ゆらぎの振幅(px)
    public float   driftFreq   = 0.6f;  // ゆらぎの速さ
    public float   lifeTime    = 8f;    // 1個の寿命（秒）

    [Header("Appearance")]
    [Range(0f, 1f)] public float baseAlpha = 0.15f; // 標準の不透明度（0.1～0.2推奨）
    public float fadeInTime  = 0.6f;                // 出現フェード（秒）
    public float fadeOutTime = 1.0f;                // 消滅フェード（秒）
    public bool  behindText  = true;                // テキストより背面に置く

    readonly List<RectTransform> _alive = new();
    RectTransform _area;
    float _timer;

    void Awake()
    {
        _area = spawnArea ? spawnArea : GetComponent<RectTransform>();
    }

    void Update()
    {
        // 生成
        if (_alive.Count < maxBubbles)
        {
            _timer += Time.deltaTime;
            if (_timer >= spawnInterval)
            {
                _timer = 0f;
                SpawnOne();
            }
        }

        // 更新 & 破棄
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var rt = _alive[i];
            if (!rt) { _alive.RemoveAt(i); continue; }

            var b = rt.GetComponent<_Bubble>();
            if (!b) { _alive.RemoveAt(i); Destroy(rt.gameObject); continue; }

            if (!b.Tick(Time.deltaTime))
            {
                _alive.RemoveAt(i);
                Destroy(rt.gameObject);
            }
        }
    }

    void SpawnOne()
    {
        if (!bubblePrefab || !_area) return;

        var go = Instantiate(bubblePrefab, _area);
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

        // UI座標（中央基準）
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        // サイズ決定
        float size = Random.Range(sizeRange.x, sizeRange.y);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   size);

        // 出現位置
        var rect = _area.rect; // 中心(0,0)基準
        float x = Random.Range(rect.xMin + margin + size * 0.5f,
                               rect.xMax - margin - size * 0.5f);

        float y = spawnFromBottom
            ? rect.yMin - size * 0.6f              // 画面外の下から
            : Random.Range(rect.yMin + margin + size * 0.5f,
                           rect.yMax - margin - size * 0.5f);

        rt.anchoredPosition = new Vector2(x, y);

        // クリックなどの判定を持たせない
        foreach (var img in go.GetComponentsInChildren<Image>(true))
        {
            img.raycastTarget = false;
        }

        // 重なり順
        if (behindText) rt.SetAsFirstSibling();

        // 挙動付与（フェードは内部で制御）
        var bub = go.AddComponent<_Bubble>();
        bub.Init(_area.rect, speedYRange, driftAmp, driftFreq,
                 lifeTime, size, spawnFromBottom,
                 baseAlpha, fadeInTime, fadeOutTime);
        _alive.Add(rt);
    }

    // --- 1 個のバブルの挙動 ---
    class _Bubble : MonoBehaviour
    {
        RectTransform _rt;
        Rect _bounds;
        float _vy, _amp, _freq, _life, _age, _size;
        bool _fromBottom;

        readonly List<Image> _images = new();
        float _baseAlpha;
        float _fadeIn, _fadeOut;

        public void Init(
            Rect bounds, Vector2 vyRange, float amp, float freq,
            float life, float size, bool fromBottom,
            float baseAlpha, float fadeInTime, float fadeOutTime)
        {
            _rt = GetComponent<RectTransform>();
            _bounds = bounds;
            _vy   = Random.Range(vyRange.x, vyRange.y);
            _amp  = amp;
            _freq = freq * Random.Range(0.8f, 1.2f);
            _life = life;
            _size = size;
            _fromBottom = fromBottom;

            _images.Clear();
            _images.AddRange(GetComponentsInChildren<Image>(true));

            _baseAlpha = Mathf.Clamp01(baseAlpha);
            _fadeIn  = Mathf.Max(0.001f, fadeInTime);
            _fadeOut = Mathf.Max(0.001f, fadeOutTime);

            // 最初は透明（フェードイン）
            SetAlpha(0f);

            // 揺れの位相をランダマイズ
            _age = Random.value * 0.2f;
        }

        public bool Tick(float dt)
        {
            _age  += dt;
            _life -= dt;
            if (_life <= 0f) return false;

            // 位置更新（上方向 + 横ゆらぎ）
            var p = _rt.anchoredPosition;
            p.x += Mathf.Sin(_age * _freq) * _amp * dt;
            p.y += _vy * dt;
            _rt.anchoredPosition = p;

            // 画面上端を出たら終了
            if (p.y - _size * 0.5f > _bounds.yMax + 20f) return false;

            // アルファ計算：フェードイン／アウト
            float fin  = Mathf.Clamp01(_age / _fadeIn);
            float fout = Mathf.Clamp01(_life / _fadeOut);
            float a = _baseAlpha * Mathf.Min(fin, fout); // どちらか小さい方を採用
            SetAlpha(a);

            return true;
        }

        void SetAlpha(float a)
        {
            for (int i = 0; i < _images.Count; i++)
            {
                var c = _images[i].color;
                c.a = a;
                _images[i].color = c;
            }
        }
    }
}

// シャボン玉表現の最初のコード
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// [RequireComponent(typeof(RectTransform))]
// public class BubbleBackground : MonoBehaviour
// {
//     [Header("References")]
//     public RectTransform spawnArea;           // 未設定なら自分を使用
//     public GameObject bubblePrefab;           // Image(UI) のプレハブ

//     [Header("Spawn")]
//     public int   maxBubbles     = 25;
//     public float spawnInterval  = 0.3f;
//     public float margin         = 20f;        // 端からの余白
//     public bool  spawnFromBottom = false;     // 下端の外から湧かせたい時にON

//     [Header("Motion / Look")]
//     public Vector2 sizeRange    = new Vector2(80, 160);
//     public Vector2 speedYRange  = new Vector2(20, 40);
//     public float   driftAmp     = 40f;        // 左右ゆらぎの振幅(px)
//     public float   driftFreq    = 0.6f;       // ゆらぎの速さ
//     public float   lifeTime     = 8f;         // 秒
//     public float   startAlpha   = 0.15f;      // 0.1〜0.2 推奨
//     public bool    behindText   = true;       // テキストの背面に回す

//     readonly List<RectTransform> _alive = new();
//     RectTransform _area;
//     float _timer;

//     void Awake()
//     {
//         _area = spawnArea ? spawnArea : GetComponent<RectTransform>();
//         // 念のため：パネルは Stretch で画面いっぱいを想定
//     }

//     void Update()
//     {
//         // 生成
//         if (_alive.Count < maxBubbles)
//         {
//             _timer += Time.deltaTime;
//             if (_timer >= spawnInterval)
//             {
//                 _timer = 0f;
//                 SpawnOne();
//             }
//         }

//         // 生存更新
//         for (int i = _alive.Count - 1; i >= 0; i--)
//         {
//             var rt = _alive[i];
//             if (!rt) { _alive.RemoveAt(i); continue; }

//             var b = rt.GetComponent<_Bubble>();
//             if (!b) { _alive.RemoveAt(i); Destroy(rt.gameObject); continue; }

//             if (!b.Tick(Time.deltaTime))
//             {
//                 _alive.RemoveAt(i);
//                 Destroy(rt.gameObject);
//             }
//         }
//     }

//     void SpawnOne()
//     {
//         if (!bubblePrefab || !_area) return;

//         var go = Instantiate(bubblePrefab, _area);
//         var rt = go.GetComponent<RectTransform>();
//         if (!rt) rt = go.AddComponent<RectTransform>();

//         // 常に中央アンカー・中央ピボットで UI 空間を扱う
//         rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
//         rt.pivot     = new Vector2(0.5f, 0.5f);

//         // サイズ
//         float size = Random.Range(sizeRange.x, sizeRange.y);
//         rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
//         rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   size);

//         // パネル内に確実に収まるよう座標を計算
//         var rect = _area.rect; // 中心(0,0)基準のローカル座標空間
//         float x = Random.Range(rect.xMin + margin + size * 0.5f,
//                                rect.xMax - margin - size * 0.5f);

//         float y;
//         if (spawnFromBottom)
//         {
//             // 画面外の少し下からスタートして上に入ってくる
//             y = rect.yMin - size * 0.6f;
//         }
//         else
//         {
//             // 完全にパネル内でランダム
//             y = Random.Range(rect.yMin + margin + size * 0.5f,
//                              rect.yMax - margin - size * 0.5f);
//         }
//         rt.anchoredPosition = new Vector2(x, y);

//         // 見た目（α）
//         var img = go.GetComponent<Image>();
//         if (img)
//         {
//             var c = img.color;
//             c.a   = startAlpha;
//             img.color = c;
//             img.raycastTarget = false;
//         }

//         // 重なり順：背面にしたい場合は最前の背景側へ移動
//         if (behindText) rt.SetAsFirstSibling();

//         // 挙動付与
//         var bub = go.AddComponent<_Bubble>();
//         bub.Init(_area.rect, speedYRange, driftAmp, driftFreq, lifeTime, size, spawnFromBottom);
//         _alive.Add(rt);
//     }

//     // 内部用：1 個のバブルの挙動
//     class _Bubble : MonoBehaviour
//     {
//         RectTransform _rt;
//         Rect _bounds;
//         float _vy, _amp, _freq, _life, _t, _size;
//         bool _fromBottom;
//         Image _img;

//         public void Init(Rect bounds, Vector2 vyRange, float amp, float freq, float life, float size, bool fromBottom)
//         {
//             _rt = GetComponent<RectTransform>();
//             _bounds = bounds;
//             _vy   = Random.Range(vyRange.x, vyRange.y);
//             _amp  = amp;
//             _freq = freq * Random.Range(0.8f, 1.2f);
//             _life = life;
//             _size = size;
//             _fromBottom = fromBottom;
//             _img = GetComponent<Image>();
//             _t = Random.value * 10f; // 揺れ位相をバラす
//         }

//         public bool Tick(float dt)
//         {
//             _t += dt;
//             _life -= dt;
//             if (_life <= 0f) return false;

//             // 上方向へ移動＋左右にふわふわ
//             var p = _rt.anchoredPosition;
//             float dx = Mathf.Sin(_t * _freq) * _amp * dt;
//             p.x += dx;
//             p.y += _vy * dt;
//             _rt.anchoredPosition = p;

//             // 上に抜けたら終了（見えないところで破棄）
//             if (p.y - _size * 0.5f > _bounds.yMax + 20f) return false;

//             // 終盤でフェードアウト
//             if (_img)
//             {
//                 float fade = Mathf.Clamp01(_life / 1.0f); // 最後の1秒でフェード
//                 var c = _img.color; c.a = c.a * fade; _img.color = c;
//             }
//             return true;
//         }
//     }
// }

// 試作保存1
// using UnityEngine;
// using UnityEngine.UI;
// using System.Collections.Generic;

// public class BubbleBackground : MonoBehaviour
// {
//     public RectTransform spawnArea;   // 未指定ならこのオブジェクトのRectTransform
//     public Image bubblePrefab;        // 丸いImage（UI/Sprite）
//     public int maxBubbles = 25;
//     public float spawnInterval = 0.3f;
//     public Vector2 sizeRange = new Vector2(60, 160);
//     public Vector2 speedYRange = new Vector2(15, 40);
//     public float driftAmp = 40f;
//     public float lifeTime = 8f;

//     readonly List<GameObject> _pool = new();

//     void OnEnable() { InvokeRepeating(nameof(Spawn), 0.2f, spawnInterval); }
//     void OnDisable() { CancelInvoke(nameof(Spawn)); }

//     void Spawn()
//     {
//         if (!bubblePrefab) return;
//         var root = spawnArea ? spawnArea : (RectTransform)transform;
//         _pool.RemoveAll(x => x == null);

//         if (_pool.Count >= maxBubbles) return;

//         var go = Instantiate(bubblePrefab, root).gameObject;
//         _pool.Add(go);

//         var img = go.GetComponent<Image>();
//         var rt = go.GetComponent<RectTransform>();

//         float size = Random.Range(sizeRange.x, sizeRange.y);
//         rt.sizeDelta = new Vector2(size, size);
//         rt.anchoredPosition = new Vector2(Random.Range(-root.rect.width * 0.5f, root.rect.width * 0.5f), -root.rect.height * 0.55f);

//         var col = img.color;
//         col.a = Random.Range(0.08f, 0.18f);
//         img.color = col;

//         float vy = Random.Range(speedYRange.x, speedYRange.y);
//         float phase = Random.Range(0f, Mathf.PI * 2f);

//         StartCoroutine(Animate(rt, img, vy, phase));
//     }

//     System.Collections.IEnumerator Animate(RectTransform rt, Image img, float vy, float phase)
//     {
//         float t = 0f;
//         while (t < lifeTime && rt != null)
//         {
//             t += Time.deltaTime;
//             var p = rt.anchoredPosition;
//             p.y += vy * Time.deltaTime;
//             p.x += Mathf.Sin((t + phase) * 0.6f) * Time.deltaTime * driftAmp;
//             rt.anchoredPosition = p;

//             // 端でフェード
//             var c = img.color;
//             c.a = Mathf.Lerp(c.a, 0f, t / lifeTime);
//             img.color = c;

//             yield return null;
//         }
//         if (rt) Destroy(rt.gameObject);
//     }
// }