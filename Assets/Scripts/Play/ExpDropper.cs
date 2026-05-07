using UnityEngine;

public class ExpDropper : MonoBehaviour
{
    [SerializeField] private GameObject _gemPrefab;

    private void OnEnable() => EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    private void OnDisable() => EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);

    private void OnEnemyKilled(EnemyKilledEvent evt)
    {
        var gem = Instantiate(_gemPrefab, evt.Position, Quaternion.identity);
        if (gem.TryGetComponent<ExpGem>(out var expGem))
            expGem.ExpValue = evt.ExpValue;
    }
}
