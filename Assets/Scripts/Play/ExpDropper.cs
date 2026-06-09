using UnityEngine;

public class ExpDropper : MonoBehaviour
{
    [SerializeField] private GameObject _expGemPrefab;

    public void Configure(GameObject expGemPrefab)
    {
        _expGemPrefab = expGemPrefab;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        if (_expGemPrefab == null) return;
        // ドロップ率は EnemyKilledEvent に含まれないため常にドロップ（シンプル化）
        var go = Instantiate(_expGemPrefab, evt.Position, Quaternion.identity);
        if (!go.activeSelf)
            go.SetActive(true);
    }
}
