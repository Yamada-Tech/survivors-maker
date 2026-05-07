using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class WeaponEditor : MonoBehaviour
{
    private const string WeaponFileName = "weapons.json";

    private WeaponListData _weaponList;
    private ListView _listView;
    private VisualElement _detailPanel;

    private TextField _nameField;
    private EnumField _typeField;
    private IntegerField _damageField;
    private FloatField _cooldownField;
    private FloatField _rangeField;
    private FloatField _projectileSpeedField;
    private TextField _spriteIdField;

    private bool _isUiBuilt;
    private bool _isBinding;
    private int _selectedIndex = -1;

    public void Initialize(WeaponListData data)
    {
        _weaponList = data;
        EnsureWeaponList();
        BuildUI();
        RefreshList();
    }

    private void OnEnable()
    {
        if (_weaponList == null || _weaponList.Weapons == null)
        {
            _weaponList = DataManager.Instance != null
                ? DataManager.Instance.Load<WeaponListData>(WeaponFileName)
                : new WeaponListData();
        }

        EnsureWeaponList();
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
            Debug.LogWarning("[WeaponEditor] UIDocument root not found.");
            return;
        }

        _listView = root.Q<ListView>("WeaponList");
        if (_listView != null)
        {
            _listView.makeItem = () => new Label();
            _listView.bindItem = (e, i) =>
            {
                var weapon = _weaponList.Weapons[i];
                (e as Label).text = weapon == null || string.IsNullOrWhiteSpace(weapon.Name)
                    ? $"Weapon {i + 1}"
                    : weapon.Name;
            };
            _listView.itemsSource = _weaponList.Weapons;
            _listView.selectionType = SelectionType.Single;
            _listView.selectionChanged += _ => ShowDetail(_listView.selectedIndex);
        }

        _detailPanel = root.Q("DetailPanel");
        _nameField = root.Q<TextField>("NameField");
        _typeField = root.Q<EnumField>("TypeField");
        _damageField = root.Q<IntegerField>("DamageField");
        _cooldownField = root.Q<FloatField>("CooldownField");
        _rangeField = root.Q<FloatField>("RangeField");
        _projectileSpeedField = root.Q<FloatField>("ProjectileSpeedField");
        _spriteIdField = root.Q<TextField>("SpriteIdField");

        RegisterCallbacks();

        var addBtn = root.Q<Button>("AddBtn");
        if (addBtn != null) addBtn.clicked += AddWeapon;
        var deleteBtn = root.Q<Button>("DeleteBtn");
        if (deleteBtn != null) deleteBtn.clicked += DeleteWeapon;
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
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.Name = evt.newValue;
                RefreshListLabelsOnly();
            });
        }

        if (_typeField != null)
        {
            _typeField.Init(WeaponType.Melee);
            _typeField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                if (evt.newValue is WeaponType newType)
                    weapon.Type = newType;
            });
        }

        if (_damageField != null)
        {
            _damageField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.Damage = Mathf.Max(0, evt.newValue);
                if (weapon.Damage != evt.newValue)
                    _damageField.SetValueWithoutNotify(weapon.Damage);
            });
        }

        if (_cooldownField != null)
        {
            _cooldownField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.Cooldown = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(weapon.Cooldown, evt.newValue))
                    _cooldownField.SetValueWithoutNotify(weapon.Cooldown);
            });
        }

        if (_rangeField != null)
        {
            _rangeField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.Range = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(weapon.Range, evt.newValue))
                    _rangeField.SetValueWithoutNotify(weapon.Range);
            });
        }

        if (_projectileSpeedField != null)
        {
            _projectileSpeedField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.ProjectileSpeed = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(weapon.ProjectileSpeed, evt.newValue))
                    _projectileSpeedField.SetValueWithoutNotify(weapon.ProjectileSpeed);
            });
        }

        if (_spriteIdField != null)
        {
            _spriteIdField.RegisterValueChangedCallback(evt =>
            {
                var weapon = GetSelectedWeapon();
                if (_isBinding || weapon == null) return;

                weapon.SpriteId = evt.newValue;
            });
        }
    }

    private void AddWeapon()
    {
        _weaponList.Weapons.Add(new WeaponData
        {
            Id = $"weapon_{Guid.NewGuid():N}",
            Name = "新しい武器",
            Type = WeaponType.Melee,
            SpriteId = string.Empty
        });

        _selectedIndex = _weaponList.Weapons.Count - 1;
        RefreshList();
    }

    private void DeleteWeapon()
    {
        var idx = _listView != null ? _listView.selectedIndex : _selectedIndex;
        if (idx < 0 || idx >= _weaponList.Weapons.Count)
            return;

        _weaponList.Weapons.RemoveAt(idx);
        _selectedIndex = _weaponList.Weapons.Count == 0 ? -1 : Mathf.Clamp(idx, 0, _weaponList.Weapons.Count - 1);
        RefreshList();
    }

    private void ShowDetail(int index)
    {
        _selectedIndex = index;
        var data = GetSelectedWeapon();

        if (_detailPanel != null)
            _detailPanel.SetEnabled(data != null);

        _isBinding = true;
        if (_nameField != null) _nameField.SetValueWithoutNotify(data?.Name ?? string.Empty);
        if (_typeField != null) _typeField.SetValueWithoutNotify(data?.Type ?? WeaponType.Melee);
        if (_damageField != null) _damageField.SetValueWithoutNotify(data?.Damage ?? 0);
        if (_cooldownField != null) _cooldownField.SetValueWithoutNotify(data?.Cooldown ?? 0f);
        if (_rangeField != null) _rangeField.SetValueWithoutNotify(data?.Range ?? 0f);
        if (_projectileSpeedField != null) _projectileSpeedField.SetValueWithoutNotify(data?.ProjectileSpeed ?? 0f);
        if (_spriteIdField != null) _spriteIdField.SetValueWithoutNotify(data?.SpriteId ?? string.Empty);
        _isBinding = false;
    }

    private WeaponData GetSelectedWeapon()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _weaponList.Weapons.Count)
            return null;
        return _weaponList.Weapons[_selectedIndex];
    }

    private void RefreshList()
    {
        if (_listView == null) return;

        _listView.itemsSource = _weaponList.Weapons;
        _listView.Rebuild();

        if (_selectedIndex >= 0 && _selectedIndex < _weaponList.Weapons.Count)
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
            Debug.LogWarning("[WeaponEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_weaponList, WeaponFileName);
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void EnsureWeaponList()
    {
        _weaponList ??= new WeaponListData();
        _weaponList.Weapons ??= new List<WeaponData>();
    }
}
