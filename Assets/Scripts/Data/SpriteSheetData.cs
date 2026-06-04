using System;
using System.Collections.Generic;

[Serializable]
public class SpriteSheetData
{
    public const int DefaultFrameSize = 32;

    public string TextureGuid;
    public int FrameWidth = DefaultFrameSize;
    public int FrameHeight = DefaultFrameSize;
    public int Columns = 1;
    public int Rows = 1;
    public List<AnimationRowData> Animations = new();
}

[Serializable]
public class AnimationRowData
{
    public const int DefaultFps = 8;

    public string Name;
    public int StartFrame;
    public int FrameCount;
    public int Fps = DefaultFps;
}
