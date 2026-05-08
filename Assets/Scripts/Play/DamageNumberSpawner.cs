using UnityEngine;

/// <summary>
/// ダメージ数字を生成するスポナー。
/// EnemyAI と PlayerController から呼ばれる。
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [Header("表示設定")]
    [SerializeField] private bool _showEnemyDamage  = true;  // 敵に与えるダメージ
    [SerializeField] private bool _showPlayerDamage = true;  // プレイヤーが食らうダメージ

    [Header("色設定")]
    [SerializeField] private Color _enemyDamageColor  = Color.yellow;
    [SerializeField] private Color _playerDamageColor = Color.red;

    [Header("サイズ設定")]
    [SerializeField] private int   _enemyDamageFontSize  = 22;
    [SerializeField] private int   _playerDamageFontSize = 28;
    [SerializeField] private float _duration   = 0.9f;
    [SerializeField] private float _floatSpeed = 1.8f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>敵に与えたダメージ数字を表示（黄色）</summary>
    public void SpawnEnemyDamage(int damage, Vector3 worldPos)
    {
        if (!_showEnemyDamage) return;
        Spawn(damage.ToString(), _enemyDamageColor, worldPos, _enemyDamageFontSize);
    }

    /// <summary>プレイヤーが受けたダメージ数字を表示（赤）</summary>
    public void SpawnPlayerDamage(int damage, Vector3 worldPos)
    {
        if (!_showPlayerDamage) return;
        Spawn("-" + damage.ToString(), _playerDamageColor, worldPos, _playerDamageFontSize);
    }

    private void Spawn(string text, Color color, Vector3 worldPos, int fontSize)
    {
        // 少しランダムオフセット（複数の数字が重ならないように）
        var offset = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.4f), 0f);
        var go = new GameObject("DamageNumber");
        go.transform.position = worldPos + offset;
        var dn = go.AddComponent<DamageNumber>();
        dn.Initialize(text, color, _duration, _floatSpeed, fontSize);
    }
}
