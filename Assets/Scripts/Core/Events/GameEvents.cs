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

// エディタ全体保存要求
public struct SaveAllRequestedEvent
{
}

// プレイモード開始
public struct PlayModeStartedEvent
{
}

// エディタモード復帰
public struct EditorModeRestoredEvent
{
}

// ゲームオーバー
public struct GameOverEvent
{
    public int SurvivedTimeSec;
    public int KillCount;
    public int ReachedLevel;
}

// プレイヤー死亡（演出フェーズ開始）
public struct PlayerDiedEvent
{
    public int ReachedLevel;
}

// 制限時間終了（クリア）
public struct TimeLimitReachedEvent
{
    public int SurvivedTimeSec;
    public int KillCount;
    public int ReachedLevel;
}

// リスタート要求
public struct RestartRequestedEvent
{
}
