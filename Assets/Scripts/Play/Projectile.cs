using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    private int _damage;
    private float _maxDistance;
    private Vector3 _startPos;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(int damage, Vector2 direction, float speed, float range)
    {
        _damage = damage;
        _maxDistance = range;
        _startPos = transform.position;

        _rb.gravityScale = 0f;
        _rb.linearVelocity = direction.normalized * speed;
    }

    private void Update()
    {
        if (Vector3.Distance(_startPos, transform.position) >= _maxDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.TryGetComponent<EnemyAI>(out var enemy))
        {
            var knockDir = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
            enemy.TakeDamage(_damage, knockDir);
            Destroy(gameObject);
        }
    }
}
