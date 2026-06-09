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
        SeedDefaultEnemies();
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
        _detailPanel = root.Q("DetailPanel");

        _nameField = root.Q<TextField>("NameField");
        _typeField = root.Q<EnumField>("TypeField");
        _hpField = root.Q<IntegerField>("HpField");
        _atkField = root.Q<IntegerField>("AtkField");
        _moveSpeedField = root.Q<FloatField>("MoveSpeedField");
        _dropRateField = root.Q<FloatField>("DropRateField");
        _expValueField = root.Q<IntegerField>("ExpValueField");
        _spriteIdField = root.Q<TextField>("SpriteIdField");

        if (_listView == null || _detailPanel == null || _nameField == null || _typeField == null || _hpField == null ||
            _atkField == null || _moveSpeedField == null || _dropRateField == null || _expValueField == null || _spriteIdField == null)
        {
            BuildFallbackUI(root);

            _listView = root.Q<ListView>("EnemyList");
            _detailPanel = root.Q("DetailPanel");
            _nameField = root.Q<TextField>("NameField");
            _typeField = root.Q<EnumField>("TypeField");
            _hpField = root.Q<IntegerField>("HpField");
            _atkField = root.Q<IntegerField>("AtkField");
            _moveSpeedField = root.Q<FloatField>("MoveSpeedField");
            _dropRateField = root.Q<FloatField>("DropRateField");
            _expValueField = root.Q<IntegerField>("ExpValueField");
            _spriteIdField = root.Q<TextField>("SpriteIdField");
        }

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

        RegisterCallbacks();

        var addBtn = root.Q<Button>("AddBtn");
        if (addBtn != null) addBtn.clicked += AddEnemy;
        var deleteBtn = root.Q<Button>("DeleteBtn");
        if (deleteBtn != null) deleteBtn.clicked += DeleteEnemy;
        var saveBtn = root.Q<Button>("SaveBtn");
        if (saveBtn != null) saveBtn.clicked += Save;

        _isUiBuilt = true;
    }

    private static void BuildFallbackUI(VisualElement root)
    {
        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1f;

        var leftPane = new VisualElement
        {
            style =
            {
                width = 200f,
                minWidth = 200f,
                maxWidth = 200f,
                flexDirection = FlexDirection.Column,
                marginRight = 8f
            }
        };

        var listView = new ListView
        {
            name = "EnemyList",
            style =
            {
                flexGrow = 1f,
                minHeight = 200f
            }
        };
        leftPane.Add(listView);

        var buttonRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginTop = 6f
            }
        };

        buttonRow.Add(new Button { name = "AddBtn", text = "追加", style = { flexGrow = 1f } });
        buttonRow.Add(new Button { name = "DeleteBtn", text = "削除", style = { flexGrow = 1f } });
        buttonRow.Add(new Button { name = "SaveBtn", text = "保存", style = { flexGrow = 1f } });
        leftPane.Add(buttonRow);

        var rightPane = new VisualElement
        {
            style =
            {
                flexGrow = 1f,
                minWidth = 0f
            }
        };

        var scrollView = new ScrollView
        {
            style =
            {
                flexGrow = 1f
            }
        };

        var detailPanel = new VisualElement
        {
            name = "DetailPanel",
            style =
            {
                flexDirection = FlexDirection.Column,
                flexGrow = 1f
            }
        };

        detailPanel.Add(new TextField("Name") { name = "NameField" });
        detailPanel.Add(new EnumField("Type", EnemyType.Melee) { name = "TypeField" });
        detailPanel.Add(new IntegerField("HP") { name = "HpField" });
        detailPanel.Add(new IntegerField("ATK") { name = "AtkField" });
        detailPanel.Add(new FloatField("MoveSpeed") { name = "MoveSpeedField" });
        detailPanel.Add(new FloatField("DropRate") { name = "DropRateField" });
        detailPanel.Add(new IntegerField("ExpValue") { name = "ExpValueField" });
        detailPanel.Add(new TextField("SpriteId") { name = "SpriteIdField" });

        scrollView.Add(detailPanel);
        rightPane.Add(scrollView);

        root.Add(leftPane);
        root.Add(rightPane);
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

    private void SeedDefaultEnemies()
    {
        EnsureEnemyList();

        AddDefaultEnemyIfMissing(new EnemyData
        {
            Id = "enemy_slime",
            Name = "スライム",
            Type = EnemyType.Melee,
            Hp = 30,
            Atk = 10,
            MoveSpeed = 2.5f,
            DropRate = 0.8f,
            ExpValue = 5,
            SpriteId = string.Empty
        });

        AddDefaultEnemyIfMissing(new EnemyData
        {
            Id = "enemy_archer",
            Name = "アーチャー",
            Type = EnemyType.Ranged,
            Hp = 20,
            Atk = 15,
            MoveSpeed = 1.5f,
            DropRate = 0.6f,
            ExpValue = 8,
            SpriteId = string.Empty
        });

        AddDefaultEnemyIfMissing(new EnemyData
        {
            Id = "enemy_fast",
            Name = "ダッシュスライム",
            Type = EnemyType.Melee,
            Hp = 15,
            Atk = 8,
            MoveSpeed = 5f,
            DropRate = 0.5f,
            ExpValue = 6,
            SpriteId = string.Empty
        });

        AddDefaultEnemyIfMissing(new EnemyData
        {
            Id = "enemy_tank",
            Name = "アーマースライム",
            Type = EnemyType.Melee,
            Hp = 120,
            Atk = 20,
            MoveSpeed = 1f,
            DropRate = 0.4f,
            ExpValue = 15,
            SpriteId = string.Empty
        });
    }

    private void AddDefaultEnemyIfMissing(EnemyData defaultEnemy)
    {
        if (defaultEnemy == null || string.IsNullOrWhiteSpace(defaultEnemy.Id))
            return;

        foreach (var enemy in _enemyList.Enemies)
        {
            if (enemy?.Id == defaultEnemy.Id)
                return;
        }

        _enemyList.Enemies.Add(defaultEnemy);
    }
}
