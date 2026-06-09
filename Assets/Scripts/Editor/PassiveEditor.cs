using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PassiveEditor : MonoBehaviour
{
    private const string PassiveFileName = "passives.json";

    private PassiveListData _passiveList;
    private ListView _listView;
    private VisualElement _detailPanel;

    private TextField _nameField;
    private TextField _descriptionField;
    private EnumField _typeField;
    private FloatField _valueField;

    private bool _isUiBuilt;
    private bool _isBinding;
    private int _selectedIndex = -1;

    public void Initialize(PassiveListData data)
    {
        _passiveList = data;
        EnsurePassiveList();
        BuildUI();
        RefreshList();
    }

    private void OnEnable()
    {
        if (_passiveList == null || _passiveList.Passives == null)
        {
            _passiveList = DataManager.Instance != null
                ? DataManager.Instance.Load<PassiveListData>(PassiveFileName)
                : new PassiveListData();
        }

        EnsurePassiveList();
        SeedDefaultPassives();
        BuildUI();
        RefreshList();
        EventBus.Subscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Subscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Unsubscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void BuildUI()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[PassiveEditor] UIDocument root not found.");
            return;
        }

        QueryUiElements(root);

        if (HasMissingUiElements())
        {
            BuildFallbackUI(root);
            QueryUiElements(root);
        }

        if (_listView != null)
        {
            _listView.makeItem = () => new Label();
            _listView.bindItem = (e, i) =>
            {
                var passive = _passiveList.Passives[i];
                (e as Label).text = passive == null || string.IsNullOrWhiteSpace(passive.Name)
                    ? $"Passive {i + 1}"
                    : passive.Name;
            };
            _listView.itemsSource = _passiveList.Passives;
            _listView.selectionType = SelectionType.Single;
            _listView.selectionChanged += _ => ShowDetail(_listView.selectedIndex);
        }

        RegisterCallbacks();

        var addBtn = root.Q<Button>("AddBtn");
        if (addBtn != null) addBtn.clicked += AddPassive;
        var deleteBtn = root.Q<Button>("DeleteBtn");
        if (deleteBtn != null) deleteBtn.clicked += DeletePassive;
        var saveBtn = root.Q<Button>("SaveBtn");
        if (saveBtn != null) saveBtn.clicked += Save;

        _isUiBuilt = true;
    }

    private void QueryUiElements(VisualElement root)
    {
        _listView = root.Q<ListView>("PassiveList");
        _detailPanel = root.Q("DetailPanel");
        _nameField = root.Q<TextField>("NameField");
        _descriptionField = root.Q<TextField>("DescriptionField");
        _typeField = root.Q<EnumField>("TypeField");
        _valueField = root.Q<FloatField>("ValueField");
    }

    private bool HasMissingUiElements()
    {
        return _listView == null || _detailPanel == null || _nameField == null ||
               _descriptionField == null || _typeField == null || _valueField == null;
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
            name = "PassiveList",
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
        detailPanel.Add(new TextField("Description")
        {
            name = "DescriptionField",
            multiline = true,
            style = { minHeight = 60f }
        });
        detailPanel.Add(new EnumField("Type", PassiveType.MaxHpUp) { name = "TypeField" });
        detailPanel.Add(new FloatField("Value") { name = "ValueField" });

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
                if (_isBinding) return;

                var passive = GetSelectedPassive();
                if (passive == null) return;

                passive.Name = evt.newValue;
                RefreshListLabelsOnly();
            });
        }

        if (_descriptionField != null)
        {
            _descriptionField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;

                var passive = GetSelectedPassive();
                if (passive == null) return;

                passive.Description = evt.newValue;
            });
        }

        if (_typeField != null)
        {
            _typeField.Init(PassiveType.MaxHpUp);
            _typeField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;

                var passive = GetSelectedPassive();
                if (passive == null) return;

                if (evt.newValue is PassiveType newType)
                    passive.Type = newType;
            });
        }

        if (_valueField != null)
        {
            _valueField.RegisterValueChangedCallback(evt =>
            {
                if (_isBinding) return;

                var passive = GetSelectedPassive();
                if (passive == null) return;

                passive.Value = evt.newValue;
            });
        }
    }

    private void AddPassive()
    {
        _passiveList.Passives.Add(new PassiveData
        {
            Id = $"passive_{Guid.NewGuid():N}",
            Name = "新しいパッシブ"
        });

        _selectedIndex = _passiveList.Passives.Count - 1;
        RefreshList();
    }

    private void DeletePassive()
    {
        var idx = _listView != null ? _listView.selectedIndex : _selectedIndex;
        if (idx < 0 || idx >= _passiveList.Passives.Count)
            return;

        _passiveList.Passives.RemoveAt(idx);
        _selectedIndex = _passiveList.Passives.Count == 0 ? -1 : Mathf.Clamp(idx, 0, _passiveList.Passives.Count - 1);
        RefreshList();
    }

    private void Save()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[PassiveEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_passiveList, PassiveFileName);
    }

    private void Load()
    {
        _passiveList = DataManager.Instance != null
            ? DataManager.Instance.Load<PassiveListData>(PassiveFileName)
            : new PassiveListData();
        EnsurePassiveList();
    }

    private void ShowDetail(int index)
    {
        _selectedIndex = index;
        var data = GetSelectedPassive();

        if (_detailPanel != null)
            _detailPanel.SetEnabled(data != null);

        _isBinding = true;
        if (_nameField != null) _nameField.SetValueWithoutNotify(data?.Name ?? string.Empty);
        if (_descriptionField != null) _descriptionField.SetValueWithoutNotify(data?.Description ?? string.Empty);
        if (_typeField != null) _typeField.SetValueWithoutNotify(data?.Type ?? PassiveType.MaxHpUp);
        if (_valueField != null) _valueField.SetValueWithoutNotify(data?.Value ?? 0f);
        _isBinding = false;
    }

    private PassiveData GetSelectedPassive()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _passiveList.Passives.Count)
            return null;
        return _passiveList.Passives[_selectedIndex];
    }

    private void RefreshList()
    {
        if (_listView == null) return;

        _listView.itemsSource = _passiveList.Passives;
        _listView.Rebuild();

        if (_selectedIndex >= 0 && _selectedIndex < _passiveList.Passives.Count)
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

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void OnLoadAllRequested(LoadAllRequestedEvent _)
    {
        Load();
        RefreshList();
    }

    private void EnsurePassiveList()
    {
        _passiveList ??= new PassiveListData();
        _passiveList.Passives ??= new List<PassiveData>();
    }

    private void SeedDefaultPassives()
    {
        EnsurePassiveList();

        AddDefaultPassiveIfMissing(new PassiveData
        {
            Id = "passive_hp_up",
            Name = "HP強化",
            Description = "最大HPを20増加する",
            Type = PassiveType.MaxHpUp,
            Value = 20f
        });
        AddDefaultPassiveIfMissing(new PassiveData
        {
            Id = "passive_hp_regen",
            Name = "回復薬",
            Description = "HPを30回復する",
            Type = PassiveType.HpRecover,
            Value = 30f
        });
        AddDefaultPassiveIfMissing(new PassiveData
        {
            Id = "passive_speed_up",
            Name = "加速",
            Description = "移動速度が15%上がる",
            Type = PassiveType.MoveSpeedUp,
            Value = 0.15f
        });
        AddDefaultPassiveIfMissing(new PassiveData
        {
            Id = "passive_tough",
            Name = "タフネス",
            Description = "被弾間隔が短縮される",
            Type = PassiveType.DamageCooldownDown,
            Value = 0.15f
        });
        AddDefaultPassiveIfMissing(new PassiveData
        {
            Id = "passive_exp_up",
            Name = "経験値UP",
            Description = "EXP獲得量が20%増える",
            Type = PassiveType.ExpBonus,
            Value = 0.2f
        });
    }

    private void AddDefaultPassiveIfMissing(PassiveData defaultPassive)
    {
        if (defaultPassive == null || string.IsNullOrWhiteSpace(defaultPassive.Id))
            return;

        foreach (var passive in _passiveList.Passives)
        {
            if (passive?.Id == defaultPassive.Id)
                return;
        }

        _passiveList.Passives.Add(defaultPassive);
    }
}
