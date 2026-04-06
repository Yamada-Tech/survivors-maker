using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("データ")]
    [SerializeField] private PlayerData _data;

    [Header("ランタイム")]
    public int CurrentHp;
    public int Level = 1;
    public int Exp = 0;
    public int ExpToNext = 100;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        CurrentHp = _data.MaxHp;
    }

    private void Update()
    {
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
        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (CurrentHp <= 0)
        {
            // TODO: ゲームオーバー処理
            Debug.Log("[Player] Dead");
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
