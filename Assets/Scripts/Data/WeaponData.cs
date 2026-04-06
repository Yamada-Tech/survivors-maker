using System.Collections.Generic;

[System.Serializable]
public class WeaponData
{
    public string Id;
    public string Name;
    public WeaponType Type;
    public int Damage = 10;
    public float Cooldown = 1.0f;       // 秒
    public float Range = 2f;            // タイル単位
    public float ProjectileSpeed = 8f;  // 投射時のみ
    public string SpriteId;             // 32×32
}

public enum WeaponType { Melee, Projectile, Area }

[System.Serializable]
public class WeaponListData
{
    public List<WeaponData> Weapons = new();
}
