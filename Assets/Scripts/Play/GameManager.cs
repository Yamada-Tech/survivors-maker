using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerController _player;
    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private WeaponSystem _weaponSystem;
    [Header("タイマー設定")]
    [SerializeField] private float _timeLimitSec = 1800f;
    [SerializeField] private bool _countDown = true;
    [SerializeField] private GameHUD _gameHUD;
    [Header("死亡演出設定")]
    [SerializeField] private float _deathDelaySec = 0.8f;

    private bool _gameStarted;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnStateChanged);
        EventBus.Subscribe<RestartRequestedEvent>(OnRestartRequested);
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        // Playモードで直接Playした場合は即ゲーム開始
        Invoke(nameof(StartGame), 0.1f);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnStateChanged);
        EventBus.Unsubscribe<RestartRequestedEvent>(OnRestartRequested);
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnStateChanged(AppStateChangedEvent evt)
    {
        if (evt.NewState == AppState.Play)
            StartGame();
    }

    public void StartGame()
    {
        if (_gameStarted || _player == null || _waveSpawner == null) return;
        _gameStarted = true;

        if (_gameHUD != null)
            _gameHUD.SetTimerConfig(_timeLimitSec, _countDown);

        // デフォルト武器を装備
        var defaultWeapon = new WeaponData
        {
            Id = "default_sword",
            Name = "はじめの剣",
            Type = WeaponType.Melee,
            Damage = 15,
            Cooldown = 0.8f,
            Range = 1.5f
        };
        _weaponSystem?.EquipWeapon(defaultWeapon);

        // デフォルトウェーブデータを生成して開始
        var waveList = TryLoadWaveData();
        var enemyList = TryLoadEnemyData();
        _waveSpawner.Initialize(waveList, enemyList, _player.transform);

        // パッシブデータをLevelUpUIに渡す
        var levelUpUI = FindAnyObjectByType<LevelUpUI>();
        if (levelUpUI != null)
        {
            var passiveList = TryLoadPassiveData();
            if (passiveList?.Passives != null && passiveList.Passives.Count > 0)
                levelUpUI.SetCustomPassives(passiveList.Passives);
        }

        Debug.Log("[GameManager] Game started!");
    }

    private void OnRestartRequested(RestartRequestedEvent _)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        StartCoroutine(DeathSequence(evt.ReachedLevel));
    }

    private IEnumerator DeathSequence(int reachedLevel)
    {
        yield return new WaitForSeconds(_deathDelaySec);

        EventBus.Publish(new GameOverEvent
        {
            SurvivedTimeSec = Mathf.FloorToInt(_waveSpawner != null ? _waveSpawner.ElapsedTime : 0f),
            KillCount = 0,
            ReachedLevel = reachedLevel,
        });

        AppStateMachine.Instance?.ChangeState(AppState.Editor);
    }

    private WaveListData TryLoadWaveData()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists("waves.json"))
        {
            var loaded = DataManager.Instance.Load<WaveListData>("waves.json");
            if (loaded?.Waves != null && loaded.Waves.Count > 0)
            {
                Debug.Log("[GameManager] WaveData loaded from JSON.");
                return loaded;
            }
        }
        Debug.Log("[GameManager] WaveData: using built-in defaults.");
        return CreateDefaultWaveData();
    }

    private EnemyListData TryLoadEnemyData()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists("enemies.json"))
        {
            var loaded = DataManager.Instance.Load<EnemyListData>("enemies.json");
            if (loaded?.Enemies != null && loaded.Enemies.Count > 0)
            {
                Debug.Log("[GameManager] EnemyData loaded from JSON.");
                return loaded;
            }
        }
        Debug.Log("[GameManager] EnemyData: using built-in defaults.");
        return CreateDefaultEnemyData();
    }

    private PassiveListData TryLoadPassiveData()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists("passives.json"))
        {
            var loaded = DataManager.Instance.Load<PassiveListData>("passives.json");
            if (loaded?.Passives != null && loaded.Passives.Count > 0)
            {
                Debug.Log("[GameManager] PassiveData loaded from JSON.");
                return loaded;
            }
        }
        Debug.Log("[GameManager] PassiveData: using built-in defaults.");
        return null;
    }

    private WaveListData CreateDefaultWaveData()
    {
        const float loopWaveIntervalSec = 30f;  // 30秒ごと
        const float scaleIntervalSec = 600f;    // 10分で2倍
        const float maxDifficultyScale = 3f;    // 最大3倍
        const float lateGameThresholdSec = 300f; // 残り5分

        var waveList = new WaveListData();
        var waves = new System.Collections.Generic.List<WaveEntry>();
        int loopWaveCount = Mathf.Max(1, Mathf.CeilToInt(_timeLimitSec / loopWaveIntervalSec));

        void AddEliteWave(float startTimeSec, System.Collections.Generic.List<SpawnGroup> spawnGroups)
        {
            if (startTimeSec > _timeLimitSec) return;
            waves.Add(new WaveEntry
            {
                StartTimeSec = startTimeSec,
                SpawnGroups = spawnGroups
            });
        }

        // --- 基本ループウェーブ (0秒〜ゲーム制限時間まで、30秒ごと) ---
        for (int i = 0; i < loopWaveCount; i++)
        {
            float t = i * loopWaveIntervalSec;
            float scale = 1f + (t / scaleIntervalSec);
            scale = Mathf.Clamp(scale, 1f, maxDifficultyScale);

            int meleeCount = Mathf.RoundToInt(6 * scale);
            float meleeInterval = Mathf.Max(0.2f, 0.8f / scale);

            if (_timeLimitSec - t <= lateGameThresholdSec)
                meleeCount *= 2;

            var groups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup
                {
                    EnemyId = "enemy_melee_01",
                    Count = meleeCount,
                    SpawnInterval = meleeInterval,
                    Position = SpawnPosition.RandomEdge
                }
            };

            // 60秒以降はRanged敵も追加
            if (t >= 60f)
            {
                int rangedCount = Mathf.RoundToInt(2 * scale);
                if (_timeLimitSec - t <= lateGameThresholdSec)
                    rangedCount *= 2;

                groups.Add(new SpawnGroup
                {
                    EnemyId = "enemy_ranged_01",
                    Count = rangedCount,
                    SpawnInterval = Mathf.Max(0.5f, 1.5f / scale),
                    Position = SpawnPosition.RandomEdge
                });
            }

            waves.Add(new WaveEntry { StartTimeSec = t, SpawnGroups = groups });
        }

        // --- エリートウェーブ（大量スポーン） ---
        AddEliteWave(600f, new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 30, SpawnInterval = 0.15f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 10, SpawnInterval = 0.5f,  Position = SpawnPosition.RandomEdge }
            });

        AddEliteWave(900f, new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 50, SpawnInterval = 0.1f,  Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 20, SpawnInterval = 0.3f,  Position = SpawnPosition.RandomEdge }
            });

        AddEliteWave(1200f, new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 80, SpawnInterval = 0.08f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 30, SpawnInterval = 0.2f,  Position = SpawnPosition.RandomEdge }
            });

        AddEliteWave(1500f, new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 120, SpawnInterval = 0.05f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 50,  SpawnInterval = 0.15f, Position = SpawnPosition.RandomEdge }
            });

        waveList.Waves = waves;
        return waveList;
    }

    private EnemyListData CreateDefaultEnemyData()
    {
        var list = new EnemyListData();
        list.Enemies = new System.Collections.Generic.List<EnemyData>
        {
            new EnemyData
            {
                Id = "enemy_melee_01",
                Name = "スライム",
                Type = EnemyType.Melee,
                Hp = 30,
                Atk = 5,
                MoveSpeed = 2.5f,
                ExpValue = 10,
                DropRate = 0.8f
            },
            new EnemyData
            {
                Id = "enemy_ranged_01",
                Name = "アーチャー",
                Type = EnemyType.Ranged,
                Hp = 20,
                Atk = 3,
                MoveSpeed = 1.5f,
                ExpValue = 15,
                DropRate = 0.6f,
                AttackRange = 3f,
                ShootCooldown = 2.5f,
                ProjectileDamage = 8,
                ProjectileSpeed = 5f
            }
        };
        return list;
    }

    public void ApplyTimeLimitSec(int timeLimitSec)
    {
        _timeLimitSec = Mathf.Clamp(timeLimitSec, 30, 3600);
        _gameHUD?.SetTimerConfig(_timeLimitSec, _countDown);
    }
}
