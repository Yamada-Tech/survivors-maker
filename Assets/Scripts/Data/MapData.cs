using System.Collections.Generic;

[System.Serializable]
public class MapData
{
    public int Width = 32;              // タイル数
    public int Height = 32;
    public int TileSize = 32;           // px
    public List<TileLayer> Layers = new();
}

[System.Serializable]
public class TileLayer
{
    public string LayerName;            // "Ground", "Object", "Event"
    public int[] Tiles;                 // Width×Height の1次元配列 (タイルID)
}
