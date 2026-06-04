using System;
using System.Collections.Generic;

[Serializable]
public class SpriteSheetData
{
    public string TextureGuid;
    public int FrameWidth = 32;
    public int FrameHeight = 32;
    public int Columns = 1;
    public int Rows = 1;
    public List<AnimationRowData> Animations = new();
}

[Serializable]
public class AnimationRowData
{
    public string Name;
    public int StartFrame;
    public int FrameCount;
    public int Fps = 8;
}
