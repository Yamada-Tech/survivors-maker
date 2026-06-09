using System.Collections.Generic;
using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    private const int EnemyLayer = 9;

    [SerializeField] private Transform _player;
    [SerializeField] private GameObject _projectilePrefab;

    private readonly List<WeaponRuntime> _equipped = new();
    private bool _frozen;
    private int _enemyLayerMask;
    private WeaponRangeIndicator _rangeIndicator;

    private static readonly Color MeleeIndicatorColor = new(1f, 0.3f, 0.3f);
    private static readonly Color RangedIndicatorColor = new(0.3f, 0.8f, 1f);
    private static readonly Color AreaIndicatorColor = new(1f, 0.8f, 0.2f);

    private void Awake()
    {
        _enemyLayerMask = 1 << EnemyLayer;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnAppStateChanged);
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnGameEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnAppStateChanged);
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnGameEnded);
    }

    private void OnAppStateChanged(AppStateChangedEvent evt) => UpdateFrozenState(evt.NewState);
    private void OnPlayerDied(PlayerDiedEvent _) => _frozen = true;
    private void OnGameEnded(TimeLimitReachedEvent _) => _frozen = true;

    private void Start()
    {
        UpdateFrozenState(AppStateMachine.Instance?.CurrentState ?? AppState.Title);
        if (_rangeIndicator == null)
            InitRangeIndicator();
    }

    public void Configure(Transform player, GameObject projectilePrefab)
    {
        _player = player;
        _projectilePrefab = projectilePrefab;
    }

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
        if (_player == null) return;

        var target = FindClosestEnemy();
        if (target == null) return;

        var dir = ((Vector2)target.position - (Vector2)_player.position).normalized;

        switch (weapon.Data.Type)
        {
            case WeaponType.Melee:
                _rangeIndicator?.Show(weapon.Data.Range, MeleeIndicatorColor);
                MeleeAttack(weapon.Data, dir);
                break;
            case WeaponType.Projectile:
                _rangeIndicator?.Show(weapon.Data.Range, RangedIndicatorColor);
                ProjectileAttack(weapon.Data, dir);
                break;
            case WeaponType.Area:
                _rangeIndicator?.Show(weapon.Data.Range, AreaIndicatorColor);
                AreaAttack(weapon.Data);
                break;
        }
    }

    private void MeleeAttack(WeaponData data, Vector2 dir)
    {
        // CircleCastAll(distance=0)は機能しないためOverlapCircleAllを使用
        var hits = Physics2D.OverlapCircleAll(_player.position, data.Range, _enemyLayerMask);
        foreach (var col in hits)
        {
            if (col.TryGetComponent<EnemyAI>(out var enemy))
            {
                enemy.TakeDamage(data.Damage, dir);
            }
        }

        SpawnRangeEffect(_player.position, data.Range, new Color(1f, 1f, 0.3f, 0.8f));
    }

    private void ProjectileAttack(WeaponData data, Vector2 dir)
    {
        if (_projectilePrefab == null) return;

        var go = Instantiate(_projectilePrefab, _player.position, Quaternion.identity);
        if (!go.activeSelf)
            go.SetActive(true);
        var proj = go.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(data.Damage, dir, data.ProjectileSpeed, data.Range);
        }
    }

    private void AreaAttack(WeaponData data)
    {
        var hits = Physics2D.OverlapCircleAll(_player.position, data.Range, _enemyLayerMask);
        foreach (var col in hits)
        {
            if (col.TryGetComponent<EnemyAI>(out var enemy))
            {
                var knockDir = ((Vector2)col.transform.position - (Vector2)_player.position).normalized;
                enemy.TakeDamage(data.Damage, knockDir);
            }
        }

        SpawnRangeEffect(_player.position, data.Range, new Color(1f, 0.5f, 0f, 0.8f));
    }

    private void SpawnRangeEffect(Vector3 pos, float radius, Color color)
    {
        GameObject go = new GameObject("AttackRangeEffect");
        go.transform.position = pos;
        var effect = go.AddComponent<AttackRangeEffect>();
        effect.Initialize(radius, color, 0.35f);
    }

    private Transform FindClosestEnemy()
    {
        if (_player == null) return null;

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

    private void InitRangeIndicator()
    {
        if (_player == null)
            Debug.LogWarning("[WeaponSystem] _player is null. WeaponRangeIndicator will be parented to WeaponSystem.");

        var parent = _player != null ? _player : transform;
        var indicatorGo = new GameObject("WeaponRangeIndicator");
        indicatorGo.transform.SetParent(parent);
        indicatorGo.transform.localPosition = Vector3.zero;
        _rangeIndicator = indicatorGo.AddComponent<WeaponRangeIndicator>();
    }

    private void UpdateFrozenState(AppState state)
    {
        _frozen = state != AppState.Play;
    }

    private struct WeaponRuntime
    {
        public WeaponData Data;
        public float CooldownTimer;
    }
}
