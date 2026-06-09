using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class PresetData
{
    public string Name;
    public EnemyListData Enemies;
    public WeaponListData Weapons;
    public WaveListData Waves;
    public PassiveListData Passives;
    public GameSettingsData GameSettings;
}

[RequireComponent(typeof(UIDocument))]
public class PresetEditor : MonoBehaviour
{
    private const string PresetFolderName = "presets";
    private const string EnemyFileName = "enemies.json";
    private const string WeaponFileName = "weapons.json";
    private const string WaveFileName = "waves.json";
    private const string PassiveFileName = "passives.json";
    private const string GameSettingsFileName = "game_settings.json";

    private readonly List<PresetData> _customPresets = new();
    private readonly List<PresetData> _builtinPresets = new();

    private TextField _presetNameField;
    private ListView _customPresetList;
    private ListView _builtinPresetList;
    private int _selectedCustomIndex = -1;
    private int _selectedBuiltinIndex = -1;
    private bool _isUiBuilt;

    private void OnEnable()
    {
        BuildUi();
        BuildBuiltinPresets();
        ScanCustomPresets();
        RefreshLists();
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[PresetEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;
        root.style.paddingTop = 12f;
        root.style.paddingBottom = 12f;

        var headerRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 8f
            }
        };
        var headerLabel = new Label("プリセット");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.flexGrow = 1f;
        headerRow.Add(headerLabel);

        var savePresetBtn = new Button(() => SaveCurrentAsPreset(_presetNameField?.value))
        {
            name = "SavePresetBtn",
            text = "現在の設定を保存"
        };
        headerRow.Add(savePresetBtn);
        root.Add(headerRow);

        var presetNameRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 8f
            }
        };
        _presetNameField = new TextField("保存名")
        {
            name = "PresetNameField"
        };
        _presetNameField.style.flexGrow = 1f;
        presetNameRow.Add(_presetNameField);
        root.Add(presetNameRow);

        var customLabel = new Label("カスタムプリセット");
        customLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        customLabel.style.marginTop = 8f;
        root.Add(customLabel);

        _customPresetList = new ListView
        {
            name = "CustomPresetList",
            selectionType = SelectionType.Single
        };
        _customPresetList.style.minHeight = 120f;
        _customPresetList.makeItem = () => new Label();
        _customPresetList.bindItem = (element, index) =>
        {
            if (element is Label label)
                label.text = _customPresets[index]?.Name ?? "-";
        };
        _customPresetList.selectionChanged += _ => _selectedCustomIndex = _customPresetList.selectedIndex;
        root.Add(_customPresetList);

        var customButtonRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginTop = 6f,
                marginBottom = 8f
            }
        };
        var loadCustomBtn = new Button(LoadSelectedCustomPreset)
        {
            name = "LoadCustomBtn",
            text = "読み込み"
        };
        var deletePresetBtn = new Button(DeleteSelectedCustomPreset)
        {
            name = "DeletePresetBtn",
            text = "削除"
        };
        loadCustomBtn.style.flexGrow = 1f;
        deletePresetBtn.style.flexGrow = 1f;
        customButtonRow.Add(loadCustomBtn);
        customButtonRow.Add(deletePresetBtn);
        root.Add(customButtonRow);

        var builtinLabel = new Label("ビルトインプリセット");
        builtinLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        builtinLabel.style.marginTop = 8f;
        root.Add(builtinLabel);

        _builtinPresetList = new ListView
        {
            name = "BuiltinPresetList",
            selectionType = SelectionType.Single
        };
        _builtinPresetList.style.minHeight = 80f;
        _builtinPresetList.makeItem = () => new Label();
        _builtinPresetList.bindItem = (element, index) =>
        {
            if (element is Label label)
                label.text = _builtinPresets[index]?.Name ?? "-";
        };
        _builtinPresetList.selectionChanged += _ => _selectedBuiltinIndex = _builtinPresetList.selectedIndex;
        root.Add(_builtinPresetList);

        var loadBuiltinBtn = new Button(LoadSelectedBuiltinPreset)
        {
            name = "LoadBuiltinBtn",
            text = "読み込み"
        };
        root.Add(loadBuiltinBtn);

        _isUiBuilt = true;
    }

    private void SaveCurrentAsPreset(string presetName)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[PresetEditor] DataManager.Instance is null.");
            return;
        }

        var normalizedName = (presetName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            Debug.LogWarning("[PresetEditor] Preset name is empty.");
            return;
        }

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Debug.LogWarning("[PresetEditor] Preset name contains invalid file name characters.");
            return;
        }

        var preset = new PresetData
        {
            Name = normalizedName,
            Enemies = DataManager.Instance.Load<EnemyListData>(EnemyFileName),
            Weapons = DataManager.Instance.Load<WeaponListData>(WeaponFileName),
            Waves = DataManager.Instance.Load<WaveListData>(WaveFileName),
            Passives = DataManager.Instance.Load<PassiveListData>(PassiveFileName),
            GameSettings = DataManager.Instance.Load<GameSettingsData>(GameSettingsFileName)
        };

        var presetDir = GetPresetDirectoryPath();
        Directory.CreateDirectory(presetDir);

        var filePath = GetPresetFilePath(normalizedName);
        File.WriteAllText(filePath, JsonUtility.ToJson(preset, true));
        Debug.Log($"[PresetEditor] Saved custom preset: {filePath}");

        ScanCustomPresets();
        RefreshLists();
    }

    private void LoadPreset(PresetData preset)
    {
        if (preset == null)
            return;

        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[PresetEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(preset.Enemies ?? new EnemyListData(), EnemyFileName);
        DataManager.Instance.Save(preset.Weapons ?? new WeaponListData(), WeaponFileName);
        DataManager.Instance.Save(preset.Waves ?? new WaveListData(), WaveFileName);
        DataManager.Instance.Save(preset.Passives ?? new PassiveListData(), PassiveFileName);
        DataManager.Instance.Save(preset.GameSettings ?? new GameSettingsData(), GameSettingsFileName);

        EventBus.Publish(new LoadAllRequestedEvent());
        Debug.Log($"[PresetEditor] Loaded preset: {preset.Name}");
    }

    private void LoadSelectedCustomPreset()
    {
        if (_selectedCustomIndex < 0 || _selectedCustomIndex >= _customPresets.Count)
            return;

        LoadPreset(_customPresets[_selectedCustomIndex]);
    }

    private void LoadSelectedBuiltinPreset()
    {
        if (_selectedBuiltinIndex < 0 || _selectedBuiltinIndex >= _builtinPresets.Count)
            return;

        LoadPreset(_builtinPresets[_selectedBuiltinIndex]);
    }

    private void DeleteSelectedCustomPreset()
    {
        if (_selectedCustomIndex < 0 || _selectedCustomIndex >= _customPresets.Count)
            return;

        DeleteCustomPreset(_customPresets[_selectedCustomIndex]?.Name);
    }

    private void ScanCustomPresets()
    {
        _customPresets.Clear();

        var presetDir = GetPresetDirectoryPath();
        if (!Directory.Exists(presetDir))
        {
            Directory.CreateDirectory(presetDir);
            return;
        }

        var files = Directory.GetFiles(presetDir, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var filePath in files)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var preset = JsonUtility.FromJson<PresetData>(json);
                if (preset == null)
                    continue;

                preset.Name = string.IsNullOrWhiteSpace(preset.Name)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : preset.Name;
                _customPresets.Add(preset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PresetEditor] Failed to load preset '{filePath}': {ex.Message}");
            }
        }

        _customPresets.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });
        _selectedCustomIndex = -1;
    }

    private void DeleteCustomPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var filePath = GetPresetFilePath(name.Trim());
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"[PresetEditor] Deleted custom preset: {filePath}");
        }

        ScanCustomPresets();
        RefreshLists();
    }

    private void RefreshLists()
    {
        if (_customPresetList != null)
        {
            _customPresetList.itemsSource = _customPresets;
            _customPresetList.Rebuild();
            _customPresetList.ClearSelection();
        }

        if (_builtinPresetList != null)
        {
            _builtinPresetList.itemsSource = _builtinPresets;
            _builtinPresetList.Rebuild();
            _builtinPresetList.ClearSelection();
        }

        _selectedCustomIndex = -1;
        _selectedBuiltinIndex = -1;
    }

    private void BuildBuiltinPresets()
    {
        _builtinPresets.Clear();
        _builtinPresets.Add(CreateSlimeRushPreset());
        _builtinPresets.Add(CreateArcherHellPreset());
    }

    private static string GetPresetDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "ProjectData", PresetFolderName);
    }

    private static string GetPresetFilePath(string presetName)
    {
        return Path.Combine(GetPresetDirectoryPath(), $"{presetName}.json");
    }

    private static PresetData CreateSlimeRushPreset()
    {
        return new PresetData
        {
            Name = "🐌 スライムラッシュ",
            Enemies = new EnemyListData
            {
                Enemies = new List<EnemyData>
                {
                    new()
                    {
                        Id = "enemy_slime",
                        Name = "スライム",
                        Type = EnemyType.Melee,
                        Hp = 20,
                        Atk = 3,
                        MoveSpeed = 3f,
                        ExpValue = 5,
                        DropRate = 1.0f
                    }
                }
            },
            Weapons = new WeaponListData
            {
                Weapons = new List<WeaponData>
                {
                    new()
                    {
                        Id = "weapon_sword",
                        Name = "ソード",
                        Type = WeaponType.Melee,
                        Damage = 20,
                        Cooldown = 0.7f,
                        Range = 1.8f
                    }
                }
            },
            Waves = new WaveListData
            {
                Waves = new List<WaveEntry>
                {
                    new()
                    {
                        WaveNumber = 1,
                        StartTimeSec = 0f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_slime", Count = 10, SpawnInterval = 0.3f }
                        }
                    },
                    new()
                    {
                        WaveNumber = 2,
                        StartTimeSec = 30f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_slime", Count = 20, SpawnInterval = 0.2f }
                        }
                    },
                    new()
                    {
                        WaveNumber = 3,
                        StartTimeSec = 60f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_slime", Count = 40, SpawnInterval = 0.1f }
                        }
                    }
                }
            },
            Passives = new PassiveListData(),
            GameSettings = new GameSettingsData
            {
                TimeLimitSec = 180,
                PlayerMaxHp = 100,
                PlayerMoveSpeed = 4f,
                InvincibleSec = 0.8f,
                ExpMultiplier = 1f
            }
        };
    }

    private static PresetData CreateArcherHellPreset()
    {
        return new PresetData
        {
            Name = "🏹 アーチャー地獄",
            Enemies = new EnemyListData
            {
                Enemies = new List<EnemyData>
                {
                    new()
                    {
                        Id = "enemy_archer",
                        Name = "アーチャー",
                        Type = EnemyType.Ranged,
                        Hp = 15,
                        Atk = 8,
                        MoveSpeed = 1.5f,
                        AttackRange = 3f,
                        ShootCooldown = 2.0f,
                        ProjectileDamage = 8,
                        ProjectileSpeed = 5f,
                        ExpValue = 20,
                        DropRate = 0.6f
                    }
                }
            },
            Weapons = new WeaponListData
            {
                Weapons = new List<WeaponData>
                {
                    new()
                    {
                        Id = "weapon_nova",
                        Name = "ノヴァ",
                        Type = WeaponType.Area,
                        Damage = 12,
                        Cooldown = 0.8f,
                        Range = 7f
                    }
                }
            },
            Waves = new WaveListData
            {
                Waves = new List<WaveEntry>
                {
                    new()
                    {
                        WaveNumber = 1,
                        StartTimeSec = 0f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_archer", Count = 3, SpawnInterval = 1.0f }
                        }
                    },
                    new()
                    {
                        WaveNumber = 2,
                        StartTimeSec = 30f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_archer", Count = 6, SpawnInterval = 0.8f }
                        }
                    },
                    new()
                    {
                        WaveNumber = 3,
                        StartTimeSec = 60f,
                        SpawnGroups = new List<SpawnGroup>
                        {
                            new() { EnemyId = "enemy_archer", Count = 12, SpawnInterval = 0.5f }
                        }
                    }
                }
            },
            Passives = new PassiveListData(),
            GameSettings = new GameSettingsData
            {
                TimeLimitSec = 120,
                PlayerMaxHp = 80,
                PlayerMoveSpeed = 5f,
                InvincibleSec = 0.6f,
                ExpMultiplier = 1.5f
            }
        };
    }
}
