// アプリ状態変更
public struct AppStateChangedEvent
{
    public AppState OldState;
    public AppState NewState;
    public AppStateChangedEvent(AppState old, AppState @new) { OldState = old; NewState = @new; }
}

// 敵撃破
public struct EnemyKilledEvent
{
    public int EnemyId;
    public UnityEngine.Vector3 Position;
    public int ExpValue;
}

// 経験値獲得
public struct ExpGainedEvent
{
    public int Amount;
    public int TotalExp;
}

// レベルアップ
public struct LevelUpEvent
{
    public int NewLevel;
}

// 武器装備変更
public struct WeaponEquippedEvent
{
    public string WeaponId;
}

// MAP保存
public struct MapSavedEvent
{
    public string FilePath;
}

// データ保存
public struct DataSavedEvent
{
    public string FileName;
}
