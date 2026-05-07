using System.Collections.Generic;

[System.Serializable]
public class EnemyData
{
    public string Id;
    public string Name;
    public EnemyType Type;
    public int Hp = 30;
    public int Atk = 5;
    public float MoveSpeed = 2f;
    public float AttackRange = 6f;       // 攻撃射程（タイル単位）
    public float ShootCooldown = 2f;     // 射撃クールダウン（秒）
    public int ProjectileDamage = 8;     // 飛道具ダメージ（Ranged用）
    public float ProjectileSpeed = 6f;   // 飛道具速度（Ranged用）
    public float DropRate = 0.5f;       // 経験値ジェムドロップ率
    public int ExpValue = 10;
    public string SpriteId;             // 32×32
}

public enum EnemyType { Melee, Ranged, Explosive, Stationary }

[System.Serializable]
public class EnemyListData
{
    public List<EnemyData> Enemies = new();
}
