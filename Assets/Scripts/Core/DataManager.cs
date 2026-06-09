using System.IO;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    private const string DefaultFolder = "ProjectData";
    private static readonly string[] DefaultProjectFiles =
    {
        "player.json",
        "map.json",
        "enemies.json",
        "weapons.json",
        "waves.json"
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDefaultProjectDataExists();
    }

    // ---- 汎用 Save / Load ----

    public void Save<T>(T data, string fileName)
    {
        var dir = Path.Combine(Application.persistentDataPath, DefaultFolder);
        Directory.CreateDirectory(dir);

        var json = JsonUtility.ToJson(data, prettyPrint: true);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, json);

        Debug.Log($"[DataManager] Save complete: {fileName} ({path})");
        EventBus.Publish(new DataSavedEvent { FileName = fileName });
    }

    public T Load<T>(string fileName) where T : new()
    {
        var path = Path.Combine(Application.persistentDataPath, DefaultFolder, fileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DataManager] File not found: {path}");
            return new T();
        }

        var json = File.ReadAllText(path);
        var result = JsonUtility.FromJson<T>(json);
        if (result == null)
        {
            Debug.LogWarning($"[DataManager] Failed to deserialize: {path}");
            return new T();
        }
        return result;
    }

    public bool Exists(string fileName)
    {
        return File.Exists(
            Path.Combine(Application.persistentDataPath, DefaultFolder, fileName));
    }

    // ---- プロジェクト一括 ----

    public void SaveProject(ProjectData project)
    {
        Save(project.PlayerData,  "player.json");
        Save(project.MapData,     "map.json");
        Save(project.EnemyData,   "enemies.json");
        Save(project.WeaponData,  "weapons.json");
        Save(project.WaveData,    "waves.json");
    }

    public ProjectData LoadProject()
    {
        return new ProjectData
        {
            PlayerData = Load<PlayerData>("player.json"),
            MapData    = Load<MapData>("map.json"),
            EnemyData  = Load<EnemyListData>("enemies.json"),
            WeaponData = Load<WeaponListData>("weapons.json"),
            WaveData   = Load<WaveListData>("waves.json"),
        };
    }

    private void EnsureDefaultProjectDataExists()
    {
        var sourceDir = Path.Combine(Application.streamingAssetsPath, DefaultFolder);
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogWarning($"[DataManager] Default data folder not found: {sourceDir}");
            return;
        }

        var targetDir = Path.Combine(Application.persistentDataPath, DefaultFolder);
        Directory.CreateDirectory(targetDir);

        foreach (var fileName in DefaultProjectFiles)
        {
            var sourcePath = Path.Combine(sourceDir, fileName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[DataManager] Default data file not found: {sourcePath}");
                continue;
            }

            var targetPath = Path.Combine(targetDir, fileName);
            if (File.Exists(targetPath))
            {
                continue;
            }

            try
            {
                File.Copy(sourcePath, targetPath);
                Debug.Log($"[DataManager] Copied default data → {targetPath}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[DataManager] I/O error while copying default data ({fileName}): {ex.Message}");
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[DataManager] Permission denied while copying default data ({fileName}): {ex.Message}");
            }
        }
    }
}
