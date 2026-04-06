[System.Serializable]
public class PlayerData
{
    public string Id = "player_default";
    public string Name = "Hero";
    public int MaxHp = 100;
    public float MoveSpeed = 5f;        // タイル/秒 (1タイル = 32px)
    public int BaseAtk = 10;
    public string SpriteId = "player_sprite";  // 32×32
}
