using UnityEngine;

/// <summary>
/// マップオブジェクトの通行可否設定。
/// このコンポーネントをアタッチしたオブジェクトは、
/// プレイヤー・敵それぞれに対して通行可否を設定できる。
/// 将来的にはマップエディタから配置可能にする予定。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapObject : MonoBehaviour
{
    [SerializeField] private bool _blockPlayer = true;
    [SerializeField] private bool _blockEnemy  = false;

    public bool BlockPlayer => _blockPlayer;
    public bool BlockEnemy  => _blockEnemy;

    private void Start()
    {
        ApplyCollisionConfig();
    }

    /// <summary>エディタ/SceneSetupから呼び出し用</summary>
    public void SetCollisionConfig(bool blockPlayer, bool blockEnemy)
    {
        _blockPlayer = blockPlayer;
        _blockEnemy  = blockEnemy;
        // Startより前に呼ばれる場合があるためここでは適用しない
        // Physics2D.IgnoreLayerCollision は SceneSetupEditor で一括設定する
    }

    private void ApplyCollisionConfig()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (playerLayer >= 0)
            col.excludeLayers = _blockPlayer
                ? col.excludeLayers & ~(1 << playerLayer)
                : col.excludeLayers | (1 << playerLayer);

        if (enemyLayer >= 0)
            col.excludeLayers = _blockEnemy
                ? col.excludeLayers & ~(1 << enemyLayer)
                : col.excludeLayers | (1 << enemyLayer);
    }
}
