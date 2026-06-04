using System;
using UnityEngine;

[Serializable]
public class MapSettingsData
{
    public int Width = 32;
    public int Height = 32;
    public float WallRatio = 0.2f;
    public Color WallColor = new Color(0.3f, 0.3f, 0.35f);
    public Color FloorColor = new Color(0.15f, 0.15f, 0.18f);
}
