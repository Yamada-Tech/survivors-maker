using UnityEngine;

/// <summary>
/// パッシブ効果を適用するシステム。
/// LevelUpUI から ApplyPassive() を呼び出す。
/// </summary>
public class PassiveSystem : MonoBehaviour
{
    public static PassiveSystem Instance { get; private set; }

    private PlayerController _player;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _player = FindAnyObjectByType<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ApplyPassive(PassiveData data)
    {
        if (_player == null)
            _player = FindAnyObjectByType<PlayerController>();
        if (_player == null || data == null) return;

        switch (data.Type)
        {
            case PassiveType.MaxHpUp:
                _player.AddMaxHp(Mathf.RoundToInt(data.Value));
                break;
            case PassiveType.HpRecover:
                _player.Heal(Mathf.RoundToInt(data.Value));
                break;
            case PassiveType.MoveSpeedUp:
                _player.AddMoveSpeedMultiplier(data.Value);
                break;
            case PassiveType.DamageCooldownDown:
                _player.ReduceDamageCooldown(data.Value);
                break;
            case PassiveType.ExpBonus:
                _player.AddExpMultiplier(data.Value);
                break;
        }

        Debug.Log($"[PassiveSystem] Applied: {data.Name}");
    }
}
