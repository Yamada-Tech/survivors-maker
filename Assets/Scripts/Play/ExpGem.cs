using UnityEngine;

public class ExpGem : MonoBehaviour
{
    public int ExpValue = 10;
    private float _magnetSpeed = 8f;
    private Transform _player;

    private void Start()
    {
        _player = FindFirstObjectByType<PlayerController>()?.transform;
    }

    private void Update()
    {
        if (_player == null) return;
        var dist = Vector2.Distance(transform.position, _player.position);
        if (dist < 2f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position, _player.position, _magnetSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.TryGetComponent<PlayerController>(out var player))
        {
            player.GainExp(ExpValue);
            Destroy(gameObject);
        }
    }
}
