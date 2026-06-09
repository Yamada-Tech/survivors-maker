using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EditorRootPanel : MonoBehaviour
{
    private const float MenuWidth = 180f;
    private const float DividerWidth = 1f;
    private const float ContentLeftOffset = MenuWidth + DividerWidth;
    private const string MapPanelKey = "map";
    private const string MapSettingsPanelKey = "mapsettings";
    private const string GameSettingsPanelKey = "gamesettings";
    private const string MapSettingsPanelName = "MapSettingsEditorPanel";
    private const string GameSettingsPanelName = "GameSettingsEditorPanel";
    private static readonly Color RootBackgroundColor = new(0.12f, 0.12f, 0.15f);
    private static readonly Color MenuButtonColor = new(0.18f, 0.18f, 0.22f);
    private static readonly Color MenuButtonSelectedColor = new(0.2f, 0.5f, 1f);
    private static readonly Color DividerColor = new(0.3f, 0.3f, 0.35f);

    private readonly Dictionary<string, GameObject> _panelGameObjects = new();
    private readonly Dictionary<string, Button> _menuButtons = new();
    private readonly Dictionary<string, List<string>> _linkedPanels = new()
    {
        { MapPanelKey, new List<string> { MapSettingsPanelKey } }
    };
    private bool _isUiBuilt;
    private string _selectedKey;

    private void OnEnable()
    {
        BuildUi();
        SelectPanel(MapPanelKey);
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[EditorRootPanel] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1f;
        root.style.backgroundColor = RootBackgroundColor;

        var menuPane = new VisualElement();
        menuPane.style.width = MenuWidth;
        menuPane.style.flexShrink = 0f;
        menuPane.style.flexDirection = FlexDirection.Column;
        root.Add(menuPane);

        var divider = new VisualElement();
        divider.style.width = DividerWidth;
        divider.style.backgroundColor = DividerColor;
        root.Add(divider);

        var contentPane = new VisualElement();
        contentPane.style.flexGrow = 1f;
        contentPane.style.backgroundColor = RootBackgroundColor;
        root.Add(contentPane);

        RegisterPanel(MapPanelKey, "MapEditorPanel");
        RegisterPanel(MapSettingsPanelKey, MapSettingsPanelName, isSidePanel: true);
        RegisterPanel(GameSettingsPanelKey, GameSettingsPanelName, isSidePanel: false);
        RegisterPanel("enemy", "EnemyEditorPanel");
        RegisterPanel("weapon", "WeaponEditorPanel");
        RegisterPanel("passive", "PassiveEditorPanel");
        RegisterPanel("preset", "PresetEditorPanel");
        RegisterPanel("wave", "WaveEditorPanel");
        RegisterPanel("asset", "AssetManagerPanel");
        RegisterPanel("dotart", "DotArtEditorPanel");
        RegisterPanel("spritesheet", "SpriteSheetEditorPanel");

        AddMenuButton(menuPane, MapPanelKey, "🗺️ マップ");
        AddMenuButton(menuPane, GameSettingsPanelKey, "⚙️ 基本設定");
        AddMenuButton(menuPane, "enemy", "👾 敵");
        AddMenuButton(menuPane, "weapon", "⚔️ 武器");
        AddMenuButton(menuPane, "passive", "🌀 パッシブ");
        AddMenuButton(menuPane, "preset", "🗂️ プリセット");
        AddMenuButton(menuPane, "wave", "🌊 Wave");
        AddMenuButton(menuPane, "asset", "🖼️ アセット");
        AddMenuButton(menuPane, "dotart", "🎨 ドット絵");
        AddMenuButton(menuPane, "spritesheet", "📋 スプライト");

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        menuPane.Add(spacer);

        menuPane.Add(CreateHorizontalDivider());
        AddActionButton(menuPane, "💾 全保存", OnSaveAllClicked, 44f);
        AddActionButton(menuPane, "📂 全読込", OnLoadAllClicked, 44f);
        menuPane.Add(CreateHorizontalDivider());
        AddMenuButton(menuPane, "play", "▶️ プレイ", OnPlayClicked, false);

        _isUiBuilt = true;
    }

    private void OnSaveAllClicked()
    {
        EventBus.Publish(new SaveAllRequestedEvent());
        Debug.Log("[EditorRootPanel] Save All requested.");
    }

    private void OnLoadAllClicked()
    {
        EventBus.Publish(new LoadAllRequestedEvent());
        Debug.Log("[EditorRootPanel] Load All requested.");
    }

    private void OnPlayClicked()
    {
        EventBus.Publish(new AppStateChangedEvent { NewState = AppState.Play });
    }

    private void RegisterPanel(string key, string panelName, bool isSidePanel = false)
    {
        var panelTransform = transform.Find(panelName);
        if (panelTransform == null)
        {
            Debug.LogWarning($"[EditorRootPanel] Panel not found: {panelName}");
            return;
        }

        _panelGameObjects[key] = panelTransform.gameObject;
        var panelRoot = panelTransform.GetComponent<UIDocument>()?.rootVisualElement;
        if (panelRoot != null)
        {
            panelRoot.style.position = Position.Absolute;
            if (!isSidePanel)
                panelRoot.style.left = ContentLeftOffset;
            panelRoot.style.right = 0f;
            panelRoot.style.top = 0f;
            panelRoot.style.bottom = 0f;
            panelRoot.style.backgroundColor = RootBackgroundColor;
            if (isSidePanel)
            {
                panelRoot.style.width = 320f;
            }
        }
    }

    private void AddMenuButton(VisualElement parent, string key, string label, System.Action clickAction = null, bool shouldSelectPanel = true)
    {
        var button = new Button(() =>
        {
            clickAction?.Invoke();
            if (shouldSelectPanel)
                SelectPanel(key);
        })
        {
            text = label
        };

        button.style.width = Length.Percent(100);
        button.style.height = 48f;
        button.style.backgroundColor = MenuButtonColor;
        button.style.color = Color.white;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        button.style.paddingLeft = 12f;
        parent.Add(button);

        _menuButtons[key] = button;
    }

    private void AddActionButton(VisualElement parent, string label, System.Action clickAction, float height)
    {
        var button = new Button(() => clickAction?.Invoke())
        {
            text = label
        };

        button.style.width = Length.Percent(100);
        button.style.height = height;
        button.style.backgroundColor = MenuButtonColor;
        button.style.color = Color.white;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        button.style.paddingLeft = 12f;
        parent.Add(button);
    }

    private static VisualElement CreateHorizontalDivider()
    {
        var divider = new VisualElement();
        divider.style.width = Length.Percent(100);
        divider.style.height = 1f;
        divider.style.backgroundColor = DividerColor;
        return divider;
    }

    private void SelectPanel(string key)
    {
        _selectedKey = key;

        foreach (var pair in _panelGameObjects)
            pair.Value?.SetActive(ShouldShowPanel(key, pair.Key));

        foreach (var pair in _menuButtons)
            pair.Value.style.backgroundColor = pair.Key == _selectedKey ? MenuButtonSelectedColor : MenuButtonColor;
    }

    private bool ShouldShowPanel(string selectedKey, string panelKey)
    {
        if (panelKey == selectedKey)
            return true;

        return _linkedPanels.TryGetValue(selectedKey, out var linkedPanelKeys) && linkedPanelKeys.Contains(panelKey);
    }
}
