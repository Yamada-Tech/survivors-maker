using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    private const float MoveInputThreshold = 0.01f;

    [Header("データ")]
    [SerializeField] private PlayerData _data;

    [field: Header("ランタイム")]
    [field: SerializeField] public int CurrentHp { get; private set; }
    [field: SerializeField] public int Level { get; private set; } = 1;
    [field: SerializeField] public int Exp { get; private set; } = 0;
    [field: SerializeField] public int ExpToNext { get; private set; } = 100;
    public int MaxHp => _data != null ? _data.MaxHp + _maxHpBonus : 0;

    [Header("被弾設定")]
    [SerializeField] private GameObject _hitEffectPrefab;       // nullの場合はデフォルトエフェクト
    [SerializeField] private float _damageCooldown = 0.8f;      // 被弾後の無敵時間（秒）

    private Rigidbody2D _rb;
    private Vector2 _moveInput;
    private bool _dead;
    private float _damageCooldownTimer;
    private int _maxHpBonus;
    private float _moveSpeedMultiplier = 1f;
    private float _expMultiplier = 1f;
    private PlayerAnimator _animator;
    private bool _controlsEnabled;

    private void Awake()
    {
        _data ??= new PlayerData();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<PlayerAnimator>();
        CurrentHp = _data.MaxHp;
    }

    private void Start()
    {
        _animator ??= GetComponent<PlayerAnimator>();
        EventBus.Subscribe<AppStateChangedEvent>(OnAppStateChanged);
        _controlsEnabled = AppStateMachine.Instance != null && AppStateMachine.Instance.CurrentState == AppState.Play;
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnAppStateChanged);
    }

    private void Update()
    {
        if (_dead)
        {
            _moveInput = Vector2.zero;
            return;
        }

        if (!_controlsEnabled)
        {
            _moveInput = Vector2.zero;
            if (_animator != null)
                _animator.SetState(PlayerAnimator.AnimState.Idle);
            return;
        }

        // 無敵タイマーを更新
        if (_damageCooldownTimer > 0f)
            _damageCooldownTimer -= Time.deltaTime;

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

            if (kbInput.sqrMagnitude > MoveInputThreshold)
                _moveInput = kbInput.normalized;
        }

        if (_animator != null && !_dead)
        {
            _animator.SetFacing(_moveInput);
            _animator.SetState(_moveInput.sqrMagnitude > MoveInputThreshold
                ? PlayerAnimator.AnimState.Walk
                : PlayerAnimator.AnimState.Idle);
        }
    }

    private void FixedUpdate()
    {
        // 32px = 1タイル = 1 Unity unit
        _rb.linearVelocity = _moveInput * _data.MoveSpeed * _moveSpeedMultiplier;
    }

    private void OnAppStateChanged(AppStateChangedEvent evt)
    {
        _controlsEnabled = evt.NewState == AppState.Play;
        if (!_controlsEnabled)
        {
            _moveInput = Vector2.zero;
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;
        }
    }

    // ---- ダメージ / 経験値 ----

    public void TakeDamage(int amount)
    {
        if (_dead) return;

        // 無敵時間中はダメージを受けない
        if (_damageCooldownTimer > 0f) return;

        _damageCooldownTimer = _damageCooldown;

        // ダメージ数字
        DamageNumberSpawner.Instance?.SpawnPlayerDamage(amount, transform.position);

        _animator?.SetState(PlayerAnimator.AnimState.Hit);

        // 被弾エフェクト
        SpawnHitEffect(transform.position);

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (CurrentHp <= 0)
        {
            _dead = true;
            _animator?.SetState(PlayerAnimator.AnimState.Die);
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
        int boosted = Mathf.RoundToInt(amount * _expMultiplier);
        Exp += boosted;
        EventBus.Publish(new ExpGainedEvent { Amount = boosted, TotalExp = Exp });

        while (Exp >= ExpToNext)
        {
            Exp -= ExpToNext;
            Level++;
            ExpToNext = Mathf.RoundToInt(ExpToNext * 1.2f);
            EventBus.Publish(new LevelUpEvent { NewLevel = Level });
        }
    }

    public void AddMaxHp(int amount)
    {
        if (amount <= 0) return;
        int newMaxHp = MaxHp + amount;
        _maxHpBonus += amount;
        CurrentHp = Mathf.Min(CurrentHp + amount, newMaxHp);
    }

    public void Heal(int amount)
    {
        CurrentHp = Mathf.Min(CurrentHp + amount, MaxHp);
    }

    public void AddMoveSpeedMultiplier(float addRate)
    {
        _moveSpeedMultiplier += addRate;
    }

    public void ReduceDamageCooldown(float reduceSec)
    {
        _damageCooldown = Mathf.Max(0.1f, _damageCooldown - reduceSec);
    }

    public void AddExpMultiplier(float addRate)
    {
        _expMultiplier += addRate;
    }

    public void ApplyGameSettings(int maxHp, float moveSpeed, float invincibleSec, float expMultiplier)
    {
        if (_data == null)
        {
            Debug.LogWarning("[PlayerController] PlayerData is null.");
            return;
        }

        _data.MaxHp = Mathf.Clamp(maxHp, 1, 9999);
        _data.MoveSpeed = Mathf.Clamp(moveSpeed, 0.5f, 20f);
        _damageCooldown = Mathf.Clamp(invincibleSec, 0f, 5f);
        _expMultiplier = Mathf.Clamp(expMultiplier, 0.1f, 10f);
        CurrentHp = Mathf.Min(CurrentHp, MaxHp);
    }

    public void SetData(PlayerData data)
    {
        _data = data ?? new PlayerData();
        CurrentHp = _data.MaxHp;
    }
}
