using System.Collections.Generic;
using UnityEngine;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private float _timeLimitSec = 1800f;
    [SerializeField] private bool _countDown = true;
    [Header("EXP表示設定")]
    [SerializeField] private bool _showExpBar = true;
    [SerializeField] private bool _showExpNumbers = true;
    [SerializeField] private int _expFontSize = 20;
    [SerializeField] private Vector2 _expBarSize = new Vector2(236f, 14f);
    [SerializeField] private Color _expBarBackgroundColor = new(0.2f, 0.1f, 0.3f, 1f);
    [SerializeField] private Color _expBarFillColor = new(0.75f, 0.4f, 1f, 1f);

    private float _elapsed;
    private int _killCount;
    private int _displayLevel = 1;
    private bool _gameOver;
    private bool _clearMode;
    private bool _timeLimitReached;
    private string _gameOverText;
    private int _survivedSec;
    private int _finalKillCount;
    private int _finalLevel;
    private int _maxDamageDealt;
    private bool _isVisible;

    private GUIStyle _labelStyle;
    private GUIStyle _expLabelStyle;
    private GUIStyle _gameOverStyle;
    private GUIStyle _clearStyle;
    private GUIStyle _restartButtonStyle;
    private GUIStyle _resultTitleStyle;
    private GUIStyle _resultStatStyle;
    private GUIStyle _resultSubStyle;
    private GUIStyle _resultClearTitleStyle;
    private GUIStyle _resultGameOverTitleStyle;
    private GUIStyle _resultValueStyle;
    private bool _resultStylesInit;
    private readonly List<Texture2D> _resultTextures = new();

    private void OnEnable()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnAppStateChanged);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Subscribe<GameOverEvent>(OnGameOver);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnAppStateChanged);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void Start()
    {
        if (_player == null)
            _player = FindAnyObjectByType<PlayerController>();
        _isVisible = AppStateMachine.Instance != null && AppStateMachine.Instance.CurrentState == AppState.Play;
    }

    private void Update()
    {
        if (!_isVisible || _gameOver) return;

        _elapsed += Time.deltaTime;

        if (_countDown && !_timeLimitReached && _elapsed >= _timeLimitSec)
        {
            _timeLimitReached = true;
            _elapsed = _timeLimitSec;
            EventBus.Publish(new TimeLimitReachedEvent
            {
                SurvivedTimeSec = Mathf.FloorToInt(_timeLimitSec),
                KillCount = _killCount,
                ReachedLevel = _displayLevel
            });
        }
    }

    private void OnGUI()
    {
        if (!_isVisible)
            return;

        // スタイル初期化（初回のみ）
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _expLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = _expFontSize
            };
        }

        if (_gameOver)
        {
            DrawResultPanel();
            return;
        }

        if (_player == null) return;

        float displayTime = _countDown ? Mathf.Max(0f, _timeLimitSec - _elapsed) : _elapsed;
        int min = Mathf.FloorToInt(displayTime / 60f);
        int sec = Mathf.FloorToInt(displayTime % 60f);
        const float hudHeightDefault = 110f;
        const float hudHeightWithExpMin = 145f;
        float hudHeight = hudHeightDefault;

        if (_showExpBar || _showExpNumbers)
        {
            float expBottom = 0f;
            if (_showExpNumbers)
                expBottom = Mathf.Max(expBottom, 96f + Mathf.Max(24f, _expFontSize + 4f));
            if (_showExpBar)
                expBottom = Mathf.Max(expBottom, 122f + _expBarSize.y);
            hudHeight = Mathf.Max(hudHeightWithExpMin, expBottom + 9f);
        }

        // 背景（半透明黒）
        GUI.color = new Color(0, 0, 0, 0.45f);
        GUI.DrawTexture(new Rect(8, 8, 260, hudHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(16, 12,  250, 30), $"❤  HP   {_player.CurrentHp} / {_player.MaxHp}", _labelStyle);
        GUI.Label(new Rect(16, 40,  250, 30), $"⭐ Lv   {_displayLevel}", _labelStyle);
        GUI.Label(new Rect(16, 68,  250, 30), $"⏱  {min:00}:{sec:00}   💀 {_killCount}", _labelStyle);

        // EXP表示
        if (_showExpBar || _showExpNumbers)
        {
            float expRatio = _player.ExpToNext > 0
                ? Mathf.Clamp01((float)_player.Exp / _player.ExpToNext)
                : 0f;

            if (_showExpNumbers)
            {
                GUI.Label(new Rect(16, 96, 250, 24),
                    $"✨ EXP  {_player.Exp} / {_player.ExpToNext}", _expLabelStyle);
            }

            if (_showExpBar)
            {
                GUI.color = _expBarBackgroundColor;
                GUI.DrawTexture(new Rect(16, 122, _expBarSize.x, _expBarSize.y), Texture2D.whiteTexture);

                GUI.color = _expBarFillColor;
                GUI.DrawTexture(new Rect(16, 122, _expBarSize.x * expRatio, _expBarSize.y), Texture2D.whiteTexture);

                GUI.color = Color.white;
            }
        }
    }

    private void OnEnemyKilled(EnemyKilledEvent _) => _killCount++;

    private void OnLevelUp(LevelUpEvent evt) => _displayLevel = evt.NewLevel;

    private void OnGameOver(GameOverEvent evt)
    {
        _gameOver = true;
        _clearMode = false;
        _survivedSec = Mathf.FloorToInt(_elapsed);
        _finalKillCount = _killCount;
        _finalLevel = evt.ReachedLevel;
    }

    private void OnTimeLimitReached(TimeLimitReachedEvent evt)
    {
        _gameOver = true;
        _clearMode = true;
        _survivedSec = evt.SurvivedTimeSec;
        _finalKillCount = evt.KillCount;
        _finalLevel = evt.ReachedLevel;
    }

    private void OnAppStateChanged(AppStateChangedEvent evt)
    {
        _isVisible = evt.NewState == AppState.Play;
        if (_isVisible)
        {
            _elapsed = 0f;
            _killCount = 0;
            _displayLevel = _player != null ? _player.Level : 1;
            _gameOver = false;
            _clearMode = false;
            _timeLimitReached = false;
        }
    }

    public void SetTimerConfig(float timeLimitSec, bool countDown)
    {
        _timeLimitSec = timeLimitSec;
        _countDown = countDown;
        _timeLimitReached = false;
    }

    private void DrawResultPanel()
    {
        InitResultStyles();

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float panelW = 540f;
        const float panelH = 400f;
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        GUI.color = new Color(0.08f, 0.08f, 0.14f, 0.98f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string title = _clearMode ? "🎉 STAGE CLEAR!" : "💀 GAME OVER";
        _resultTitleStyle = _clearMode ? _resultClearTitleStyle : _resultGameOverTitleStyle;
        GUI.Label(new Rect(px, py + 16f, panelW, 50f), title, _resultTitleStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.DrawTexture(new Rect(px + 20f, py + 70f, panelW - 40f, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float rowY = py + 86f;
        const float rowH = 44f;
        float labelX = px + 40f;
        float valueX = px + 300f;
        const float valueW = 200f;

        int min = _survivedSec / 60;
        int sec = _survivedSec % 60;

        DrawStatRow(labelX, valueX, valueW, rowY, "⏱  生存時間", $"{min:00}:{sec:00}");
        DrawStatRow(labelX, valueX, valueW, rowY + rowH, "💀 キル数", $"{_finalKillCount}");
        DrawStatRow(labelX, valueX, valueW, rowY + rowH * 2f, "⭐ 到達レベル", $"Lv. {_finalLevel}");
        DrawStatRow(labelX, valueX, valueW, rowY + rowH * 3f, "⚡ 最大ダメージ", $"{_maxDamageDealt}");

        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.DrawTexture(new Rect(px + 20f, py + 278f, panelW - 40f, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(px, py + 282f, panelW, 24f), "RESULT", _resultSubStyle);

        InitRestartButtonStyle();
        const float btnW = 280f;
        const float btnH = 56f;
        float btnX = (Screen.width - btnW) * 0.5f;
        float btnY = py + panelH - btnH - 24f;
        if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "🔄 もう一度プレイ", _restartButtonStyle))
            EventBus.Publish(new RestartRequestedEvent());
    }

    private void DrawStatRow(float labelX, float valueX, float valueW, float y, string label, string value)
    {
        GUI.Label(new Rect(labelX, y, 260f, 38f), label, _resultStatStyle);
        GUI.Label(new Rect(valueX, y, valueW, 38f), value, _resultValueStyle);
    }

    private void InitResultStyles()
    {
        if (_resultStylesInit) return;
        _resultStylesInit = true;

        _resultClearTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0f) }
        };
        _resultGameOverTitleStyle = new GUIStyle(_resultClearTitleStyle)
        {
            normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
        };
        _resultStatStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.75f, 0.75f, 0.85f) }
        };
        _resultValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = Color.white }
        };
        _resultSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.7f, 0.7f, 0.8f, 0.9f) }
        };
    }

    private void InitRestartButtonStyle()
    {
        if (_restartButtonStyle != null) return;
        _restartButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            hover = { textColor = Color.yellow },
        };
    }
}
