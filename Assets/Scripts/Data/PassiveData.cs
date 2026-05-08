using System.Collections.Generic;

[System.Serializable]
public class PassiveData
{
    public string Id;
    public string Name;
    public string Description;
    public PassiveType Type;
    public float Value;
}

public enum PassiveType
{
    MaxHpUp,
    HpRecover,
    MoveSpeedUp,
    DamageCooldownDown,
    ExpBonus,
}

[System.Serializable]
public class PassiveListData
{
    public List<PassiveData> Passives = new();
}
