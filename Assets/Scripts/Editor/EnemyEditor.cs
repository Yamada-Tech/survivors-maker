using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EnemyEditor : MonoBehaviour
{
    private const string EnemyFileName = "enemies.json";

    private EnemyListData _enemyList;
    private ListView _listView;
    private VisualElement _detailPanel;

    private TextField _nameField;
    private EnumField _typeField;
    private IntegerField _hpField;
    private IntegerField _atkField;
    private FloatField _moveSpeedField;
    private FloatField _dropRateField;
    private IntegerField _expValueField;
    private TextField _spriteIdField;

    private bool _isUiBuilt;
    private bool _isBinding;
    private int _selectedIndex = -1;

    public void Initialize(EnemyListData data)
    {
        _enemyList = data;
        EnsureEnemyList();
        BuildUI();
        RefreshList();
    }

    private void OnEnable()
    {
        if (_enemyList == null || _enemyList.Enemies == null)
        {
            _enemyList = DataManager.Instance != null
                ? DataManager.Instance.Load<EnemyListData>(EnemyFileName)
                : new EnemyListData();
        }

        EnsureEnemyList();
        BuildUI();
        RefreshList();
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
            Debug.LogWarning("[EnemyEditor] UIDocument root not found.");
            return;
        }

        _listView = root.Q<ListView>("EnemyList");
        if (_listView != null)
        {
            _listView.makeItem = () => new Label();
            _listView.bindItem = (e, i) =>
            {
                var enemy = _enemyList.Enemies[i];
                (e as Label).text = enemy == null || string.IsNullOrWhiteSpace(enemy.Name)
                    ? $"Enemy {i + 1}"
                    : enemy.Name;
            };
            _listView.itemsSource = _enemyList.Enemies;
            _listView.selectionType = SelectionType.Single;
            _listView.selectionChanged += _ => ShowDetail(_listView.selectedIndex);
        }

        _detailPanel = root.Q("DetailPanel");

        _nameField = root.Q<TextField>("NameField");
        _typeField = root.Q<EnumField>("TypeField");
        _hpField = root.Q<IntegerField>("HpField");
        _atkField = root.Q<IntegerField>("AtkField");
        _moveSpeedField = root.Q<FloatField>("MoveSpeedField");
        _dropRateField = root.Q<FloatField>("DropRateField");
        _expValueField = root.Q<IntegerField>("ExpValueField");
        _spriteIdField = root.Q<TextField>("SpriteIdField");

        RegisterCallbacks();

        var addBtn = root.Q<Button>("AddBtn");
        if (addBtn != null) addBtn.clicked += AddEnemy;
        var deleteBtn = root.Q<Button>("DeleteBtn");
        if (deleteBtn != null) deleteBtn.clicked += DeleteEnemy;
        var saveBtn = root.Q<Button>("SaveBtn");
        if (saveBtn != null) saveBtn.clicked += Save;

        _isUiBuilt = true;
    }

    private void RegisterCallbacks()
    {
        if (_nameField != null)
        {
            _nameField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.Name = evt.newValue;
                RefreshListLabelsOnly();
            });
        }

        if (_typeField != null)
        {
            _typeField.Init(EnemyType.Melee);
            _typeField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                if (evt.newValue is EnemyType newType)
                    enemy.Type = newType;
            });
        }

        if (_hpField != null)
        {
            _hpField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.Hp = Mathf.Max(0, evt.newValue);
                if (enemy.Hp != evt.newValue)
                    _hpField.SetValueWithoutNotify(enemy.Hp);
            });
        }

        if (_atkField != null)
        {
            _atkField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.Atk = Mathf.Max(0, evt.newValue);
                if (enemy.Atk != evt.newValue)
                    _atkField.SetValueWithoutNotify(enemy.Atk);
            });
        }

        if (_moveSpeedField != null)
        {
            _moveSpeedField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.MoveSpeed = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(enemy.MoveSpeed, evt.newValue))
                    _moveSpeedField.SetValueWithoutNotify(enemy.MoveSpeed);
            });
        }

        if (_dropRateField != null)
        {
            _dropRateField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.DropRate = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(enemy.DropRate, evt.newValue))
                    _dropRateField.SetValueWithoutNotify(enemy.DropRate);
            });
        }

        if (_expValueField != null)
        {
            _expValueField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.ExpValue = Mathf.Max(0, evt.newValue);
                if (enemy.ExpValue != evt.newValue)
                    _expValueField.SetValueWithoutNotify(enemy.ExpValue);
            });
        }

        if (_spriteIdField != null)
        {
            _spriteIdField.RegisterValueChangedCallback(evt =>
            {
                var enemy = GetSelectedEnemy();
                if (_isBinding || enemy == null) return;

                enemy.SpriteId = evt.newValue;
            });
        }
    }

    private void AddEnemy()
    {
        _enemyList.Enemies.Add(new EnemyData
        {
            Id = $"enemy_{Guid.NewGuid():N}",
            Name = "新しい敵",
            Type = EnemyType.Melee,
            SpriteId = string.Empty
        });

        _selectedIndex = _enemyList.Enemies.Count - 1;
        RefreshList();
    }

    private void DeleteEnemy()
    {
        var idx = _listView != null ? _listView.selectedIndex : _selectedIndex;
        if (idx < 0 || idx >= _enemyList.Enemies.Count)
            return;

        _enemyList.Enemies.RemoveAt(idx);
        _selectedIndex = _enemyList.Enemies.Count == 0 ? -1 : Mathf.Clamp(idx, 0, _enemyList.Enemies.Count - 1);
        RefreshList();
    }

    private void ShowDetail(int index)
    {
        _selectedIndex = index;
        var data = GetSelectedEnemy();

        if (_detailPanel != null)
            _detailPanel.SetEnabled(data != null);

        _isBinding = true;
        if (_nameField != null) _nameField.SetValueWithoutNotify(data?.Name ?? string.Empty);
        if (_typeField != null) _typeField.SetValueWithoutNotify(data?.Type ?? EnemyType.Melee);
        if (_hpField != null) _hpField.SetValueWithoutNotify(data?.Hp ?? 0);
        if (_atkField != null) _atkField.SetValueWithoutNotify(data?.Atk ?? 0);
        if (_moveSpeedField != null) _moveSpeedField.SetValueWithoutNotify(data?.MoveSpeed ?? 0f);
        if (_dropRateField != null) _dropRateField.SetValueWithoutNotify(data?.DropRate ?? 0f);
        if (_expValueField != null) _expValueField.SetValueWithoutNotify(data?.ExpValue ?? 0);
        if (_spriteIdField != null) _spriteIdField.SetValueWithoutNotify(data?.SpriteId ?? string.Empty);
        _isBinding = false;
    }

    private EnemyData GetSelectedEnemy()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _enemyList.Enemies.Count)
            return null;
        return _enemyList.Enemies[_selectedIndex];
    }

    private void RefreshList()
    {
        if (_listView == null) return;

        _listView.itemsSource = _enemyList.Enemies;
        _listView.Rebuild();

        if (_selectedIndex >= 0 && _selectedIndex < _enemyList.Enemies.Count)
        {
            _listView.SetSelection(_selectedIndex);
            ShowDetail(_selectedIndex);
        }
        else
        {
            ShowDetail(-1);
        }
    }

    private void RefreshListLabelsOnly()
    {
        _listView?.Rebuild();
    }

    private void Save()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[EnemyEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_enemyList, EnemyFileName);
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void EnsureEnemyList()
    {
        _enemyList ??= new EnemyListData();
        _enemyList.Enemies ??= new List<EnemyData>();
    }
}
