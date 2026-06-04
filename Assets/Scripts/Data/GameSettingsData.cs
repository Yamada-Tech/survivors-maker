using System;

[Serializable]
public class GameSettingsData
{
    public int TimeLimitSec = 300;
    public int PlayerMaxHp = 100;
    public float PlayerMoveSpeed = 4f;
    public float InvincibleSec = 0.8f;
    public float ExpMultiplier = 1f;
}
