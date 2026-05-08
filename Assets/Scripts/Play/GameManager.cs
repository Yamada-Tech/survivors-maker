using UnityEngine;

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

    private bool _gameStarted;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnStateChanged);
        // Playモードで直接Playした場合は即ゲーム開始
        Invoke(nameof(StartGame), 0.1f);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnStateChanged);
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
        var waveList = CreateDefaultWaveData();
        var enemyList = CreateDefaultEnemyData();
        _waveSpawner.Initialize(waveList, enemyList, _player.transform);

        Debug.Log("[GameManager] Game started!");
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
        waves.Add(new WaveEntry
        {
            StartTimeSec = 600f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 30, SpawnInterval = 0.15f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 10, SpawnInterval = 0.5f,  Position = SpawnPosition.RandomEdge }
            }
        });

        waves.Add(new WaveEntry
        {
            StartTimeSec = 900f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 50, SpawnInterval = 0.1f,  Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 20, SpawnInterval = 0.3f,  Position = SpawnPosition.RandomEdge }
            }
        });

        waves.Add(new WaveEntry
        {
            StartTimeSec = 1200f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 80, SpawnInterval = 0.08f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 30, SpawnInterval = 0.2f,  Position = SpawnPosition.RandomEdge }
            }
        });

        waves.Add(new WaveEntry
        {
            StartTimeSec = 1500f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01",  Count = 120, SpawnInterval = 0.05f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 50,  SpawnInterval = 0.15f, Position = SpawnPosition.RandomEdge }
            }
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
                AttackRange = 6f,
                ShootCooldown = 2f,
                ProjectileDamage = 8,
                ProjectileSpeed = 6f
            }
        };
        return list;
    }
}
