using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class WaveEditor : MonoBehaviour
{
    private const string WaveFileName = "waves.json";
    private const string DefaultEnemyId = "enemy_slime";
    private const int DefaultSpawnCount = 5;
    private const float DefaultSpawnInterval = 0.5f;
    private const int MinSpawnCount = 1;
    private const float MinStartTimeSec = 0f;
    private const float MinSpawnIntervalSec = 0f;

    private WaveListData _waveList = new();

    private ListView _waveListView;
    private ListView _spawnGroupListView;

    private IntegerField _waveNumberField;
    private FloatField _startTimeField;

    private TextField _enemyIdField;
    private IntegerField _spawnCountField;
    private FloatField _spawnIntervalField;

    private TextField _jsonField;

    private int _selectedWaveIndex = -1;
    private int _selectedSpawnGroupIndex = -1;

    private bool _isUiBuilt;
    private bool _isBinding;

    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    public void Initialize(WaveListData data)
    {
        _waveList = data ?? new WaveListData();
        EnsureDataValidity(_waveList);
        BuildUI();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (_waveList == null || _waveList.Waves == null)
        {
            _waveList = DataManager.Instance != null
                ? DataManager.Instance.Load<WaveListData>(WaveFileName)
                : new WaveListData();
            EnsureDataValidity(_waveList);
        }

        BuildUI();
        RefreshAll();
        EventBus.Subscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
    }

    private void BuildUI()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[WaveEditor] UIDocument root not found.");
            return;
        }

        if (root.Q<ListView>("WaveList") == null)
        {
            BuildFallbackUI(root);
        }

        _waveListView = root.Q<ListView>("WaveList");
        if (_waveListView != null)
        {
            _waveListView.makeItem = () => new Label();
            _waveListView.bindItem = (e, i) =>
            {
                var wave = _waveList.Waves[i];
                if (wave == null)
                {
                    (e as Label).text = "-";
                    return;
                }

                (e as Label).text = $"Wave {wave.WaveNumber} - {wave.StartTimeSec:0.##}s";
            };
            _waveListView.selectionType = SelectionType.Single;
            _waveListView.selectionChanged += _ => ShowWaveDetail(_waveListView.selectedIndex);
        }

        _spawnGroupListView = root.Q<ListView>("SpawnGroupList");
        if (_spawnGroupListView != null)
        {
            _spawnGroupListView.makeItem = () => new Label();
            _spawnGroupListView.bindItem = (e, i) =>
            {
                var group = GetSelectedWave()?.SpawnGroups[i];
                (e as Label).text = group == null
                    ? "-"
                    : $"{group.EnemyId} x{group.Count} ({group.SpawnInterval:0.##}s)";
            };
            _spawnGroupListView.selectionType = SelectionType.Single;
            _spawnGroupListView.selectionChanged += _ => ShowSpawnGroupDetail(_spawnGroupListView.selectedIndex);
        }

        _waveNumberField = root.Q<IntegerField>("WaveNumberField");
        _startTimeField = root.Q<FloatField>("StartTimeField");
        _enemyIdField = root.Q<TextField>("EnemyIdField");
        _spawnCountField = root.Q<IntegerField>("SpawnCountField");
        _spawnIntervalField = root.Q<FloatField>("SpawnIntervalField");
        _jsonField = root.Q<TextField>("JsonField");

        if (_waveNumberField != null)
        {
            _waveNumberField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;
                var wave = GetSelectedWave();
                if (wave == null) return;

                RecordUndoState();
                wave.WaveNumber = evt.newValue;
                RefreshAfterWaveMutation();
            });
        }

        if (_startTimeField != null)
        {
            _startTimeField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;
                var wave = GetSelectedWave();
                if (wave == null) return;

                RecordUndoState();
                wave.StartTimeSec = Mathf.Max(MinStartTimeSec, evt.newValue);
                RefreshAfterWaveMutation();
            });
        }

        if (_enemyIdField != null)
        {
            _enemyIdField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;
                var group = GetSelectedSpawnGroup();
                if (group == null) return;

                RecordUndoState();
                group.EnemyId = evt.newValue;
                RefreshAfterSpawnGroupMutation();
            });
        }

        if (_spawnCountField != null)
        {
            _spawnCountField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;
                var group = GetSelectedSpawnGroup();
                if (group == null) return;

                RecordUndoState();
                group.Count = Mathf.Max(MinSpawnCount, evt.newValue);
                RefreshAfterSpawnGroupMutation();
            });
        }

        if (_spawnIntervalField != null)
        {
            _spawnIntervalField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;
                var group = GetSelectedSpawnGroup();
                if (group == null) return;

                RecordUndoState();
                group.SpawnInterval = Mathf.Max(MinSpawnIntervalSec, evt.newValue);
                RefreshAfterSpawnGroupMutation();
            });
        }

        var addWaveBtn = root.Q<Button>("AddWaveBtn");
        if (addWaveBtn != null) addWaveBtn.clicked += AddWave;
        var deleteWaveBtn = root.Q<Button>("DeleteWaveBtn");
        if (deleteWaveBtn != null) deleteWaveBtn.clicked += DeleteWave;
        var addSpawnGroupBtn = root.Q<Button>("AddSpawnGroupBtn");
        if (addSpawnGroupBtn != null) addSpawnGroupBtn.clicked += AddSpawnGroup;
        var deleteSpawnGroupBtn = root.Q<Button>("DeleteSpawnGroupBtn");
        if (deleteSpawnGroupBtn != null) deleteSpawnGroupBtn.clicked += DeleteSpawnGroup;
        var saveBtn = root.Q<Button>("SaveBtn");
        if (saveBtn != null) saveBtn.clicked += Save;
        var undoBtn = root.Q<Button>("UndoBtn");
        if (undoBtn != null) undoBtn.clicked += Undo;
        var redoBtn = root.Q<Button>("RedoBtn");
        if (redoBtn != null) redoBtn.clicked += Redo;

        var applyJsonBtn = root.Q<Button>("ApplyJsonBtn");
        if (applyJsonBtn != null) applyJsonBtn.clicked += ApplyJson;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        _isUiBuilt = true;
    }

    private static void BuildFallbackUI(VisualElement root)
    {
        root.Clear();
        root.style.flexDirection = FlexDirection.Column;

        var toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 8f
            }
        };
        toolbar.Add(new Button { name = "UndoBtn", text = "Undo" });
        toolbar.Add(new Button { name = "RedoBtn", text = "Redo" });
        toolbar.Add(new Button { name = "SaveBtn", text = "保存" });
        root.Add(toolbar);

        var content = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1f
            }
        };

        var leftPane = new VisualElement
        {
            style =
            {
                width = 200f,
                marginRight = 8f
            }
        };
        leftPane.Add(new Label("Wave一覧") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        leftPane.Add(new ListView
        {
            name = "WaveList",
            style = { flexGrow = 1f, minHeight = 150f }
        });
        var waveButtonRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        waveButtonRow.Add(new Button { name = "AddWaveBtn", text = "追加" });
        waveButtonRow.Add(new Button { name = "DeleteWaveBtn", text = "削除" });
        leftPane.Add(waveButtonRow);

        var midPane = new VisualElement
        {
            style =
            {
                width = 240f,
                marginRight = 8f
            }
        };
        midPane.Add(new Label("Wave詳細") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        midPane.Add(new IntegerField("Wave番号") { name = "WaveNumberField" });
        midPane.Add(new FloatField("開始時刻 (秒)") { name = "StartTimeField" });
        midPane.Add(new Label("スポーングループ") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f } });
        midPane.Add(new ListView
        {
            name = "SpawnGroupList",
            style = { flexGrow = 1f, minHeight = 100f }
        });
        var spawnGroupButtonRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        spawnGroupButtonRow.Add(new Button { name = "AddSpawnGroupBtn", text = "追加" });
        spawnGroupButtonRow.Add(new Button { name = "DeleteSpawnGroupBtn", text = "削除" });
        midPane.Add(spawnGroupButtonRow);

        var rightPane = new VisualElement
        {
            style =
            {
                flexGrow = 1f
            }
        };
        rightPane.Add(new Label("スポーングループ詳細") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        rightPane.Add(new TextField("敵ID") { name = "EnemyIdField" });
        rightPane.Add(new IntegerField("スポーン数") { name = "SpawnCountField" });
        rightPane.Add(new FloatField("間隔 (秒)") { name = "SpawnIntervalField" });
        rightPane.Add(new Label("JSON") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f } });
        rightPane.Add(new TextField
        {
            name = "JsonField",
            multiline = true,
            style = { flexGrow = 1f, minHeight = 80f }
        });
        rightPane.Add(new Button { name = "ApplyJsonBtn", text = "JSON適用" });

        content.Add(leftPane);
        content.Add(midPane);
        content.Add(rightPane);
        root.Add(content);
    }

    private void AddWave()
    {
        RecordUndoState();

        _waveList.Waves.Add(new WaveEntry
        {
            WaveNumber = _waveList.Waves.Count + 1,
            StartTimeSec = _waveList.Waves.Count * 30f,
            SpawnGroups = new List<SpawnGroup>
            {
                new SpawnGroup
                {
                    EnemyId = DefaultEnemyId,
                    Count = DefaultSpawnCount,
                    SpawnInterval = DefaultSpawnInterval
                }
            }
        });

        _selectedWaveIndex = _waveList.Waves.Count - 1;
        _selectedSpawnGroupIndex = 0;
        RefreshAll();
        ShowWaveDetail(_selectedWaveIndex);
    }

    private void DeleteWave()
    {
        var idx = _waveListView != null ? _waveListView.selectedIndex : _selectedWaveIndex;
        if (idx < 0 || idx >= _waveList.Waves.Count)
            return;

        RecordUndoState();
        _waveList.Waves.RemoveAt(idx);

        _selectedWaveIndex = _waveList.Waves.Count == 0
            ? -1
            : Mathf.Clamp(idx, 0, _waveList.Waves.Count - 1);
        _selectedSpawnGroupIndex = -1;

        RefreshAll();
    }

    private void AddSpawnGroup()
    {
        var wave = GetSelectedWave();
        if (wave == null) return;

        RecordUndoState();

        wave.SpawnGroups ??= new List<SpawnGroup>();
        wave.SpawnGroups.Add(new SpawnGroup
        {
            EnemyId = DefaultEnemyId,
            Count = DefaultSpawnCount,
            SpawnInterval = DefaultSpawnInterval,
            Position = SpawnPosition.RandomEdge
        });

        _selectedSpawnGroupIndex = wave.SpawnGroups.Count - 1;
        RefreshAfterSpawnGroupMutation();
    }

    private void DeleteSpawnGroup()
    {
        var wave = GetSelectedWave();
        if (wave?.SpawnGroups == null || wave.SpawnGroups.Count == 0) return;

        var idx = _spawnGroupListView != null ? _spawnGroupListView.selectedIndex : _selectedSpawnGroupIndex;
        if (idx < 0 || idx >= wave.SpawnGroups.Count) return;

        RecordUndoState();
        wave.SpawnGroups.RemoveAt(idx);

        _selectedSpawnGroupIndex = wave.SpawnGroups.Count == 0
            ? -1
            : Mathf.Clamp(idx, 0, wave.SpawnGroups.Count - 1);

        RefreshAfterSpawnGroupMutation();
    }

    private void ShowWaveDetail(int index)
    {
        _selectedWaveIndex = index;

        var wave = GetSelectedWave();
        _selectedSpawnGroupIndex = wave?.SpawnGroups != null && wave.SpawnGroups.Count > 0 ? 0 : -1;
        _isBinding = true;

        if (_waveNumberField != null)
            _waveNumberField.SetValueWithoutNotify(wave?.WaveNumber ?? 0);
        if (_startTimeField != null)
            _startTimeField.SetValueWithoutNotify(wave?.StartTimeSec ?? 0f);

        if (_spawnGroupListView != null)
        {
            _spawnGroupListView.itemsSource = wave?.SpawnGroups;
            _spawnGroupListView.Rebuild();
            if (wave?.SpawnGroups != null && wave.SpawnGroups.Count > 0)
                _spawnGroupListView.SetSelection(0);
            else
                ShowSpawnGroupDetail(-1);
        }

        _isBinding = false;
    }

    private void ShowSpawnGroupDetail(int index)
    {
        _selectedSpawnGroupIndex = index;
        var group = GetSelectedSpawnGroup();

        _isBinding = true;
        if (_enemyIdField != null)
            _enemyIdField.SetValueWithoutNotify(group?.EnemyId ?? string.Empty);
        if (_spawnCountField != null)
            _spawnCountField.SetValueWithoutNotify(group?.Count ?? 0);
        if (_spawnIntervalField != null)
            _spawnIntervalField.SetValueWithoutNotify(group?.SpawnInterval ?? 0f);
        _isBinding = false;
    }

    private void Save()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[WaveEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_waveList, WaveFileName);
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void ApplyJson()
    {
        if (_jsonField == null) return;

        var json = _jsonField.value;
        if (string.IsNullOrWhiteSpace(json)) return;

        var parsed = JsonUtility.FromJson<WaveListData>(json);
        if (parsed == null)
        {
            Debug.LogWarning("[WaveEditor] JSON parse failed.");
            return;
        }

        RecordUndoState();
        EnsureDataValidity(parsed);
        _waveList = parsed;
        _selectedWaveIndex = -1;
        _selectedSpawnGroupIndex = -1;
        RefreshAll();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var current = Serialize(_waveList);
        var previous = _undoStack.Pop();
        var deserialized = TryDeserialize(previous);
        if (deserialized == null)
        {
            _undoStack.Push(previous);
            return;
        }

        _redoStack.Push(current);
        _waveList = deserialized;
        _selectedWaveIndex = -1;
        _selectedSpawnGroupIndex = -1;
        RefreshAll();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        var current = Serialize(_waveList);
        var next = _redoStack.Pop();
        var deserialized = TryDeserialize(next);
        if (deserialized == null)
        {
            _redoStack.Push(next);
            return;
        }

        _undoStack.Push(current);
        _waveList = deserialized;
        _selectedWaveIndex = -1;
        _selectedSpawnGroupIndex = -1;
        RefreshAll();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        var actionKey = evt.ctrlKey || evt.commandKey;
        if (!actionKey) return;

        if (evt.keyCode == KeyCode.Z && !evt.shiftKey)
        {
            Undo();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey))
        {
            Redo();
            evt.StopPropagation();
        }
    }

    private void RecordUndoState()
    {
        _undoStack.Push(Serialize(_waveList));
        _redoStack.Clear();
    }

    private void RefreshAll()
    {
        EnsureDataValidity(_waveList);

        if (_waveListView != null)
        {
            _waveListView.itemsSource = _waveList.Waves;
            _waveListView.Rebuild();

            if (_selectedWaveIndex >= 0 && _selectedWaveIndex < _waveList.Waves.Count)
                _waveListView.SetSelection(_selectedWaveIndex);
            else
                ShowWaveDetail(-1);
        }

        UpdateJsonField();
    }

    private void RefreshAfterWaveMutation()
    {
        if (_waveListView != null)
            _waveListView.Rebuild();
        UpdateJsonField();
    }

    private void RefreshAfterSpawnGroupMutation()
    {
        if (_spawnGroupListView != null)
            _spawnGroupListView.Rebuild();
        UpdateJsonField();
    }

    private void UpdateJsonField()
    {
        if (_jsonField == null) return;
        _jsonField.SetValueWithoutNotify(Serialize(_waveList, true));
    }

    private WaveEntry GetSelectedWave()
    {
        if (_selectedWaveIndex < 0 || _selectedWaveIndex >= _waveList.Waves.Count)
            return null;
        return _waveList.Waves[_selectedWaveIndex];
    }

    private SpawnGroup GetSelectedSpawnGroup()
    {
        var wave = GetSelectedWave();
        if (wave?.SpawnGroups == null) return null;

        if (_selectedSpawnGroupIndex < 0 || _selectedSpawnGroupIndex >= wave.SpawnGroups.Count)
            return null;

        return wave.SpawnGroups[_selectedSpawnGroupIndex];
    }

    private static string Serialize(WaveListData data, bool pretty = false)
    {
        return JsonUtility.ToJson(data ?? new WaveListData(), pretty);
    }

    private static WaveListData Deserialize(string json)
    {
        var parsed = JsonUtility.FromJson<WaveListData>(json);
        parsed ??= new WaveListData();
        EnsureDataValidity(parsed);
        return parsed;
    }

    private static WaveListData TryDeserialize(string json)
    {
        try
        {
            return Deserialize(json);
        }
        catch (System.ArgumentException ex)
        {
            Debug.LogWarning($"[WaveEditor] Failed to restore state from JSON: {ex.Message}");
            return null;
        }
    }

    private static void EnsureDataValidity(WaveListData data)
    {
        if (data == null) return;

        data.Waves ??= new List<WaveEntry>();
        foreach (var wave in data.Waves)
        {
            if (wave == null) continue;
            wave.SpawnGroups ??= new List<SpawnGroup>();
            foreach (var group in wave.SpawnGroups)
            {
                if (group == null) continue;
                group.EnemyId ??= string.Empty;
                group.Count = Mathf.Max(MinSpawnCount, group.Count);
                group.SpawnInterval = Mathf.Max(MinSpawnIntervalSec, group.SpawnInterval);
            }
        }
    }
}
