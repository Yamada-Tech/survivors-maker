using UnityEngine;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private PlayerController _player;

    private float _elapsed;
    private int _killCount;
    private int _displayLevel = 1;
    private bool _gameOver;
    private string _gameOverText;

    private GUIStyle _labelStyle;
    private GUIStyle _gameOverStyle;

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Subscribe<GameOverEvent>(OnGameOver);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
    }

    private void Start()
    {
        if (_player == null)
            _player = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (!_gameOver)
            _elapsed += Time.deltaTime;
    }

    private void OnGUI()
    {
        // スタイル初期化（初回のみ）
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _gameOverStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red }
            };
        }

        if (_gameOver)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), _gameOverText, _gameOverStyle);
            return;
        }

        if (_player == null) return;

        int min = Mathf.FloorToInt(_elapsed / 60f);
        int sec = Mathf.FloorToInt(_elapsed % 60f);

        // 背景（半透明黒）
        GUI.color = new Color(0, 0, 0, 0.45f);
        GUI.DrawTexture(new Rect(8, 8, 260, 110), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(16, 12,  250, 30), $"❤  HP   {_player.CurrentHp} / {_player.MaxHp}", _labelStyle);
        GUI.Label(new Rect(16, 40,  250, 30), $"⭐ Lv   {_displayLevel}", _labelStyle);
        GUI.Label(new Rect(16, 68,  250, 30), $"⏱  {min:00}:{sec:00}   💀 {_killCount}", _labelStyle);
    }

    private void OnEnemyKilled(EnemyKilledEvent _) => _killCount++;

    private void OnLevelUp(LevelUpEvent evt) => _displayLevel = evt.NewLevel;

    private void OnGameOver(GameOverEvent evt)
    {
        Time.timeScale = 1f;
        _gameOver = true;
        int survived = Mathf.FloorToInt(_elapsed);
        _gameOverText = $"GAME OVER\n\nTime: {survived / 60:00}:{survived % 60:00}   Kills: {_killCount}   Lv: {evt.ReachedLevel}";
    }
}
