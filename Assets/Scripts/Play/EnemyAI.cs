using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    private EnemyData _data;
    private int _currentHp;
    private Transform _player;
    private Rigidbody2D _rb;

    public void Initialize(EnemyData data, Transform player)
    {
        _data = data;
        _currentHp = data.Hp;
        _player = player;
        _rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (_data == null || _player == null) return;

        switch (_data.Type)
        {
            case EnemyType.Melee:
                ChasePlayer();
                break;
            case EnemyType.Ranged:
                // TODO: 一定距離で止まって攻撃
                ChasePlayer();
                break;
            case EnemyType.Stationary:
                // 動かない
                break;
            default:
                ChasePlayer();
                break;
        }
    }

    private void ChasePlayer()
    {
        var dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * _data.MoveSpeed;
    }

    public void TakeDamage(int damage, Vector2 knockbackDir)
    {
        _currentHp -= damage;

        // ノックバック
        _rb.AddForce(knockbackDir.normalized * 3f, ForceMode2D.Impulse);

        if (_currentHp <= 0) Die();
    }

    private void Die()
    {
        EventBus.Publish(new EnemyKilledEvent
        {
            EnemyId = _data.Id.GetHashCode(),
            Position = transform.position,
            ExpValue = _data.ExpValue,
        });
        Destroy(gameObject);
    }

    private void OnCollisionStay2D(Collision2D col)
    {
        if (col.gameObject.TryGetComponent<PlayerController>(out var player))
        {
            player.TakeDamage(_data.Atk);
        }
    }
}
