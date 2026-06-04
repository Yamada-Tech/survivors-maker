using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EditorRootPanel : MonoBehaviour
{
    private const float MenuWidth = 180f;
    private const float DividerWidth = 1f;
    private const float ContentLeftOffset = MenuWidth + DividerWidth;
    private static readonly Color RootBackgroundColor = new(0.12f, 0.12f, 0.15f);
    private static readonly Color MenuButtonColor = new(0.18f, 0.18f, 0.22f);
    private static readonly Color MenuButtonSelectedColor = new(0.2f, 0.5f, 1f);
    private static readonly Color DividerColor = new(0.3f, 0.3f, 0.35f);

    private readonly Dictionary<string, GameObject> _panelGameObjects = new();
    private readonly Dictionary<string, Button> _menuButtons = new();
    private bool _isUiBuilt;
    private string _selectedKey;

    private void OnEnable()
    {
        BuildUi();
        SelectPanel("map");
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

        RegisterPanel("map", "MapEditorPanel");
        RegisterPanel("mapsettings", "MapSettingsEditorPanel");
        RegisterPanel("enemy", "EnemyEditorPanel");
        RegisterPanel("weapon", "WeaponEditorPanel");
        RegisterPanel("wave", "WaveEditorPanel");
        RegisterPanel("asset", "AssetManagerPanel");
        RegisterPanel("dotart", "DotArtEditorPanel");
        RegisterPanel("spritesheet", "SpriteSheetEditorPanel");

        AddMenuButton(menuPane, "map", "🗺️ マップ");
        AddMenuButton(menuPane, "enemy", "👾 敵");
        AddMenuButton(menuPane, "weapon", "⚔️ 武器");
        AddMenuButton(menuPane, "wave", "🌊 Wave");
        AddMenuButton(menuPane, "asset", "🖼️ アセット");
        AddMenuButton(menuPane, "dotart", "🎨 ドット絵");
        AddMenuButton(menuPane, "spritesheet", "📋 スプライト");
        AddMenuButton(menuPane, "play", "▶️ プレイ", OnPlayClicked, false);

        _isUiBuilt = true;
    }

    private void OnPlayClicked()
    {
        EventBus.Publish(new AppStateChangedEvent { NewState = AppState.Play });
    }

    private void RegisterPanel(string key, string panelName)
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
            if (panelName == "MapSettingsEditorPanel")
                panelRoot.style.left = StyleKeyword.Auto;
            else
                panelRoot.style.left = ContentLeftOffset;
            panelRoot.style.right = 0f;
            panelRoot.style.top = 0f;
            panelRoot.style.bottom = 0f;
            panelRoot.style.backgroundColor = RootBackgroundColor;
            if (panelName == "MapSettingsEditorPanel")
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

    private void SelectPanel(string key)
    {
        _selectedKey = key;

        foreach (var pair in _panelGameObjects)
            pair.Value?.SetActive(pair.Key == key || (key == "map" && pair.Key == "mapsettings"));

        foreach (var pair in _menuButtons)
            pair.Value.style.backgroundColor = pair.Key == _selectedKey ? MenuButtonSelectedColor : MenuButtonColor;
    }
}
