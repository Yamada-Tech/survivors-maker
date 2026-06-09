using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private GameObject _enemyProjectilePrefab;
    [SerializeField] private Transform _player;
    [SerializeField] private float _earlyWaveDelay = 3f;

    private WaveListData _waveData;
    private EnemyListData _enemyListData;
    private float _elapsedTime;
    private int _currentWaveIndex;
    private bool _isRunning;
    private int _aliveEnemyCount;
    private int _activeSpawnCount;
    private bool _waitingEarlyWave;
    private float _earlyWaveTimer;

    public float ElapsedTime => _elapsedTime;
    public int CurrentWaveNumber => _currentWaveIndex;

    private void OnEnable()
    {
        EventBus.Subscribe<GameOverEvent>(OnGameEnded);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnGameEnded);
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameOverEvent>(OnGameEnded);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnGameEnded);
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    public void Initialize(WaveListData waveData, EnemyListData enemyData, Transform player)
    {
        _waveData = waveData;
        _enemyListData = enemyData;
        _player = player;
        _currentWaveIndex = 0;
        _elapsedTime = 0f;
        _isRunning = true;
        _aliveEnemyCount = 0;
        _activeSpawnCount = 0;
        _waitingEarlyWave = false;
        _earlyWaveTimer = 0f;
    }

    public void StopSpawning()
    {
        _isRunning = false;
        _activeSpawnCount = 0;
        _waitingEarlyWave = false;
    }

    private void Update()
    {
        if (!_isRunning || _waveData == null) return;

        _elapsedTime += Time.deltaTime;

        if (_waitingEarlyWave)
        {
            _earlyWaveTimer -= Time.deltaTime;
            if (_earlyWaveTimer <= 0f)
            {
                _waitingEarlyWave = false;
                if (_currentWaveIndex < (_waveData?.Waves.Count ?? 0))
                    _elapsedTime = _waveData.Waves[_currentWaveIndex].StartTimeSec;
            }
        }

        // 次のWaveの開始時刻を過ぎたら発動
        while (_currentWaveIndex < _waveData.Waves.Count &&
               _elapsedTime >= _waveData.Waves[_currentWaveIndex].StartTimeSec)
        {
            StartCoroutine(SpawnWave(_waveData.Waves[_currentWaveIndex]));
            _currentWaveIndex++;
        }
    }

    private IEnumerator SpawnWave(WaveEntry wave)
    {
        foreach (var group in wave.SpawnGroups)
        {
            StartCoroutine(SpawnGroup(group));
        }
        yield break;
    }

    private IEnumerator SpawnGroup(SpawnGroup group)
    {
        _activeSpawnCount++;
        try
        {
            var enemyData = FindEnemyData(group.EnemyId);
            if (enemyData == null) yield break;

            for (int i = 0; i < group.Count; i++)
            {
                if (!_isRunning)
                    yield break;

                var pos = GetSpawnPosition(group.Position);
                var go = Instantiate(_enemyPrefab, pos, Quaternion.identity);
                var ai = go.GetComponent<EnemyAI>();
                if (ai == null)
                {
                    Debug.LogError("[WaveSpawner] EnemyAI component not found on prefab.");
                    Destroy(go);
                    yield break;
                }
                ai.Initialize(enemyData, _player, _enemyProjectilePrefab);
                _aliveEnemyCount++;

                yield return new WaitForSeconds(group.SpawnInterval);
            }
        }
        finally
        {
            _activeSpawnCount = Mathf.Max(0, _activeSpawnCount - 1);
        }
    }

    private void OnGameEnded(GameOverEvent _) => StopSpawning();
    private void OnGameEnded(TimeLimitReachedEvent _) => StopSpawning();
    private void OnPlayerDied(PlayerDiedEvent _) => StopSpawning();

    private void OnEnemyKilled(EnemyKilledEvent _)
    {
        _aliveEnemyCount = Mathf.Max(0, _aliveEnemyCount - 1);

        if (_aliveEnemyCount == 0 && _activeSpawnCount == 0 && _isRunning &&
            _currentWaveIndex < (_waveData?.Waves.Count ?? 0) &&
            !_waitingEarlyWave)
        {
            float nextWaveTime = _waveData.Waves[_currentWaveIndex].StartTimeSec;
            if (nextWaveTime - _elapsedTime > _earlyWaveDelay)
            {
                _waitingEarlyWave = true;
                _earlyWaveTimer = _earlyWaveDelay;
            }
        }
    }

    private EnemyData FindEnemyData(string enemyId)
    {
        foreach (var e in _enemyListData.Enemies)
        {
            if (e.Id == enemyId) return e;
        }
        Debug.LogWarning($"[WaveSpawner] EnemyId not found: {enemyId}");
        return null;
    }

    private Vector3 GetSpawnPosition(SpawnPosition position)
    {
        // カメラ外周からスポーン (±18タイル)
        float range = 18f;
        var playerPos = _player != null ? _player.position : Vector3.zero;

        switch (position)
        {
            case SpawnPosition.North:
                return playerPos + new Vector3(Random.Range(-range, range), range, 0);
            case SpawnPosition.South:
                return playerPos + new Vector3(Random.Range(-range, range), -range, 0);
            case SpawnPosition.East:
                return playerPos + new Vector3(range, Random.Range(-range, range), 0);
            case SpawnPosition.West:
                return playerPos + new Vector3(-range, Random.Range(-range, range), 0);
            case SpawnPosition.RandomEdge:
            default:
                int side = Random.Range(0, 4);
                return side switch
                {
                    0 => playerPos + new Vector3(Random.Range(-range, range), range, 0),
                    1 => playerPos + new Vector3(Random.Range(-range, range), -range, 0),
                    2 => playerPos + new Vector3(range, Random.Range(-range, range), 0),
                    _ => playerPos + new Vector3(-range, Random.Range(-range, range), 0),
                };
        }
    }
}
