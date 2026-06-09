using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SpriteSettingsEditor : MonoBehaviour
{
    private const string SettingsFileName = "sprite_settings.json";
    private const string NoneLabel = "（なし）";

    private SpriteSettingsData _data;
    private List<AssetRecord> _textureAssets = new();
    private List<string> _dropdownChoices = new();
    private AssetManager _assetManager;

    private DropdownField _playerDropdown;
    private DropdownField _enemyMeleeDropdown;
    private DropdownField _enemyRangedDropdown;
    private VisualElement _playerPreview;
    private VisualElement _enemyMeleePreview;
    private VisualElement _enemyRangedPreview;

    private bool _isUiBuilt;

    private void OnEnable()
    {
        _data = DataManager.Instance != null && DataManager.Instance.Exists(SettingsFileName)
            ? DataManager.Instance.Load<SpriteSettingsData>(SettingsFileName) ?? new SpriteSettingsData()
            : new SpriteSettingsData();

        _assetManager = FindAnyObjectByType<AssetManager>();

        BuildUI();
        RefreshDropdowns();

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
            Debug.LogWarning("[SpriteSettingsEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingTop = 12f;
        root.style.paddingBottom = 12f;
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;

        var title = new Label("スプライト設定");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 16;
        title.style.marginBottom = 12f;
        root.Add(title);

        _playerDropdown = AddSpriteSection(root, "プレイヤー", "PlayerSpriteDropdown", "プレイヤースプライト", out _playerPreview, "PlayerPreview");
        _enemyMeleeDropdown = AddSpriteSection(root, "敵（近接）", "EnemyMeleeSpriteDropdown", "近接敵スプライト", out _enemyMeleePreview, "EnemyMeleePreview");
        _enemyRangedDropdown = AddSpriteSection(root, "敵（遠距離）", "EnemyRangedSpriteDropdown", "遠距離敵スプライト", out _enemyRangedPreview, "EnemyRangedPreview");

        var saveBtn = new Button(Save) { name = "SaveBtn", text = "保存" };
        saveBtn.style.marginTop = 12f;
        saveBtn.style.height = 40f;
        root.Add(saveBtn);

        RegisterDropdownCallbacks();
        _isUiBuilt = true;
    }

    private static DropdownField AddSpriteSection(VisualElement root, string sectionLabel, string dropdownName, string dropdownLabel, out VisualElement preview, string previewName)
    {
        var header = new Label(sectionLabel);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 8f;
        root.Add(header);

        var dropdown = new DropdownField(dropdownLabel, new List<string> { NoneLabel }, 0)
        {
            name = dropdownName
        };
        root.Add(dropdown);

        var prev = new VisualElement { name = previewName };
        prev.style.width = 64f;
        prev.style.height = 64f;
        prev.style.marginTop = 4f;
        prev.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        root.Add(prev);

        preview = prev;
        return dropdown;
    }

    private void RegisterDropdownCallbacks()
    {
        if (_playerDropdown != null)
            _playerDropdown.RegisterValueChangedCallback(evt => UpdatePreview(_playerDropdown, _playerPreview));
        if (_enemyMeleeDropdown != null)
            _enemyMeleeDropdown.RegisterValueChangedCallback(evt => UpdatePreview(_enemyMeleeDropdown, _enemyMeleePreview));
        if (_enemyRangedDropdown != null)
            _enemyRangedDropdown.RegisterValueChangedCallback(evt => UpdatePreview(_enemyRangedDropdown, _enemyRangedPreview));
    }

    private void RefreshDropdowns()
    {
        _textureAssets.Clear();
        _dropdownChoices.Clear();
        _dropdownChoices.Add(NoneLabel);

        if (_assetManager != null)
        {
            foreach (var record in _assetManager.GetAssets())
            {
                if (record.Kind == AssetKind.Texture)
                {
                    _textureAssets.Add(record);
                    _dropdownChoices.Add(record.OriginalFileName);
                }
            }
        }

        SetDropdownChoices(_playerDropdown, _data?.PlayerSpriteGuid ?? "");
        SetDropdownChoices(_enemyMeleeDropdown, _data?.EnemyMeleeSpriteGuid ?? "");
        SetDropdownChoices(_enemyRangedDropdown, _data?.EnemyRangedSpriteGuid ?? "");

        UpdatePreview(_playerDropdown, _playerPreview);
        UpdatePreview(_enemyMeleeDropdown, _enemyMeleePreview);
        UpdatePreview(_enemyRangedDropdown, _enemyRangedPreview);
    }

    private void SetDropdownChoices(DropdownField dropdown, string selectedGuid)
    {
        if (dropdown == null) return;
        dropdown.choices = new List<string>(_dropdownChoices);

        if (!string.IsNullOrWhiteSpace(selectedGuid))
        {
            var record = _textureAssets.Find(r => r.Guid == selectedGuid);
            if (record != null)
            {
                dropdown.SetValueWithoutNotify(record.OriginalFileName);
                return;
            }
        }

        dropdown.SetValueWithoutNotify(NoneLabel);
    }

    private void UpdatePreview(DropdownField dropdown, VisualElement preview)
    {
        if (dropdown == null || preview == null) return;

        var selected = dropdown.value;
        if (selected == NoneLabel)
        {
            preview.style.backgroundImage = StyleKeyword.None;
            return;
        }

        var record = _textureAssets.Find(r => r.OriginalFileName == selected);
        if (record == null) return;

        if (_assetManager == null) return;

        var tex = _assetManager.LoadTexture(record.Guid);
        if (tex != null)
            preview.style.backgroundImage = Background.FromTexture2D(tex);
    }

    private string GetSelectedGuid(DropdownField dropdown)
    {
        if (dropdown == null || dropdown.value == NoneLabel) return "";
        var record = _textureAssets.Find(r => r.OriginalFileName == dropdown.value);
        return record?.Guid ?? "";
    }

    private void Save()
    {
        _data ??= new SpriteSettingsData();
        _data.PlayerSpriteGuid = GetSelectedGuid(_playerDropdown);
        _data.EnemyMeleeSpriteGuid = GetSelectedGuid(_enemyMeleeDropdown);
        _data.EnemyRangedSpriteGuid = GetSelectedGuid(_enemyRangedDropdown);

        DataManager.Instance?.Save(_data, SettingsFileName);
        Debug.Log("[SpriteSettingsEditor] Saved sprite settings.");
    }

    private void Load()
    {
        if (DataManager.Instance == null || !DataManager.Instance.Exists(SettingsFileName))
            return;

        _data = DataManager.Instance.Load<SpriteSettingsData>(SettingsFileName) ?? new SpriteSettingsData();
        RefreshDropdowns();
        Debug.Log("[SpriteSettingsEditor] Loaded sprite settings.");
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _) => Save();
    private void OnLoadAllRequested(LoadAllRequestedEvent _) => Load();
}
