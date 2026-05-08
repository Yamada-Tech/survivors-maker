using UnityEngine;

/// <summary>
/// 被弾エフェクトの基底クラス。
/// このコンポーネントをアタッチしたPrefabをPlayerControllerの_hitEffectPrefabに設定する。
/// ユーザーはこのPrefabを自由にカスタマイズ可能（Particle System等に差し替えOK）。
/// デフォルトはコード生成の赤いスプライト。
/// </summary>
public class HitEffect : MonoBehaviour
{
    [SerializeField] private float _lifetime = 0.25f;
    private float _elapsed;
    private SpriteRenderer _sr;

    private void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _lifetime;

        // フェードアウト + スケールアップ
        if (_sr != null)
        {
            var c = _sr.color;
            c.a = Mathf.Clamp01(1f - t);
            _sr.color = c;
        }
        transform.localScale = Vector3.one * (1f + t * 1.5f);

        if (_elapsed >= _lifetime)
            Destroy(gameObject);
    }
}
