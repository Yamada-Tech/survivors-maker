using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

[Serializable]
public class AssetManifest
{
    public List<AssetRecord> Assets = new();
}

[Serializable]
public class AssetRecord
{
    public string Guid;
    public string FileName;
    public string OriginalFileName;
    public string Hash;
    public AssetKind Kind;
}

public enum AssetKind
{
    Unknown,
    Texture,
    Bgm,
    Se,
    Font
}

public class AssetManager : MonoBehaviour
{
    public static AssetManager Instance { get; private set; }

    private readonly Dictionary<string, Texture2D> _textureCache = new();
    private readonly Dictionary<string, AudioClip> _audioCache = new();
    private readonly Dictionary<string, long> _fileWriteTicks = new();

    private AssetManifest _manifest = new();
    private float _nextScanTime;

    public event Action<string> OnAssetChanged;

    private string AssetsDir => Path.Combine(Application.persistentDataPath, "ProjectData", "Assets");
    private string ManifestPath => Path.Combine(AssetsDir, "manifest.json");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Directory.CreateDirectory(AssetsDir);
        LoadManifest();
        SyncManifestWithFiles();
        RebuildFileWriteCache();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextScanTime) return;
        _nextScanTime = Time.unscaledTime + 1f;
        ScanExternalChanges();
    }

    public IReadOnlyList<AssetRecord> GetAssets() => _manifest.Assets;

    public string ImportTexture(string sourcePath)
    {
        return ImportAsset(sourcePath, AssetKind.Texture);
    }

    public string ImportAudio(string sourcePath)
    {
        return ImportAsset(sourcePath, AssetKind.Bgm);
    }

    public string ImportSe(string sourcePath)
    {
        return ImportAsset(sourcePath, AssetKind.Se);
    }

    public string ImportFont(string sourcePath)
    {
        return ImportAsset(sourcePath, AssetKind.Font);
    }

    public bool ReplaceAsset(string guid, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return false;

        var record = _manifest.Assets.FirstOrDefault(a => a.Guid == guid);
        if (record == null)
            return false;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!IsSupportedExtensionForKind(record.Kind, ext))
            return false;

        var oldPath = GetAssetPath(guid);
        var newPath = Path.Combine(AssetsDir, $"{guid}{ext}");

        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath) && oldPath != newPath)
            File.Delete(oldPath);

        File.Copy(sourcePath, newPath, overwrite: true);

        record.FileName = Path.GetFileName(newPath);
        record.OriginalFileName = Path.GetFileName(sourcePath);
        record.Hash = ComputeHash(sourcePath);

        _textureCache.Remove(guid);
        _audioCache.Remove(guid);
        _fileWriteTicks[newPath] = File.GetLastWriteTimeUtc(newPath).Ticks;

        SaveManifest();
        OnAssetChanged?.Invoke(guid);
        return true;
    }

    public bool DeleteAsset(string guid, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(guid))
        {
            reason = "GUID is empty.";
            return false;
        }

        if (TryGetReferenceFile(guid, out var fileName))
        {
            reason = $"参照中のため削除できません: {fileName}";
            return false;
        }

        var path = GetAssetPath(guid);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);

        var removed = _manifest.Assets.RemoveAll(a => a.Guid == guid) > 0;

        _textureCache.Remove(guid);
        _audioCache.Remove(guid);
        if (!string.IsNullOrEmpty(path))
            _fileWriteTicks.Remove(path);

        if (removed)
        {
            SaveManifest();
            OnAssetChanged?.Invoke(guid);
        }

        return removed;
    }

    public Texture2D LoadTexture(string guid)
    {
        if (_textureCache.TryGetValue(guid, out var cached)) return cached;

        var path = GetAssetPath(guid);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        tex.LoadImage(bytes);
        _textureCache[guid] = tex;
        return tex;
    }

    public void LoadAudio(string guid, Action<AudioClip> onLoaded)
    {
        if (_audioCache.TryGetValue(guid, out var cached))
        {
            onLoaded?.Invoke(cached);
            return;
        }

        StartCoroutine(LoadAudioRoutine(guid, onLoaded));
    }

    public void ClearCache()
    {
        _textureCache.Clear();
        _audioCache.Clear();
    }

    private IEnumerator LoadAudioRoutine(string guid, Action<AudioClip> onLoaded)
    {
        var path = GetAssetPath(guid);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        var audioType = GetAudioTypeFromExtension(Path.GetExtension(path));
        if (audioType == AudioType.UNKNOWN)
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        var request = UnityWebRequestMultimedia.GetAudioClip($"file://{path}", audioType);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[AssetManager] Failed to load audio: {request.error}");
            onLoaded?.Invoke(null);
            yield break;
        }

        var clip = DownloadHandlerAudioClip.GetContent(request);
        _audioCache[guid] = clip;
        onLoaded?.Invoke(clip);
    }

    private string ImportAsset(string sourcePath, AssetKind kind)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return string.Empty;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!IsSupportedExtensionForKind(kind, ext))
            return string.Empty;

        var hash = ComputeHash(sourcePath);
        var duplicate = _manifest.Assets.FirstOrDefault(a => a.Kind == kind && a.Hash == hash && File.Exists(Path.Combine(AssetsDir, a.FileName)));
        if (duplicate != null)
            return duplicate.Guid;

        var guid = System.Guid.NewGuid().ToString("N");
        var dest = Path.Combine(AssetsDir, $"{guid}{ext}");
        File.Copy(sourcePath, dest, overwrite: true);

        _manifest.Assets.Add(new AssetRecord
        {
            Guid = guid,
            FileName = Path.GetFileName(dest),
            OriginalFileName = Path.GetFileName(sourcePath),
            Hash = hash,
            Kind = kind
        });

        _fileWriteTicks[dest] = File.GetLastWriteTimeUtc(dest).Ticks;
        SaveManifest();
        Debug.Log($"[AssetManager] Imported {kind} → {dest}");
        OnAssetChanged?.Invoke(guid);
        return guid;
    }

    private string GetAssetPath(string guid)
    {
        var record = _manifest.Assets.FirstOrDefault(a => a.Guid == guid);
        if (record != null)
        {
            var exact = Path.Combine(AssetsDir, record.FileName);
            if (File.Exists(exact))
                return exact;
        }

        var files = Directory.GetFiles(AssetsDir, $"{guid}.*");
        return files.Length > 0 ? files[0] : null;
    }

    private void LoadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            _manifest = new AssetManifest();
            return;
        }

        var json = File.ReadAllText(ManifestPath);
        _manifest = JsonUtility.FromJson<AssetManifest>(json) ?? new AssetManifest();
        _manifest.Assets ??= new List<AssetRecord>();
    }

    private void SaveManifest()
    {
        _manifest.Assets ??= new List<AssetRecord>();
        var json = JsonUtility.ToJson(_manifest, prettyPrint: true);
        File.WriteAllText(ManifestPath, json);
    }

    private void SyncManifestWithFiles()
    {
        var removed = _manifest.Assets.RemoveAll(a => !File.Exists(Path.Combine(AssetsDir, a.FileName))) > 0;
        var knownFiles = new HashSet<string>(_manifest.Assets.Select(a => a.FileName));

        foreach (var filePath in Directory.GetFiles(AssetsDir))
        {
            var fileName = Path.GetFileName(filePath);
            if (string.Equals(fileName, "manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (knownFiles.Contains(fileName)) continue;

            var ext = Path.GetExtension(fileName);
            var kind = DetectKind(ext);
            if (kind == AssetKind.Unknown) continue;

            var guidCandidate = Path.GetFileNameWithoutExtension(fileName);
            if (guidCandidate.Length != 32 || !guidCandidate.All(IsHexDigit))
                guidCandidate = System.Guid.NewGuid().ToString("N");

            _manifest.Assets.Add(new AssetRecord
            {
                Guid = guidCandidate,
                FileName = fileName,
                OriginalFileName = fileName,
                Hash = ComputeHash(filePath),
                Kind = kind
            });

            knownFiles.Add(fileName);
            removed = true;
        }

        if (removed)
            SaveManifest();
    }

    private void RebuildFileWriteCache()
    {
        _fileWriteTicks.Clear();
        foreach (var record in _manifest.Assets)
        {
            var path = Path.Combine(AssetsDir, record.FileName);
            if (File.Exists(path))
            {
                _fileWriteTicks[path] = File.GetLastWriteTimeUtc(path).Ticks;
            }
        }
    }

    private void ScanExternalChanges()
    {
        var dirty = false;

        for (var i = _manifest.Assets.Count - 1; i >= 0; i--)
        {
            var record = _manifest.Assets[i];
            var path = Path.Combine(AssetsDir, record.FileName);

            if (!File.Exists(path))
            {
                _manifest.Assets.RemoveAt(i);
                _textureCache.Remove(record.Guid);
                _audioCache.Remove(record.Guid);
                _fileWriteTicks.Remove(path);
                OnAssetChanged?.Invoke(record.Guid);
                dirty = true;
                continue;
            }

            var ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (!_fileWriteTicks.TryGetValue(path, out var knownTicks) || ticks != knownTicks)
            {
                _fileWriteTicks[path] = ticks;
                record.Hash = ComputeHash(path);
                _textureCache.Remove(record.Guid);
                _audioCache.Remove(record.Guid);
                OnAssetChanged?.Invoke(record.Guid);
                dirty = true;
            }
        }

        if (dirty)
            SaveManifest();
    }

    private bool TryGetReferenceFile(string guid, out string fileName)
    {
        var projectDir = Path.Combine(Application.persistentDataPath, "ProjectData");
        if (!Directory.Exists(projectDir))
        {
            fileName = string.Empty;
            return false;
        }

        foreach (var jsonPath in Directory.GetFiles(projectDir, "*.json"))
        {
            if (Path.GetFileName(jsonPath).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(jsonPath);
            if (text.Contains(guid, StringComparison.Ordinal))
            {
                fileName = Path.GetFileName(jsonPath);
                return true;
            }
        }

        fileName = string.Empty;
        return false;
    }

    private static string ComputeHash(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static AssetKind DetectKind(string extension)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
                return AssetKind.Texture;
            case ".mp3":
            case ".ogg":
            case ".wav":
                return AssetKind.Bgm;
            case ".ttf":
            case ".otf":
                return AssetKind.Font;
            default:
                return AssetKind.Unknown;
        }
    }

    private static bool IsSupportedExtensionForKind(AssetKind kind, string extension)
    {
        var normalized = extension.ToLowerInvariant();
        return kind switch
        {
            AssetKind.Texture => normalized == ".png" || normalized == ".jpg" || normalized == ".jpeg",
            AssetKind.Bgm => normalized == ".mp3" || normalized == ".ogg" || normalized == ".wav",
            AssetKind.Se => normalized == ".mp3" || normalized == ".ogg" || normalized == ".wav",
            AssetKind.Font => normalized == ".ttf" || normalized == ".otf",
            _ => false
        };
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9')
               || (c >= 'a' && c <= 'f')
               || (c >= 'A' && c <= 'F');
    }

    private static AudioType GetAudioTypeFromExtension(string extension)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".mp3": return AudioType.MPEG;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            default: return AudioType.UNKNOWN;
        }
    }
}

[RequireComponent(typeof(UIDocument))]
public class AssetManagerPanel : MonoBehaviour
{
    private ListView _assetListView;
    private TextField _sourcePathField;
    private Label _statusLabel;
    private Label _selectedGuidLabel;

    private readonly List<AssetRecord> _items = new();
    private bool _isUiBuilt;
    private string _selectedGuid;

    private void OnEnable()
    {
        EnsureManager();
        BuildUi();
        RefreshList();

        if (AssetManager.Instance != null)
            AssetManager.Instance.OnAssetChanged += HandleAssetChanged;
    }

    private void OnDisable()
    {
        if (AssetManager.Instance != null)
            AssetManager.Instance.OnAssetChanged -= HandleAssetChanged;
    }

    private void EnsureManager()
    {
        if (AssetManager.Instance != null) return;

        var manager = FindFirstObjectByType<AssetManager>();
        if (manager == null)
        {
            var go = new GameObject("AssetManager");
            manager = go.AddComponent<AssetManager>();
        }
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[AssetManagerPanel] UIDocument root not found.");
            return;
        }

        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        var pathRow = new VisualElement();
        pathRow.style.flexDirection = FlexDirection.Row;
        pathRow.style.marginBottom = 6;
        root.Add(pathRow);

        _sourcePathField = new TextField("Source") { name = "SourcePathField" };
        _sourcePathField.style.flexGrow = 1;
        pathRow.Add(_sourcePathField);

        var browseButton = new Button(BrowseFile) { text = "Browse" };
        browseButton.style.marginLeft = 4;
        pathRow.Add(browseButton);

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.flexWrap = Wrap.Wrap;
        buttonRow.style.marginBottom = 6;
        root.Add(buttonRow);

        AddButton(buttonRow, "Import Texture", () => ImportByKind(AssetKind.Texture));
        AddButton(buttonRow, "Import BGM", () => ImportByKind(AssetKind.Bgm));
        AddButton(buttonRow, "Import SE", () => ImportByKind(AssetKind.Se));
        AddButton(buttonRow, "Import Font", () => ImportByKind(AssetKind.Font));
        AddButton(buttonRow, "Replace Selected", ReplaceSelected);
        AddButton(buttonRow, "Delete Selected", DeleteSelected);
        AddButton(buttonRow, "Reload", RefreshList);

        _selectedGuidLabel = new Label("Selected GUID: -");
        _selectedGuidLabel.style.marginBottom = 4;
        root.Add(_selectedGuidLabel);

        _assetListView = new ListView();
        _assetListView.style.flexGrow = 1;
        _assetListView.selectionType = SelectionType.Single;
        _assetListView.makeItem = () => new Label();
        _assetListView.bindItem = (element, index) =>
        {
            var item = _items[index];
            (element as Label).text = $"{item.Kind,-7} | {item.OriginalFileName} | {item.Guid}";
        };
        _assetListView.selectionChanged += _ =>
        {
            var index = _assetListView.selectedIndex;
            _selectedGuid = index >= 0 && index < _items.Count ? _items[index].Guid : string.Empty;
            _selectedGuidLabel.text = string.IsNullOrWhiteSpace(_selectedGuid)
                ? "Selected GUID: -"
                : $"Selected GUID: {_selectedGuid}";
        };
        root.Add(_assetListView);

        _statusLabel = new Label();
        _statusLabel.style.marginTop = 6;
        root.Add(_statusLabel);

        _isUiBuilt = true;
    }

    private void ImportByKind(AssetKind kind)
    {
        if (AssetManager.Instance == null)
        {
            SetStatus("AssetManager not found.");
            return;
        }

        var sourcePath = _sourcePathField?.value?.Trim();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            SetStatus("ファイルパスを指定してください。");
            return;
        }

        string guid;
        switch (kind)
        {
            case AssetKind.Texture:
                guid = AssetManager.Instance.ImportTexture(sourcePath);
                break;
            case AssetKind.Bgm:
                guid = AssetManager.Instance.ImportAudio(sourcePath);
                break;
            case AssetKind.Se:
                guid = AssetManager.Instance.ImportSe(sourcePath);
                break;
            case AssetKind.Font:
                guid = AssetManager.Instance.ImportFont(sourcePath);
                break;
            default:
                guid = string.Empty;
                break;
        }

        if (string.IsNullOrEmpty(guid))
        {
            SetStatus("インポートに失敗しました。拡張子またはパスを確認してください。");
            return;
        }

        _selectedGuid = guid;
        RefreshList();
        SetStatus($"インポート完了: {guid}");
    }

    private void ReplaceSelected()
    {
        if (AssetManager.Instance == null)
        {
            SetStatus("AssetManager not found.");
            return;
        }

        var sourcePath = _sourcePathField?.value?.Trim();
        if (string.IsNullOrWhiteSpace(_selectedGuid) || string.IsNullOrWhiteSpace(sourcePath))
        {
            SetStatus("GUID選択とファイルパス指定が必要です。");
            return;
        }

        var ok = AssetManager.Instance.ReplaceAsset(_selectedGuid, sourcePath);
        RefreshList();
        SetStatus(ok ? "置換しました。" : "置換に失敗しました（種別不一致の可能性があります）。");
    }

    private void DeleteSelected()
    {
        if (AssetManager.Instance == null)
        {
            SetStatus("AssetManager not found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedGuid))
        {
            SetStatus("削除対象を選択してください。");
            return;
        }

        var ok = AssetManager.Instance.DeleteAsset(_selectedGuid, out var reason);
        if (ok)
            _selectedGuid = string.Empty;

        RefreshList();
        SetStatus(ok ? "削除しました。" : reason);
    }

    private void HandleAssetChanged(string guid)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        if (_assetListView == null || AssetManager.Instance == null) return;

        _items.Clear();
        _items.AddRange(AssetManager.Instance.GetAssets());

        _assetListView.itemsSource = _items;
        _assetListView.Rebuild();

        if (!string.IsNullOrWhiteSpace(_selectedGuid))
        {
            var selectedIndex = _items.FindIndex(i => i.Guid == _selectedGuid);
            if (selectedIndex >= 0)
                _assetListView.SetSelection(selectedIndex);
            else
                _selectedGuid = string.Empty;
        }

        _selectedGuidLabel.text = string.IsNullOrWhiteSpace(_selectedGuid)
            ? "Selected GUID: -"
            : $"Selected GUID: {_selectedGuid}";
    }

    private void BrowseFile()
    {
#if UNITY_EDITOR
        var path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Select Asset", string.Empty,
            new[]
            {
                "All Supported", "png,jpg,jpeg,mp3,ogg,wav,ttf,otf",
                "Image", "png,jpg,jpeg",
                "Audio", "mp3,ogg,wav",
                "Font", "ttf,otf"
            });

        if (!string.IsNullOrWhiteSpace(path) && _sourcePathField != null)
            _sourcePathField.value = path;
#else
        SetStatus("この環境ではBrowse不可です。パスを直接入力してください。");
#endif
    }

    private void AddButton(VisualElement parent, string text, Action callback)
    {
        var button = new Button(() => callback?.Invoke()) { text = text };
        button.style.marginRight = 4;
        button.style.marginBottom = 4;
        parent.Add(button);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message;
    }
}
