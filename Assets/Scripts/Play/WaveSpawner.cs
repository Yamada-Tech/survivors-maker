using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform _player;

    private WaveListData _waveData;
    private EnemyListData _enemyListData;
    private float _elapsedTime;
    private int _currentWaveIndex;
    private bool _isRunning;

    public float ElapsedTime => _elapsedTime;
    public int CurrentWaveNumber => _currentWaveIndex;

    public void Initialize(WaveListData waveData, EnemyListData enemyData, Transform player)
    {
        _waveData = waveData;
        _enemyListData = enemyData;
        _player = player;
        _currentWaveIndex = 0;
        _elapsedTime = 0f;
        _isRunning = true;
    }

    public void StopSpawning() => _isRunning = false;

    private void Update()
    {
        if (!_isRunning || _waveData == null) return;

        _elapsedTime += Time.deltaTime;

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
        var enemyData = FindEnemyData(group.EnemyId);
        if (enemyData == null) yield break;

        for (int i = 0; i < group.Count; i++)
        {
            var pos = GetSpawnPosition(group.Position);
            var go = Instantiate(_enemyPrefab, pos, Quaternion.identity);
            var ai = go.GetComponent<EnemyAI>();
            ai.Initialize(enemyData, _player);

            yield return new WaitForSeconds(group.SpawnInterval);
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
        // カメラ外周からスポーン (仮: ±15タイル)
        float range = 15f;
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
