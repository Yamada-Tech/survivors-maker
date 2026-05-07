using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class WaveEditor : MonoBehaviour
{
    private const string WaveFileName = "waves.json";

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

        _waveListView = root.Q<ListView>("WaveList");
        if (_waveListView != null)
        {
            _waveListView.makeItem = () => new Label();
            _waveListView.bindItem = (e, i) =>
            {
                var wave = _waveList.Waves[i];
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
                wave.StartTimeSec = Mathf.Max(0f, evt.newValue);
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
                group.Count = Mathf.Max(1, evt.newValue);
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
                group.SpawnInterval = Mathf.Max(0f, evt.newValue);
                RefreshAfterSpawnGroupMutation();
            });
        }

        root.Q<Button>("AddWaveBtn")?.clicked += AddWave;
        root.Q<Button>("DeleteWaveBtn")?.clicked += DeleteWave;
        root.Q<Button>("AddSpawnGroupBtn")?.clicked += AddSpawnGroup;
        root.Q<Button>("DeleteSpawnGroupBtn")?.clicked += DeleteSpawnGroup;
        root.Q<Button>("SaveBtn")?.clicked += Save;
        root.Q<Button>("UndoBtn")?.clicked += Undo;
        root.Q<Button>("RedoBtn")?.clicked += Redo;

        root.Q<Button>("ApplyJsonBtn")?.clicked += ApplyJson;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        _isUiBuilt = true;
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
                new SpawnGroup { EnemyId = "Enemy_001", Count = 5, SpawnInterval = 0.5f }
            }
        });

        _selectedWaveIndex = _waveList.Waves.Count - 1;
        _selectedSpawnGroupIndex = 0;
        RefreshAll();
    }

    private void DeleteWave()
    {
        var idx = _waveListView != null ? _waveListView.selectedIndex : _selectedWaveIndex;
        if (idx < 0 || idx >= _waveList.Waves.Count)
            return;

        RecordUndoState();
        _waveList.Waves.RemoveAt(idx);

        _selectedWaveIndex = Mathf.Clamp(idx, 0, _waveList.Waves.Count - 1);
        if (_waveList.Waves.Count == 0) _selectedWaveIndex = -1;
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
            EnemyId = "Enemy_001",
            Count = 5,
            SpawnInterval = 0.5f,
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

        _selectedSpawnGroupIndex = Mathf.Clamp(idx, 0, wave.SpawnGroups.Count - 1);
        if (wave.SpawnGroups.Count == 0) _selectedSpawnGroupIndex = -1;

        RefreshAfterSpawnGroupMutation();
    }

    private void ShowWaveDetail(int index)
    {
        _selectedWaveIndex = index;
        _selectedSpawnGroupIndex = 0;

        var wave = GetSelectedWave();
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

        _redoStack.Push(Serialize(_waveList));
        _waveList = Deserialize(_undoStack.Pop());
        _selectedWaveIndex = -1;
        _selectedSpawnGroupIndex = -1;
        RefreshAll();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        _undoStack.Push(Serialize(_waveList));
        _waveList = Deserialize(_redoStack.Pop());
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
                group.Count = Mathf.Max(1, group.Count);
                group.SpawnInterval = Mathf.Max(0f, group.SpawnInterval);
            }
        }
    }
}
