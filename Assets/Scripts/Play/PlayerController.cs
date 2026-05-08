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
