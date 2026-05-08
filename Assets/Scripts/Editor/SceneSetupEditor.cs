#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class SceneSetupEditor
{
    private const string PrefabsFolder = "Assets/GeneratedPrefabs";

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
        DestroyIfExists("ExpDropper");
        DestroyIfExists("GameHUD");
        DestroyIfExists("Main Camera");

        // プレハブ保存先フォルダを確保
        if (!AssetDatabase.IsValidFolder(PrefabsFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");

        // ---- 共通スプライト（Unity組み込みUIスプライト）----
        var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---- プレハブ資産を作成・保存 ----
        var enemyPrefab          = SavePrefabAsset(BuildEnemyGO(defaultSprite),          "EnemyPrefab");
        var projectilePrefab     = SavePrefabAsset(BuildProjectileGO(defaultSprite),     "ProjectilePrefab");
        var enemyProjectilePrefab = SavePrefabAsset(BuildEnemyProjectileGO(defaultSprite), "EnemyProjectilePrefab");
        var expGemPrefab         = SavePrefabAsset(BuildExpGemGO(defaultSprite),         "ExpGemPrefab");

        // ---- シーン構築 ----

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

        // GameHUD
        var hud = SetupHUD(playerCtrl);

        // GameManager
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var gmSO = new SerializedObject(gm);
        gmSO.FindProperty("_player").objectReferenceValue = playerCtrl;
        gmSO.FindProperty("_waveSpawner").objectReferenceValue = spawner;
        gmSO.FindProperty("_weaponSystem").objectReferenceValue = weaponSys;
        gmSO.FindProperty("_gameHUD").objectReferenceValue = hud;
        gmSO.ApplyModifiedProperties();

        // AppStateMachine がなければ追加
        var asm = Object.FindAnyObjectByType<AppStateMachine>();
        if (asm == null)
        {
            var asmGo = new GameObject("AppStateMachine");
            asmGo.AddComponent<AppStateMachine>();
        }

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
        sr.color = Color.cyan;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        var ctrl = go.AddComponent<PlayerController>();
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
        var hudGo = new GameObject("GameHUD");
        var hud = hudGo.AddComponent<GameHUD>();
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("_player").objectReferenceValue = player;
        hudSO.ApplyModifiedProperties();
        // UIDocumentは不要（OnGUIで描画するため）
        return hud;
    }
}
#endif
