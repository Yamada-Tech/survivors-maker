using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("データ")]
    [SerializeField] private PlayerData _data;

    [field: Header("ランタイム")]
    [field: SerializeField] public int CurrentHp { get; private set; }
    [field: SerializeField] public int Level { get; private set; } = 1;
    [field: SerializeField] public int Exp { get; private set; } = 0;
    [field: SerializeField] public int ExpToNext { get; private set; } = 100;
    public int MaxHp => _data != null ? _data.MaxHp : 0;

    [Header("被弾エフェクト")]
    [SerializeField] private GameObject _hitEffectPrefab; // nullの場合はデフォルトエフェクト

    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private bool _dead;

    private void Awake()
    {
        gameObject.tag = "PlayObject";
        _rb = GetComponent<Rigidbody2D>();
        CurrentHp = _data.MaxHp;
    }

    private void Update()
    {
        if (_dead)
        {
            _moveInput = Vector2.zero;
            return;
        }

        // --- Input System (Keyboard + Gamepad) ---
        var gp = Gamepad.current;
        var kb = Keyboard.current;

        _moveInput = Vector2.zero;

        // ゲームパッド優先
        if (gp != null)
            _moveInput = gp.leftStick.ReadValue();

        // キーボード上書き
        if (kb != null)
        {
            var kbInput = Vector2.zero;
            if (kb.wKey.isPressed) kbInput.y += 1;
            if (kb.sKey.isPressed) kbInput.y -= 1;
            if (kb.aKey.isPressed) kbInput.x -= 1;
            if (kb.dKey.isPressed) kbInput.x += 1;

            if (kbInput.sqrMagnitude > 0.01f)
                _moveInput = kbInput.normalized;
        }
    }

    private void FixedUpdate()
    {
        // 32px = 1タイル = 1 Unity unit
        _rb.linearVelocity = _moveInput * _data.MoveSpeed;
    }

    // ---- ダメージ / 経験値 ----

    public void TakeDamage(int amount)
    {
        if (_dead) return;

        // ダメージ数字
        DamageNumberSpawner.Instance?.SpawnPlayerDamage(amount, transform.position);

        // 被弾エフェクト
        SpawnHitEffect(transform.position);

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (CurrentHp <= 0)
        {
            _dead = true;
            _moveInput = Vector2.zero;
            EventBus.Publish(new PlayerDiedEvent
            {
                ReachedLevel = Level,
            });
        }
    }

    private void SpawnHitEffect(Vector3 pos)
    {
        if (_hitEffectPrefab != null)
        {
            Instantiate(_hitEffectPrefab, pos, Quaternion.identity);
            return;
        }

        // デフォルトエフェクト（赤いスプライト、コード生成）
        var go = new GameObject("HitEffect_Default");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.8f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDefaultSprite();
        sr.color  = new Color(1f, 0.1f, 0.1f, 0.85f);
        sr.sortingOrder = 10;

        go.AddComponent<HitEffect>();
    }

    private static Sprite _defaultSprite;

    private static Sprite GetDefaultSprite()
    {
        if (_defaultSprite != null) return _defaultSprite;
#if UNITY_EDITOR
        _defaultSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
#else
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _defaultSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
#endif
        return _defaultSprite;
    }

    public void GainExp(int amount)
    {
        Exp += amount;
        EventBus.Publish(new ExpGainedEvent { Amount = amount, TotalExp = Exp });

        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            ExpToNext = Mathf.RoundToInt(ExpToNext * 1.2f);
            EventBus.Publish(new LevelUpEvent { NewLevel = Level });
        }
    }
}
