using System.Collections.Generic;
using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    [SerializeField] private Transform _player;
    [SerializeField] private GameObject _projectilePrefab;

    private readonly List<WeaponRuntime> _equipped = new();
    private bool _frozen;

    private void Awake()
    {
        gameObject.tag = "PlayObject";
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnGameEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnGameEnded);
    }

    private void OnPlayerDied(PlayerDiedEvent _) => _frozen = true;
    private void OnGameEnded(TimeLimitReachedEvent _) => _frozen = true;

    public void EquipWeapon(WeaponData data)
    {
        _equipped.Add(new WeaponRuntime { Data = data, CooldownTimer = 0f });
        EventBus.Publish(new WeaponEquippedEvent { WeaponId = data.Id });
    }

    private void Update()
    {
        if (_frozen) return;

        for (int i = 0; i < _equipped.Count; i++)
        {
            var w = _equipped[i];
            w.CooldownTimer -= Time.deltaTime;

            if (w.CooldownTimer <= 0f)
            {
                Fire(w);
                w.CooldownTimer = w.Data.Cooldown;
            }

            _equipped[i] = w;
        }
    }

    private void Fire(WeaponRuntime weapon)
    {
        var target = FindClosestEnemy();
        if (target == null) return;

        var dir = ((Vector2)target.position - (Vector2)_player.position).normalized;

        switch (weapon.Data.Type)
        {
            case WeaponType.Melee:
                MeleeAttack(weapon.Data, dir);
                break;
            case WeaponType.Projectile:
                ProjectileAttack(weapon.Data, dir);
                break;
            case WeaponType.Area:
                AreaAttack(weapon.Data);
                break;
        }
    }

    private void MeleeAttack(WeaponData data, Vector2 dir)
    {
        var hits = Physics2D.CircleCastAll(
            _player.position, data.Range, dir, 0f);

        foreach (var hit in hits)
        {
            if (hit.collider.TryGetComponent<EnemyAI>(out var enemy))
            {
                enemy.TakeDamage(data.Damage, dir);
            }
        }
    }

    private void ProjectileAttack(WeaponData data, Vector2 dir)
    {
        if (_projectilePrefab == null) return;

        var go = Instantiate(_projectilePrefab, _player.position, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(data.Damage, dir, data.ProjectileSpeed, data.Range);
        }
    }

    private void AreaAttack(WeaponData data)
    {
        var hits = Physics2D.OverlapCircleAll(_player.position, data.Range);
        foreach (var col in hits)
        {
            if (col.TryGetComponent<EnemyAI>(out var enemy))
            {
                var knockDir = ((Vector2)col.transform.position - (Vector2)_player.position).normalized;
                enemy.TakeDamage(data.Damage, knockDir);
            }
        }
    }

    private Transform FindClosestEnemy()
    {
        float minDist = float.MaxValue;
        Transform closest = null;
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

        foreach (var e in enemies)
        {
            float dist = Vector2.Distance(_player.position, e.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = e.transform;
            }
        }
        return closest;
    }

    private struct WeaponRuntime
    {
        public WeaponData Data;
        public float CooldownTimer;
    }
}
