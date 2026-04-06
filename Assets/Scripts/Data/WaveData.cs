using System.Collections.Generic;

[System.Serializable]
public class WaveEntry
{
    public int WaveNumber;
    public float StartTimeSec;          // ゲーム開始からの秒数
    public List<SpawnGroup> SpawnGroups = new();
}

[System.Serializable]
public class SpawnGroup
{
    public string EnemyId;
    public int Count = 5;
    public float SpawnInterval = 0.5f;  // 1体ずつ出す間隔
    public SpawnPosition Position;
}

public enum SpawnPosition { RandomEdge, North, South, East, West, Custom }

[System.Serializable]
public class WaveListData
{
    public List<WaveEntry> Waves = new();
}
