#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public static class SceneSetupEditor
{
    private const string PrefabsFolder = "Assets/GeneratedPrefabs";
    private const int PlayerLayer = 8;
    private const int EnemyLayer = 9;
    private const int ProjectileLayer = 10;
    private const int WallLayer = 11;

    // デフォルトプレイヤーパラメータ
    private const int DefaultPlayerMaxHp = 100;
    private const float DefaultPlayerMoveSpeed = 4f;

    // カメラ設定
    private const float DefaultCameraSize = 10f;

    [MenuItem("SurvivorsMaker/\U0001f3ae Setup Scene (初回セットアップ)")]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog("Setup Scene",
            "現在のシーンにゲームオブジェクトを自動配置します。\n既存の同名オブジェクトは削除されます。\n\n続けますか？", "OK", "キャンセル"))
            return;

        // 既存オブジェクトのクリーンアップ
        DestroyIfExists("GameManager");
        DestroyIfExists("Player");
        DestroyIfExists("WaveSpawner");
        DestroyIfExists("WeaponSystem");
        DestroyIfExists("PassiveSystem");
        DestroyIfExists("ExpDropper");
        DestroyIfExists("GameHUD");
        DestroyIfExists("LevelUpUI");
        DestroyIfExists("Main Camera");
        DestroyIfExists("MapGenerator");
        DestroyIfExists("DamageNumberSpawner");
        DestroyIfExists("EditorRoot");
        DestroyIfExists("TitleScreen");
        DestroyIfExists("AudioManager");

        // プレハブ保存先フォルダを確保
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");

        // ---- 共通スプライト（Unity組み込みUIスプライト）----
        var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---- プレハブ資産を作成・保存 ----
        var enemyPrefab           = SavePrefabAsset(BuildEnemyGO(defaultSprite),           "EnemyPrefab");
        var projectilePrefab      = SavePrefabAsset(BuildProjectileGO(defaultSprite),      "ProjectilePrefab");
        var enemyProjectilePrefab = SavePrefabAsset(BuildEnemyProjectileGO(defaultSprite), "EnemyProjectilePrefab");
        var expGemPrefab          = SavePrefabAsset(BuildExpGemGO(defaultSprite),          "ExpGemPrefab");

        // ---- シーン構築 ----

        // レイヤー設定（Player=8, Enemy=9, Projectile=10, Wall=11）
        EnsureLayers();

        // 敵↔壁 のみ無効化（矢は敵に当たる）
        Physics2D.IgnoreLayerCollision(EnemyLayer, WallLayer, true);
        // 敵同士の衝突を無効化（パフォーマンス向上）
        Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
        // 矢↔壁 も無効化（仕様として矢は壁を貫通）
        Physics2D.IgnoreLayerCollision(ProjectileLayer, WallLayer, true);
        // 矢↔プレイヤー の衝突を無効化（自分の矢で自分がダメージを受けない）
        Physics2D.IgnoreLayerCollision(ProjectileLayer, PlayerLayer, true);

        // MapGenerator
        var mapGo = new GameObject("MapGenerator");
        var mapGen = mapGo.AddComponent<MapGenerator>();
        mapGen.Generate();

        // DamageNumberSpawner
        var dnsGo = new GameObject("DamageNumberSpawner");
        dnsGo.AddComponent<DamageNumberSpawner>();

        // カメラ
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = DefaultCameraSize;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        camGo.AddComponent<AudioListener>();
        var camFollow = camGo.AddComponent<CameraFollow>();
        camGo.AddComponent<CameraZoom>();

        // プレイヤー（シーンに直接作成）
        var playerGo = BuildPlayerGO(defaultSprite);
        playerGo.name = "Player";
        playerGo.transform.position = Vector3.zero;
        var playerCtrl = playerGo.GetComponent<PlayerController>();

        // カメラターゲット設定
        var camSO = new SerializedObject(camFollow);
        camSO.FindProperty("_target").objectReferenceValue = playerGo.transform;
        camSO.ApplyModifiedProperties();

        // WeaponSystem
        var weaponGo = new GameObject("WeaponSystem");
        var weaponSys = weaponGo.AddComponent<WeaponSystem>();
        var weaponSO = new SerializedObject(weaponSys);
        weaponSO.FindProperty("_player").objectReferenceValue = playerGo.transform;
        weaponSO.FindProperty("_projectilePrefab").objectReferenceValue = projectilePrefab;
        weaponSO.ApplyModifiedProperties();

        var passiveGo = new GameObject("PassiveSystem");
        passiveGo.AddComponent<PassiveSystem>();

        // WaveSpawner
        var spawnerGo = new GameObject("WaveSpawner");
        var spawner = spawnerGo.AddComponent<WaveSpawner>();
        var spawnerSO = new SerializedObject(spawner);
        spawnerSO.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;
        spawnerSO.FindProperty("_enemyProjectilePrefab").objectReferenceValue = enemyProjectilePrefab;
        spawnerSO.FindProperty("_player").objectReferenceValue = playerGo.transform;
        spawnerSO.ApplyModifiedProperties();

        // ExpDropper
        var dropperGo = new GameObject("ExpDropper");
        var dropper = dropperGo.AddComponent<ExpDropper>();
        var dropperSO = new SerializedObject(dropper);
        dropperSO.FindProperty("_expGemPrefab").objectReferenceValue = expGemPrefab;
        dropperSO.ApplyModifiedProperties();

        // GameHUD（OnGUIで描画するゲーム内オーバーレイ）
        var hud = SetupHUD(playerCtrl);
        SetupLevelUpUI(weaponSys);

        // GameManager
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var gmSO = new SerializedObject(gm);
        gmSO.FindProperty("_player").objectReferenceValue = playerCtrl;
        gmSO.FindProperty("_waveSpawner").objectReferenceValue = spawner;
        gmSO.FindProperty("_weaponSystem").objectReferenceValue = weaponSys;
        gmSO.FindProperty("_gameHUD").objectReferenceValue = hud;
        gmSO.ApplyModifiedProperties();

        // DataManager がなければ追加
        var dm = Object.FindAnyObjectByType<DataManager>();
        if (dm == null)
        {
            var dmGo = new GameObject("DataManager");
            dmGo.AddComponent<DataManager>();
        }

        // AudioManager がなければ追加
        var am = Object.FindAnyObjectByType<AudioManager>();
        if (am == null)
        {
            var amGo = new GameObject("AudioManager");
            amGo.AddComponent<AudioManager>();
        }

        // TitleScreen（OnGUI タイトル表示）
        var titleScreenGo = new GameObject("TitleScreen");
        titleScreenGo.AddComponent<TitleScreen>();

        // AppStateMachine がなければ追加
        var asm = Object.FindAnyObjectByType<AppStateMachine>();
        if (asm == null)
        {
            var asmGo = new GameObject("AppStateMachine");
            asmGo.AddComponent<AppStateMachine>();
        }

        // EditorRoot（UI Toolkit 2ペイン構成）
        var editorRootGo = new GameObject("EditorRoot");
        editorRootGo.AddComponent<UIDocument>();
        editorRootGo.AddComponent<EditorRootPanel>();

        var mapEditorPanelGo = new GameObject("MapEditorPanel");
        mapEditorPanelGo.transform.SetParent(editorRootGo.transform);
        mapEditorPanelGo.AddComponent<UIDocument>();
        mapEditorPanelGo.AddComponent<MapEditor>();

        var mapSettingsEditorPanelGo = new GameObject("MapSettingsEditorPanel");
        mapSettingsEditorPanelGo.transform.SetParent(editorRootGo.transform);
        mapSettingsEditorPanelGo.AddComponent<UIDocument>();
        mapSettingsEditorPanelGo.AddComponent<MapSettingsEditor>();

        var gameSettingsEditorPanelGo = new GameObject("GameSettingsEditorPanel");
        gameSettingsEditorPanelGo.transform.SetParent(editorRootGo.transform);
        gameSettingsEditorPanelGo.AddComponent<UIDocument>();
        gameSettingsEditorPanelGo.AddComponent<GameSettingsEditor>();

        var enemyEditorPanelGo = new GameObject("EnemyEditorPanel");
        enemyEditorPanelGo.transform.SetParent(editorRootGo.transform);
        enemyEditorPanelGo.AddComponent<UIDocument>();
        enemyEditorPanelGo.AddComponent<EnemyEditor>();

        var weaponEditorPanelGo = new GameObject("WeaponEditorPanel");
        weaponEditorPanelGo.transform.SetParent(editorRootGo.transform);
        weaponEditorPanelGo.AddComponent<UIDocument>();
        weaponEditorPanelGo.AddComponent<WeaponEditor>();

        var passiveEditorPanelGo = new GameObject("PassiveEditorPanel");
        passiveEditorPanelGo.transform.SetParent(editorRootGo.transform);
        passiveEditorPanelGo.AddComponent<UIDocument>();
        passiveEditorPanelGo.AddComponent<PassiveEditor>();

        var presetEditorPanelGo = new GameObject("PresetEditorPanel");
        presetEditorPanelGo.transform.SetParent(editorRootGo.transform);
        presetEditorPanelGo.AddComponent<UIDocument>();
        presetEditorPanelGo.AddComponent<PresetEditor>();

        var spriteSettingsEditorPanelGo = new GameObject("SpriteSettingsEditorPanel");
        spriteSettingsEditorPanelGo.transform.SetParent(editorRootGo.transform);
        spriteSettingsEditorPanelGo.AddComponent<UIDocument>();
        spriteSettingsEditorPanelGo.AddComponent<SpriteSettingsEditor>();

        var waveEditorPanelGo = new GameObject("WaveEditorPanel");
        waveEditorPanelGo.transform.SetParent(editorRootGo.transform);
        waveEditorPanelGo.AddComponent<UIDocument>();
        waveEditorPanelGo.AddComponent<WaveEditor>();

        var assetManagerPanelGo = new GameObject("AssetManagerPanel");
        assetManagerPanelGo.transform.SetParent(editorRootGo.transform);
        assetManagerPanelGo.AddComponent<UIDocument>();
        assetManagerPanelGo.AddComponent<AssetManagerPanel>();

        var dotArtEditorPanelGo = new GameObject("DotArtEditorPanel");
        dotArtEditorPanelGo.transform.SetParent(editorRootGo.transform);
        dotArtEditorPanelGo.AddComponent<UIDocument>();
        dotArtEditorPanelGo.AddComponent<DotArtEditor>();

        var spriteSheetEditorPanelGo = new GameObject("SpriteSheetEditorPanel");
        spriteSheetEditorPanelGo.transform.SetParent(editorRootGo.transform);
        spriteSheetEditorPanelGo.AddComponent<UIDocument>();
        spriteSheetEditorPanelGo.AddComponent<SpriteSheetEditor>();

        // シーンを保存済みとしてマーク
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完了！",
            "セットアップが完了しました！\n\n▶ Playボタンを押すとゲームが始まります。",
            "OK");

        Debug.Log("[SceneSetupEditor] Scene setup complete!");
    }

    // ---- ヘルパー: 既存オブジェクト削除 ----

    private static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    // ---- ヘルパー: レイヤー自動追加 ----

    private static void EnsureLayers()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        EnsureLayer(layersProp, PlayerLayer, "Player");
        EnsureLayer(layersProp, EnemyLayer, "Enemy");
        EnsureLayer(layersProp, ProjectileLayer, "Projectile");
        EnsureLayer(layersProp, WallLayer, "Wall");

        tagManager.ApplyModifiedProperties();
    }

    private static void EnsureLayer(SerializedProperty layersProp, int index, string name)
    {
        var element = layersProp.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(element.stringValue))
            element.stringValue = name;
    }

    // ---- ヘルパー: GameObject → Prefabアセットとして保存 ----

    private static GameObject SavePrefabAsset(GameObject go, string prefabName)
    {
        var path = $"{PrefabsFolder}/{prefabName}.prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    // ---- ヘルパー: 各種プレハブ元GOを構築 ----

    private static GameObject BuildPlayerGO(Sprite sprite)
    {
        var go = new GameObject("Player");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // スプライトシートを使用する場合は Color.white に変更してください
        sr.color = Color.cyan;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        var ctrl = go.AddComponent<PlayerController>();
        go.AddComponent<PlayerAnimator>();
        go.layer = PlayerLayer; // Player layer
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        // PlayerData のインラインフィールドを設定（ScriptableObject不要）
        var so = new SerializedObject(ctrl);
        var dataProp = so.FindProperty("_data");
        if (dataProp != null)
        {
            dataProp.FindPropertyRelative("MaxHp").intValue = DefaultPlayerMaxHp;
            dataProp.FindPropertyRelative("MoveSpeed").floatValue = DefaultPlayerMoveSpeed;
            so.ApplyModifiedProperties();
        }

        return go;
    }

    private static GameObject BuildEnemyGO(Sprite sprite)
    {
        var go = new GameObject("Enemy");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.red;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        go.AddComponent<EnemyAI>();
        go.layer = EnemyLayer; // Enemy layer
        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        return go;
    }

    private static GameObject BuildProjectileGO(Sprite sprite)
    {
        var go = new GameObject("Projectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.yellow;
        sr.transform.localScale = Vector3.one * 0.3f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.15f;
        col.isTrigger = true;

        go.AddComponent<Projectile>();
        go.layer = ProjectileLayer; // Projectile layer
        return go;
    }

    private static GameObject BuildEnemyProjectileGO(Sprite sprite)
    {
        var go = new GameObject("EnemyProjectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 0.5f, 0f);
        sr.transform.localScale = Vector3.one * 0.25f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.12f;
        col.isTrigger = true;

        go.AddComponent<EnemyProjectile>();
        return go;
    }

    private static GameObject BuildExpGemGO(Sprite sprite)
    {
        var go = new GameObject("ExpGem");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.green;
        sr.transform.localScale = Vector3.one * 0.3f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.15f;
        col.isTrigger = true;

        go.AddComponent<ExpGem>();
        return go;
    }

    private static GameHUD SetupHUD(PlayerController player)
    {
        // GameHUD は OnGUI ベースのゲーム内オーバーレイ（UIElements不使用）
        var hudGo = new GameObject("GameHUD");
        var hud = hudGo.AddComponent<GameHUD>();
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("_player").objectReferenceValue = player;
        hudSO.ApplyModifiedProperties();
        return hud;
    }

    private static void SetupLevelUpUI(WeaponSystem weaponSystem)
    {
        // LevelUpUI は OnGUI ベースのゲーム内オーバーレイ（UIElements不使用）
        var levelUpGo = new GameObject("LevelUpUI");
        var levelUpUI = levelUpGo.AddComponent<LevelUpUI>();
        var levelUpSO = new SerializedObject(levelUpUI);
        levelUpSO.FindProperty("_weaponSystem").objectReferenceValue = weaponSystem;
        levelUpSO.ApplyModifiedProperties();
    }
}
#endif
