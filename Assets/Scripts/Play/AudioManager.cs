using UnityEngine;

/// <summary>
/// BGM・SE を管理するシングルトン。
/// AudioClip はインスペクターで設定する（未設定でも動作）。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGMクリップ")]
    [SerializeField] private AudioClip _titleBgm;
    [SerializeField] private AudioClip _editorBgm;
    [SerializeField] private AudioClip _gameBgm;

    [Header("SEクリップ")]
    [SerializeField] private AudioClip _enemyKillSe;
    [SerializeField] private AudioClip _levelUpSe;
    [SerializeField] private AudioClip _playerDamageSe;
    [SerializeField] private AudioClip _gameOverSe;
    [SerializeField] private AudioClip _clearSe;
    [SerializeField] private AudioClip _buttonSe;

    [Header("音量設定")]
    [SerializeField][Range(0f, 1f)] private float _bgmVolume = 0.5f;
    [SerializeField][Range(0f, 1f)] private float _seVolume = 0.8f;

    private AudioSource _bgmSource;
    private AudioSource _seSource;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.volume = _bgmVolume;

        _seSource = gameObject.AddComponent<AudioSource>();
        _seSource.loop = false;
        _seSource.volume = _seVolume;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnStateChanged);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Subscribe<GameOverEvent>(OnGameOver);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnStateChanged);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- BGM ----

    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || _bgmSource == null) return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
        _bgmSource.clip = clip;
        _bgmSource.volume = _bgmVolume;
        _bgmSource.Play();
    }

    public void StopBgm()
    {
        _bgmSource?.Stop();
    }

    // ---- SE ----

    public void PlaySe(AudioClip clip)
    {
        if (clip == null || _seSource == null) return;
        _seSource.volume = _seVolume;
        _seSource.PlayOneShot(clip);
    }

    // ---- イベントハンドラ ----

    private void OnStateChanged(AppStateChangedEvent evt)
    {
        switch (evt.NewState)
        {
            case AppState.Title:
                PlayBgm(_titleBgm);
                break;
            case AppState.Editor:
                PlayBgm(_editorBgm);
                break;
            case AppState.Play:
                PlayBgm(_gameBgm);
                break;
        }
    }

    private void OnEnemyKilled(EnemyKilledEvent _) => PlaySe(_enemyKillSe);
    private void OnLevelUp(LevelUpEvent _) => PlaySe(_levelUpSe);
    private void OnPlayerDied(PlayerDiedEvent _) => PlaySe(_playerDamageSe);
    private void OnGameOver(GameOverEvent _) => PlaySe(_gameOverSe);
    private void OnTimeLimitReached(TimeLimitReachedEvent _) => PlaySe(_clearSe);
}
