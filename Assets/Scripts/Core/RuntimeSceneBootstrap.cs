using UnityEngine;
using UnityEngine.SceneManagement;

// RuntimeSceneBootstrap
// Unity Play 開始時に必要なゲームオブジェクトを自動生成する。
// エディター UI（EditorRoot 配下のパネル）は Assets/Scripts/Editor/ に属する
// エディター専用クラスのため、ここからは生成しない。
// → それらは SurvivorsMaker > Setup Scene を1回実行すれば配置される。
public static class RuntimeSceneBootstrap
{
    private const string BootstrapRootName  = "__RuntimeSceneBootstrap";
    private const string TemplatesRootName  = "__Templates";
    private const int    PlayerLayer        = 8;
    private const int    EnemyLayer         = 9;
    private const int    ProjectileLayer    = 10;
    private const int    WallLayer          = 11;
    private const string GameSettingsFile   = "game_settings.json";

    private static Sprite    _solidSprite;
    private static Texture2D _solidTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        var bootstrapRoot  = GetOrCreateSceneObject(BootstrapRootName);
        var templatesRoot  = GetOrCreateChild(bootstrapRoot.transform, TemplatesRootName);
        templatesRoot.SetActive(false);

        ConfigurePhysics();

        // ---- シングルトン ----
        EnsurePersistentSingleton<DataManager>("DataManager");
        var appSM = EnsurePersistentSingleton<AppStateMachine>("AppStateMachine");
        appSM.SetStateWithoutTransition(AppState.Title);
        EnsurePersistentSingleton<AudioManager>("AudioManager");

        // ---- データ読み込み ----
        var playerData   = LoadPlayerData();
        var mapData      = LoadMapData();
        var gameSettings = LoadGameSettings();

        // ---- ゲームオブジェクト ----
        EnsureComponentOnSceneObject<DamageNumberSpawner>("DamageNumberSpawner");
        EnsureMapGenerator(mapData);

        var player       = EnsurePlayer(playerData, gameSettings);
        var cameraFollow = EnsureMainCamera(player.transform);

        var enemyPrefab           = EnsureEnemyPrefab(templatesRoot.transform);
        var projectilePrefab      = EnsureProjectilePrefab(templatesRoot.transform);
        var enemyProjectilePrefab = EnsureEnemyProjectilePrefab(templatesRoot.transform);
        var expGemPrefab          = EnsureExpGemPrefab(templatesRoot.transform);

        var weaponSystem = EnsureWeaponSystem(player.transform, projectilePrefab);
        EnsureComponentOnSceneObject<PassiveSystem>("PassiveSystem");
        var waveSpawner  = EnsureWaveSpawner(player.transform, enemyPrefab, enemyProjectilePrefab);
        EnsureExpDropper(expGemPrefab);

        var gameHud     = EnsureComponentOnSceneObject<GameHUD>("GameHUD");
        EnsureComponentOnSceneObject<LevelUpUI>("LevelUpUI");
        var gameManager = EnsureGameManager(player, waveSpawner, weaponSystem, gameHud, gameSettings);

        // ---- タイトル画面 ----
        EnsureComponentOnSceneObject<TitleScreen>("TitleScreen");

        // ---- 接続 ----
        cameraFollow?.SetTarget(player.transform);
        gameManager.Configure(player, waveSpawner, weaponSystem, gameHud);
    }

    // ---------------------------------------------------------------

    private static void ConfigurePhysics()
    {
        Physics2D.IgnoreLayerCollision(EnemyLayer,      WallLayer,      true);
        Physics2D.IgnoreLayerCollision(EnemyLayer,      EnemyLayer,     true);
        Physics2D.IgnoreLayerCollision(ProjectileLayer, WallLayer,      true);
        Physics2D.IgnoreLayerCollision(ProjectileLayer, PlayerLayer,    true);
    }

    private static T EnsurePersistentSingleton<T>(string objectName) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>();
        if (existing != null) return existing;
        return GetOrAddComponent<T>(GetOrCreateSceneObject(objectName));
    }

    private static T EnsureComponentOnSceneObject<T>(string objectName) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>();
        if (existing != null) return existing;
        return GetOrAddComponent<T>(GetOrCreateSceneObject(objectName));
    }

    private static MapGenerator EnsureMapGenerator(MapData mapData)
    {
        var gen = EnsureComponentOnSceneObject<MapGenerator>("MapGenerator");
        if (mapData != null)
        {
            gen.MapWidth  = mapData.Width;
            gen.MapHeight = mapData.Height;
        }
        return gen;
    }

    private static PlayerController EnsurePlayer(PlayerData playerData, GameSettingsData gs)
    {
        var existing = Object.FindAnyObjectByType<PlayerController>();
        if (existing != null) return existing;

        var go = GetOrCreateSceneObject("Player");
        go.SetActive(false);
        go.transform.position = Vector3.zero;

        var sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sprite = CreateSolidSprite();
        sr.color  = new Color(0.3f, 0.8f, 1f);

        var rb = GetOrAddComponent<Rigidbody2D>(go);
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;

        var col = GetOrAddComponent<CircleCollider2D>(go);
        col.radius = 0.4f;

        GetOrAddComponent<PlayerAnimator>(go);
        var player = GetOrAddComponent<PlayerController>(go);
        player.SetData(playerData);

        go.layer = PlayerLayer;
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        go.SetActive(true);

        player.ApplyGameSettings(gs.PlayerMaxHp, gs.PlayerMoveSpeed, gs.InvincibleSec, gs.ExpMultiplier);
        return player;
    }

    private static CameraFollow EnsureMainCamera(Transform target)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = GetOrCreateSceneObject("Main Camera");
            cam = GetOrAddComponent<Camera>(camGo);
            camGo.tag = "MainCamera";
        }
        cam.orthographic     = true;
        cam.orthographicSize = 10f;
        cam.backgroundColor  = new Color(0.1f, 0.1f, 0.15f);

        var go = cam.gameObject;
        GetOrAddComponent<AudioListener>(go);
        var follow = GetOrAddComponent<CameraFollow>(go);
        GetOrAddComponent<CameraZoom>(go);
        follow.SetTarget(target);
        return follow;
    }

    private static GameObject EnsureEnemyPrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "EnemyPrefab");
        go.SetActive(false);
        var sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sprite = CreateSolidSprite();
        sr.color  = Color.red;
        var rb = GetOrAddComponent<Rigidbody2D>(go);
        rb.gravityScale = 0f; rb.freezeRotation = true;
        GetOrAddComponent<CircleCollider2D>(go).radius = 0.4f;
        GetOrAddComponent<EnemyAI>(go);
        go.layer = EnemyLayer;
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        return go;
    }

    private static GameObject EnsureProjectilePrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "ProjectilePrefab");
        go.SetActive(false);
        var sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sprite = CreateSolidSprite(); sr.color = Color.yellow;
        sr.transform.localScale = Vector3.one * 0.3f;
        GetOrAddComponent<Rigidbody2D>(go).gravityScale = 0f;
        var col = GetOrAddComponent<CircleCollider2D>(go);
        col.radius = 0.15f; col.isTrigger = true;
        GetOrAddComponent<Projectile>(go);
        go.layer = ProjectileLayer;
        return go;
    }

    private static GameObject EnsureEnemyProjectilePrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "EnemyProjectilePrefab");
        go.SetActive(false);
        var sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sprite = CreateSolidSprite(); sr.color = new Color(1f, 0.5f, 0f);
        sr.transform.localScale = Vector3.one * 0.25f;
        GetOrAddComponent<Rigidbody2D>(go).gravityScale = 0f;
        var col = GetOrAddComponent<CircleCollider2D>(go);
        col.radius = 0.12f; col.isTrigger = true;
        GetOrAddComponent<EnemyProjectile>(go);
        return go;
    }

    private static GameObject EnsureExpGemPrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "ExpGemPrefab");
        go.SetActive(false);
        var sr = GetOrAddComponent<SpriteRenderer>(go);
        sr.sprite = CreateSolidSprite(); sr.color = Color.green;
        sr.transform.localScale = Vector3.one * 0.3f;
        var col = GetOrAddComponent<CircleCollider2D>(go);
        col.radius = 0.15f; col.isTrigger = true;
        GetOrAddComponent<ExpGem>(go);
        return go;
    }

    private static WeaponSystem EnsureWeaponSystem(Transform player, GameObject projectilePrefab)
    {
        var ws = EnsureComponentOnSceneObject<WeaponSystem>("WeaponSystem");
        ws.Configure(player, projectilePrefab);
        return ws;
    }

    private static WaveSpawner EnsureWaveSpawner(Transform player, GameObject enemyPrefab, GameObject enemyProjectilePrefab)
    {
        var ws = EnsureComponentOnSceneObject<WaveSpawner>("WaveSpawner");
        ws.Configure(enemyPrefab, enemyProjectilePrefab, player);
        return ws;
    }

    private static void EnsureExpDropper(GameObject expGemPrefab)
    {
        EnsureComponentOnSceneObject<ExpDropper>("ExpDropper").Configure(expGemPrefab);
    }

    private static GameManager EnsureGameManager(
        PlayerController player, WaveSpawner waveSpawner,
        WeaponSystem weaponSystem, GameHUD gameHud, GameSettingsData gs)
    {
        var gm = EnsureComponentOnSceneObject<GameManager>("GameManager");
        gm.Configure(player, waveSpawner, weaponSystem, gameHud);
        gm.ApplyTimeLimitSec(gs.TimeLimitSec);
        return gm;
    }

    // ---- データ読み込み ----

    private static PlayerData LoadPlayerData()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists("player.json"))
            return DataManager.Instance.Load<PlayerData>("player.json");
        return new PlayerData();
    }

    private static MapData LoadMapData()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists("map.json"))
            return DataManager.Instance.Load<MapData>("map.json");
        return new MapData();
    }

    private static GameSettingsData LoadGameSettings()
    {
        if (DataManager.Instance != null && DataManager.Instance.Exists(GameSettingsFile))
            return DataManager.Instance.Load<GameSettingsData>(GameSettingsFile);
        return new GameSettingsData();
    }

    // ---- ユーティリティ ----

    private static GameObject GetOrCreateSceneObject(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static Sprite CreateSolidSprite()
    {
        if (_solidSprite != null) return _solidSprite;
        if (_solidTexture == null)
        {
            _solidTexture = new Texture2D(1, 1);
            _solidTexture.SetPixel(0, 0, Color.white);
            _solidTexture.Apply();
        }
        _solidSprite = Sprite.Create(_solidTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        return _solidSprite;
    }

    private static void OnSceneUnloaded(Scene _)
    {
        if (_solidSprite  != null) { Object.Destroy(_solidSprite);  _solidSprite  = null; }
        if (_solidTexture != null) { Object.Destroy(_solidTexture); _solidTexture = null; }
    }
}
