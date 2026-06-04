using UnityEngine;

/// <summary>
/// グリッド床と境界壁を生成するマップジェネレーター。
/// Setup Scene で自動生成される。インスペクタからサイズ変更可能。
/// </summary>
public class MapGenerator : MonoBehaviour
{
    private const int MinMapSize = 8;
    private const int MaxMapSize = 128;
    private const int WallLayerFallback = 11; // SceneSetupEditor の Wall レイヤー設定と同じインデックス

    [Header("マップサイズ（タイル数）")]
    [SerializeField] private int _mapWidth  = 120;  // 横タイル数
    [SerializeField] private int _mapHeight = 120;  // 縦タイル数

    [Header("色設定")]
    [SerializeField] private Color _floorColorA    = new Color(0.15f, 0.15f, 0.20f); // 床タイルA（暗）
    [SerializeField] private Color _floorColorB    = new Color(0.18f, 0.18f, 0.23f); // 床タイルB（明）チェック模様
    [SerializeField] private Color _wallColor      = new Color(0.35f, 0.20f, 0.10f); // 壁タイル
    [SerializeField] private Color _gridLineColor  = new Color(0.25f, 0.25f, 0.30f, 0.4f); // グリッド線（薄い）

    [Header("壁")]
    [SerializeField] private int _wallThickness = 2; // 壁の厚さ（タイル数）
    [SerializeField, Range(0f, 1f)] private float _wallRatio = 0.2f;

    public int MapWidth
    {
        get => _mapWidth;
        set => _mapWidth = Mathf.Clamp(value, MinMapSize, MaxMapSize);
    }

    public int MapHeight
    {
        get => _mapHeight;
        set => _mapHeight = Mathf.Clamp(value, MinMapSize, MaxMapSize);
    }

    public float WallRatio
    {
        get => _wallRatio;
        set => _wallRatio = Mathf.Clamp01(value);
    }

    public Color WallColor
    {
        get => _wallColor;
        set => _wallColor = value;
    }

    public Color FloorColor
    {
        get => _floorColorA;
        set
        {
            _floorColorA = value;
            _floorColorB = value;
        }
    }

    private void Start() => Generate();

    public void Generate()
    {
        // 既存の子オブジェクトを全削除
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child);
            else
#endif
                Destroy(child);
        }

        var sprite = GetDefaultSprite();

        // 床の中心オフセット（マップ中心が Vector3.zero になるよう）
        float offsetX = -(_mapWidth  - 1) * 0.5f;
        float offsetY = -(_mapHeight - 1) * 0.5f;

        for (int x = 0; x < _mapWidth; x++)
        {
            for (int y = 0; y < _mapHeight; y++)
            {
                bool isBoundaryWall = x < _wallThickness || x >= _mapWidth - _wallThickness ||
                                      y < _wallThickness || y >= _mapHeight - _wallThickness;
                bool isWall = isBoundaryWall || (!isBoundaryWall && Random.value < _wallRatio);

                var go  = new GameObject($"Tile_{x}_{y}");
                go.transform.SetParent(transform);
                go.transform.position = new Vector3(offsetX + x, offsetY + y, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;

                if (isWall)
                {
                    sr.color = _wallColor;
                    sr.sortingOrder = -9;
                    // 壁にColliderを追加
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one;
                    // MapObjectコンポーネントを追加（プレイヤーは壁で止まる、敵は通過）
                    var mapObj = go.AddComponent<MapObject>();
                    mapObj.SetCollisionConfig(blockPlayer: true, blockEnemy: false);

                    // Wall レイヤーを設定（Enemy・Projectileとの衝突をLayerで制御）
                    int wallLayer = LayerMask.NameToLayer("Wall");
                    if (wallLayer >= 0)
                        go.layer = wallLayer;
                    else
                        go.layer = WallLayerFallback; // フォールバック
                }
                else
                {
                    // チェック模様
                    sr.color = ((x + y) % 2 == 0) ? _floorColorA : _floorColorB;
                    sr.sortingOrder = -10;
                }
            }
        }

        Debug.Log($"[MapGenerator] Generated {_mapWidth}x{_mapHeight} map.");
    }

    /// <summary>マップのプレイ可能エリアの半サイズを返す（プレイヤーのクランプ等に利用可能）</summary>
    public Vector2 PlayableHalfSize => new Vector2(
        (_mapWidth  - _wallThickness * 2 - 1) * 0.5f,
        (_mapHeight - _wallThickness * 2 - 1) * 0.5f
    );

    private static Sprite GetDefaultSprite()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
#else
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
#endif
    }
}
