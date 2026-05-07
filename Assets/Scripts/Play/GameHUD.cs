using UnityEngine;
using UnityEngine.UIElements;

public class GameHUD : MonoBehaviour
{
    private Label _hpLabel;
    private Label _levelLabel;
    private Label _timerLabel;
    private Label _killLabel;
    private ProgressBar _hpBar;

    [SerializeField] private PlayerController _player;
    private AppStateMachine _stateMachine;
    private float _elapsed;
    private int _killCount;
    private int _displayLevel;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _hpLabel = root.Q<Label>("HpLabel");
        _levelLabel = root.Q<Label>("LevelLabel");
        _timerLabel = root.Q<Label>("TimerLabel");
        _killLabel = root.Q<Label>("KillLabel");
        _hpBar = root.Q<ProgressBar>("HpBar");

        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
        _stateMachine = AppStateMachine.Instance;
        if (_player != null)
            _displayLevel = _player.Level;

        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
    }

    private void Update()
    {
        if (_player == null) return;

        if (_stateMachine != null && _stateMachine.CurrentState == AppState.Play)
            _elapsed += Time.deltaTime;

        if (_hpLabel != null) _hpLabel.text = $"HP: {_player.CurrentHp}";
        if (_levelLabel != null) _levelLabel.text = $"Lv: {_displayLevel}";
        if (_timerLabel != null) _timerLabel.text = FormatTime(_elapsed);
        if (_killLabel != null) _killLabel.text = $"Kills: {_killCount}";

        if (_hpBar != null)
        {
            _hpBar.value = _player.CurrentHp;
            _hpBar.highValue = _player.MaxHp;
        }
    }

    private void OnEnemyKilled(EnemyKilledEvent _) => _killCount++;
    private void OnLevelUp(LevelUpEvent evt)
    {
        _displayLevel = evt.NewLevel;
    }

    private static string FormatTime(float t)
    {
        var min = Mathf.FloorToInt(t / 60f);
        var sec = Mathf.FloorToInt(t % 60f);
        return $"{min:00}:{sec:00}";
    }
}
