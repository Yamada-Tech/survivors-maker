using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerController _player;
    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private WeaponSystem _weaponSystem;

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
        var waveList = new WaveListData();

        // Wave 1: 0秒から開始
        var wave1 = new WaveEntry
        {
            StartTimeSec = 0f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01", Count = 5, SpawnInterval = 1f, Position = SpawnPosition.RandomEdge }
            }
        };

        // Wave 2: 20秒から
        var wave2 = new WaveEntry
        {
            StartTimeSec = 20f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01", Count = 8, SpawnInterval = 0.8f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 3, SpawnInterval = 2f, Position = SpawnPosition.North }
            }
        };

        // Wave 3: 45秒から
        var wave3 = new WaveEntry
        {
            StartTimeSec = 45f,
            SpawnGroups = new System.Collections.Generic.List<SpawnGroup>
            {
                new SpawnGroup { EnemyId = "enemy_melee_01", Count = 15, SpawnInterval = 0.5f, Position = SpawnPosition.RandomEdge },
                new SpawnGroup { EnemyId = "enemy_ranged_01", Count = 5, SpawnInterval = 1.5f, Position = SpawnPosition.South }
            }
        };

        waveList.Waves = new System.Collections.Generic.List<WaveEntry> { wave1, wave2, wave3 };
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
