using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    private EnemyData _data;
    private int _currentHp;
    private float _shootTimer;
    private Transform _player;
    private GameObject _projectilePrefab;
    private Rigidbody2D _rb;
    private bool _frozen;

    private void Awake()
    {
        gameObject.tag = "PlayObject";
        _rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    public void Initialize(EnemyData data, Transform player, GameObject projectilePrefab = null)
    {
        _data = data;
        _currentHp = data.Hp;
        _shootTimer = 0f;
        _player = player;
        _projectilePrefab = projectilePrefab;
        _frozen = false;
    }

    private void FixedUpdate()
    {
        if (_frozen || _data == null || _player == null) return;

        switch (_data.Type)
        {
            case EnemyType.Melee:
                ChasePlayer();
                break;
            case EnemyType.Ranged:
                RangedBehavior();
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

    private void RangedBehavior()
    {
        float dist = Vector2.Distance(_player.position, transform.position);
        float fleeThreshold = _data.AttackRange * 0.6f;

        if (dist < fleeThreshold)
        {
            var fleeDir = ((Vector2)transform.position - (Vector2)_player.position).normalized;
            _rb.linearVelocity = fleeDir * _data.MoveSpeed;
        }
        else if (dist > _data.AttackRange)
        {
            ChasePlayer();
        }
        else
        {
            _rb.linearVelocity = Vector2.zero;
            _shootTimer -= Time.fixedDeltaTime;
            if (_shootTimer <= 0f)
            {
                ShootAtPlayer();
                _shootTimer = _data.ShootCooldown;
            }
        }
    }

    private void ShootAtPlayer()
    {
        if (_projectilePrefab == null) return;

        var dir = ((Vector2)_player.position - (Vector2)transform.position).normalized;
        var go = Instantiate(_projectilePrefab, transform.position, Quaternion.identity);
        var proj = go.GetComponent<EnemyProjectile>();
        if (proj != null)
            proj.Initialize(_data.ProjectileDamage, dir, _data.ProjectileSpeed, _data.AttackRange);
    }

    private void OnPlayerDied(PlayerDiedEvent _)
    {
        _frozen = true;
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;
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
