using UnityEngine;
using UnityEngine.UIElements;

public static class RuntimeSceneBootstrap
{
    private const string BootstrapRootName = "__RuntimeSceneBootstrap";
    private const string TemplatesRootName = "__Templates";
    private const int PlayerLayer = 8;
    private const int EnemyLayer = 9;
    private const int ProjectileLayer = 10;
    private const int WallLayer = 11;
    private const string GameSettingsFileName = "game_settings.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var bootstrapRoot = GetOrCreateSceneObject(BootstrapRootName);
        var templatesRoot = GetOrCreateChild(bootstrapRoot.transform, TemplatesRootName);
        templatesRoot.SetActive(false);

        ConfigurePhysics();

        EnsurePersistentSingleton<DataManager>("DataManager");
        EnsurePersistentSingleton<AudioManager>("AudioManager");
        var appStateMachine = EnsurePersistentSingleton<AppStateMachine>("AppStateMachine");
        appStateMachine.SetStateWithoutTransition(AppState.Title);

        var playerData = LoadPlayerData();
        var mapData = LoadMapData();
        var gameSettings = LoadGameSettings();

        EnsureComponentOnSceneObject<DamageNumberSpawner>("DamageNumberSpawner");
        EnsureMapGenerator(mapData);

        var player = EnsurePlayer(playerData, gameSettings);
        var cameraFollow = EnsureMainCamera(player.transform);

        var enemyPrefab = EnsureEnemyPrefab(templatesRoot.transform);
        var projectilePrefab = EnsureProjectilePrefab(templatesRoot.transform);
        var enemyProjectilePrefab = EnsureEnemyProjectilePrefab(templatesRoot.transform);
        var expGemPrefab = EnsureExpGemPrefab(templatesRoot.transform);

        var weaponSystem = EnsureWeaponSystem(player.transform, projectilePrefab);
        EnsureComponentOnSceneObject<PassiveSystem>("PassiveSystem");
        var waveSpawner = EnsureWaveSpawner(player.transform, enemyPrefab, enemyProjectilePrefab);
        EnsureExpDropper(expGemPrefab);
        var gameHud = EnsureComponentOnSceneObject<GameHUD>("GameHUD");
        EnsureComponentOnSceneObject<LevelUpUI>("LevelUpUI");
        var gameManager = EnsureGameManager(player, waveSpawner, weaponSystem, gameHud, gameSettings);

        EnsureTitleScreen();
        EnsureEditorUi();

        cameraFollow?.SetTarget(player.transform);
        gameManager.Configure(player, waveSpawner, weaponSystem, gameHud);
        gameManager.ApplyTimeLimitSec(gameSettings.TimeLimitSec);
        player.ApplyGameSettings(
            gameSettings.PlayerMaxHp,
            gameSettings.PlayerMoveSpeed,
            gameSettings.InvincibleSec,
            gameSettings.ExpMultiplier);

        appStateMachine.BroadcastCurrentState();
    }

    private static void ConfigurePhysics()
    {
        Physics2D.IgnoreLayerCollision(EnemyLayer, WallLayer, true);
        Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
        Physics2D.IgnoreLayerCollision(ProjectileLayer, WallLayer, true);
        Physics2D.IgnoreLayerCollision(ProjectileLayer, PlayerLayer, true);
    }

    private static T EnsurePersistentSingleton<T>(string objectName) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>();
        if (existing != null)
            return existing;

        var go = GetOrCreateSceneObject(objectName);
        return GetOrAddComponent<T>(go);
    }

    private static T EnsureComponentOnSceneObject<T>(string objectName) where T : Component
    {
        var existing = Object.FindAnyObjectByType<T>();
        if (existing != null)
            return existing;

        var go = GetOrCreateSceneObject(objectName);
        return GetOrAddComponent<T>(go);
    }

    private static MapGenerator EnsureMapGenerator(MapData mapData)
    {
        var mapGenerator = EnsureComponentOnSceneObject<MapGenerator>("MapGenerator");
        if (mapData != null)
        {
            mapGenerator.MapWidth = mapData.Width;
            mapGenerator.MapHeight = mapData.Height;
        }

        return mapGenerator;
    }

    private static PlayerController EnsurePlayer(PlayerData playerData, GameSettingsData gameSettings)
    {
        var existing = Object.FindAnyObjectByType<PlayerController>();
        if (existing != null)
            return existing;

        var playerGo = GetOrCreateSceneObject("Player");
        playerGo.SetActive(false);
        playerGo.transform.position = Vector3.zero;

        var spriteRenderer = GetOrAddComponent<SpriteRenderer>(playerGo);
        spriteRenderer.sprite = CreateSolidSprite();
        spriteRenderer.color = new Color(0.3f, 0.8f, 1f);

        var rigidbody = GetOrAddComponent<Rigidbody2D>(playerGo);
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;

        var collider = GetOrAddComponent<CircleCollider2D>(playerGo);
        collider.radius = 0.4f;

        GetOrAddComponent<PlayerAnimator>(playerGo);
        var player = GetOrAddComponent<PlayerController>(playerGo);
        player.SetData(playerData);

        playerGo.layer = PlayerLayer;
        playerGo.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        playerGo.SetActive(true);

        player.ApplyGameSettings(
            gameSettings.PlayerMaxHp,
            gameSettings.PlayerMoveSpeed,
            gameSettings.InvincibleSec,
            gameSettings.ExpMultiplier);

        return player;
    }

    private static CameraFollow EnsureMainCamera(Transform target)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            var cameraGo = GetOrCreateSceneObject("Main Camera");
            camera = GetOrAddComponent<Camera>(cameraGo);
            cameraGo.tag = "MainCamera";
        }

        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

        var cameraGoRef = camera.gameObject;
        GetOrAddComponent<AudioListener>(cameraGoRef);
        var follow = GetOrAddComponent<CameraFollow>(cameraGoRef);
        GetOrAddComponent<CameraZoom>(cameraGoRef);
        follow.SetTarget(target);
        return follow;
    }

    private static GameObject EnsureEnemyPrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "EnemyPrefab");
        go.SetActive(true);

        var spriteRenderer = GetOrAddComponent<SpriteRenderer>(go);
        spriteRenderer.sprite = CreateSolidSprite();
        spriteRenderer.color = Color.red;

        var rigidbody = GetOrAddComponent<Rigidbody2D>(go);
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;

        var collider = GetOrAddComponent<CircleCollider2D>(go);
        collider.radius = 0.4f;

        GetOrAddComponent<EnemyAI>(go);
        go.layer = EnemyLayer;
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        return go;
    }

    private static GameObject EnsureProjectilePrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "ProjectilePrefab");
        go.SetActive(true);

        var spriteRenderer = GetOrAddComponent<SpriteRenderer>(go);
        spriteRenderer.sprite = CreateSolidSprite();
        spriteRenderer.color = Color.yellow;
        spriteRenderer.transform.localScale = Vector3.one * 0.3f;

        var rigidbody = GetOrAddComponent<Rigidbody2D>(go);
        rigidbody.gravityScale = 0f;

        var collider = GetOrAddComponent<CircleCollider2D>(go);
        collider.radius = 0.15f;
        collider.isTrigger = true;

        GetOrAddComponent<Projectile>(go);
        go.layer = ProjectileLayer;
        return go;
    }

    private static GameObject EnsureEnemyProjectilePrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "EnemyProjectilePrefab");
        go.SetActive(true);

        var spriteRenderer = GetOrAddComponent<SpriteRenderer>(go);
        spriteRenderer.sprite = CreateSolidSprite();
        spriteRenderer.color = new Color(1f, 0.5f, 0f);
        spriteRenderer.transform.localScale = Vector3.one * 0.25f;

        var rigidbody = GetOrAddComponent<Rigidbody2D>(go);
        rigidbody.gravityScale = 0f;

        var collider = GetOrAddComponent<CircleCollider2D>(go);
        collider.radius = 0.12f;
        collider.isTrigger = true;

        GetOrAddComponent<EnemyProjectile>(go);
        return go;
    }

    private static GameObject EnsureExpGemPrefab(Transform parent)
    {
        var go = GetOrCreateChild(parent, "ExpGemPrefab");
        go.SetActive(true);

        var spriteRenderer = GetOrAddComponent<SpriteRenderer>(go);
        spriteRenderer.sprite = CreateSolidSprite();
        spriteRenderer.color = Color.green;
        spriteRenderer.transform.localScale = Vector3.one * 0.3f;

        var collider = GetOrAddComponent<CircleCollider2D>(go);
        collider.radius = 0.15f;
        collider.isTrigger = true;

        GetOrAddComponent<ExpGem>(go);
        return go;
    }

    private static WeaponSystem EnsureWeaponSystem(Transform player, GameObject projectilePrefab)
    {
        var weaponSystem = EnsureComponentOnSceneObject<WeaponSystem>("WeaponSystem");
        weaponSystem.Configure(player, projectilePrefab);
        return weaponSystem;
    }

    private static WaveSpawner EnsureWaveSpawner(Transform player, GameObject enemyPrefab, GameObject enemyProjectilePrefab)
    {
        var waveSpawner = EnsureComponentOnSceneObject<WaveSpawner>("WaveSpawner");
        waveSpawner.Configure(enemyPrefab, enemyProjectilePrefab, player);
        return waveSpawner;
    }

    private static void EnsureExpDropper(GameObject expGemPrefab)
    {
        var expDropper = EnsureComponentOnSceneObject<ExpDropper>("ExpDropper");
        expDropper.Configure(expGemPrefab);
    }

    private static GameManager EnsureGameManager(
        PlayerController player,
        WaveSpawner waveSpawner,
        WeaponSystem weaponSystem,
        GameHUD gameHud,
        GameSettingsData gameSettings)
    {
        var gameManager = EnsureComponentOnSceneObject<GameManager>("GameManager");
        gameManager.Configure(player, waveSpawner, weaponSystem, gameHud);
        gameManager.ApplyTimeLimitSec(gameSettings.TimeLimitSec);
        return gameManager;
    }

    private static void EnsureTitleScreen()
    {
        EnsureComponentOnSceneObject<TitleScreen>("TitleScreen");
    }

    private static void EnsureEditorUi()
    {
        var editorRoot = GetOrCreateSceneObject("EditorRoot");
        var rootDocument = GetOrAddComponent<UIDocument>(editorRoot);
        rootDocument.panelSettings = CreatePanelSettings(100);
        GetOrAddComponent<EditorRootPanel>(editorRoot);

        EnsureEditorPanel<MapEditor>(editorRoot.transform, "MapEditorPanel", 101);
        EnsureEditorPanel<MapSettingsEditor>(editorRoot.transform, "MapSettingsEditorPanel", 102);
        EnsureEditorPanel<GameSettingsEditor>(editorRoot.transform, "GameSettingsEditorPanel", 102);
        EnsureEditorPanel<EnemyEditor>(editorRoot.transform, "EnemyEditorPanel", 102);
        EnsureEditorPanel<WeaponEditor>(editorRoot.transform, "WeaponEditorPanel", 102);
        EnsureEditorPanel<PassiveEditor>(editorRoot.transform, "PassiveEditorPanel", 102);
        EnsureEditorPanel<PresetEditor>(editorRoot.transform, "PresetEditorPanel", 102);
        EnsureEditorPanel<WaveEditor>(editorRoot.transform, "WaveEditorPanel", 102);
        EnsureEditorPanel<AssetManagerPanel>(editorRoot.transform, "AssetManagerPanel", 102);
        EnsureEditorPanel<DotArtEditor>(editorRoot.transform, "DotArtEditorPanel", 102);
        EnsureEditorPanel<SpriteSheetEditor>(editorRoot.transform, "SpriteSheetEditorPanel", 102);
        EnsureEditorPanel<SpriteSettingsEditor>(editorRoot.transform, "SpriteSettingsEditorPanel", 102);
    }

    private static void EnsureEditorPanel<T>(Transform parent, string panelName, int sortingOrder) where T : Component
    {
        var panelGo = GetOrCreateChild(parent, panelName);
        var document = GetOrAddComponent<UIDocument>(panelGo);
        document.panelSettings = CreatePanelSettings(sortingOrder);
        GetOrAddComponent<T>(panelGo);
    }

    private static PanelSettings CreatePanelSettings(int sortingOrder)
    {
        var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.sortingOrder = sortingOrder;
        panelSettings.name = $"RuntimePanelSettings_{sortingOrder}";
        return panelSettings;
    }

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
        if (DataManager.Instance != null && DataManager.Instance.Exists(GameSettingsFileName))
            return DataManager.Instance.Load<GameSettingsData>(GameSettingsFileName);

        return new GameSettingsData();
    }

    private static GameObject GetOrCreateSceneObject(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
            go = new GameObject(name);
        return go;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child.gameObject;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
    }

    private static Sprite _solidSprite;

    private static Sprite CreateSolidSprite()
    {
        if (_solidSprite != null)
            return _solidSprite;

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _solidSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        return _solidSprite;
    }
}
