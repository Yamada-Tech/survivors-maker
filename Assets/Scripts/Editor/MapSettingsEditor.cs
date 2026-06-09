using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MapSettingsEditor : MonoBehaviour
{
    private const string MapSettingsFileName = "map_settings.json";
    private const int MinMapSize = 8;
    private const int MaxMapSize = 128;

    private MapSettingsData _settingsData = new();
    private IntegerField _widthField;
    private IntegerField _heightField;
    private FloatField   _wallRatioField;
    private TextField    _wallColorField;
    private TextField    _floorColorField;
    private MapGenerator _mapGenerator;
    private bool _isUiBuilt;
    private bool _isBinding;

    private void OnEnable()
    {
        BuildUi();
        Load();
        RefreshFields();
        _mapGenerator ??= FindAnyObjectByType<MapGenerator>();
        EventBus.Subscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Subscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Unsubscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[MapSettingsEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.paddingLeft   = 12f;
        root.style.paddingRight  = 12f;
        root.style.paddingTop    = 12f;
        root.style.paddingBottom = 12f;
        root.style.flexDirection = FlexDirection.Column;

        var title = new Label("Map Settings");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8f;
        root.Add(title);

        _widthField      = new IntegerField("Width");
        _heightField     = new IntegerField("Height");
        _wallRatioField  = new FloatField("Wall Ratio");
        _wallColorField  = new TextField("Wall Color (hex)");
        _floorColorField = new TextField("Floor Color (hex)");

        root.Add(_widthField);
        root.Add(_heightField);
        root.Add(_wallRatioField);
        root.Add(_wallColorField);
        root.Add(_floorColorField);

        var applyButton = new Button(ApplyToMapGenerator) { text = "適用" };
        applyButton.style.marginTop = 8f;
        root.Add(applyButton);

        RegisterCallbacks();
        _isUiBuilt = true;
    }

    private void RegisterCallbacks()
    {
        _widthField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.Width = Mathf.Clamp(evt.newValue, MinMapSize, MaxMapSize);
            if (_settingsData.Width != evt.newValue)
                _widthField.SetValueWithoutNotify(_settingsData.Width);
        });

        _heightField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.Height = Mathf.Clamp(evt.newValue, MinMapSize, MaxMapSize);
            if (_settingsData.Height != evt.newValue)
                _heightField.SetValueWithoutNotify(_settingsData.Height);
        });

        _wallRatioField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.WallRatio = Mathf.Clamp01(evt.newValue);
            if (!Mathf.Approximately(_settingsData.WallRatio, evt.newValue))
                _wallRatioField.SetValueWithoutNotify(_settingsData.WallRatio);
        });

        _wallColorField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            if (ColorUtility.TryParseHtmlString("#" + evt.newValue.TrimStart('#'), out var c))
                _settingsData.WallColor = c;
        });

        _floorColorField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            if (ColorUtility.TryParseHtmlString("#" + evt.newValue.TrimStart('#'), out var c))
                _settingsData.FloorColor = c;
        });
    }

    private void ApplyToMapGenerator()
    {
        _mapGenerator ??= FindAnyObjectByType<MapGenerator>();
        if (_mapGenerator == null)
        {
            Debug.LogWarning("[MapSettingsEditor] MapGenerator not found.");
            return;
        }

        _mapGenerator.MapWidth   = _settingsData.Width;
        _mapGenerator.MapHeight  = _settingsData.Height;
        _mapGenerator.WallRatio  = _settingsData.WallRatio;
        _mapGenerator.WallColor  = _settingsData.WallColor;
        _mapGenerator.FloorColor = _settingsData.FloorColor;
        _mapGenerator.Generate();
    }

    private void Save()
    {
        if (DataManager.Instance == null) return;
        DataManager.Instance.Save(_settingsData, MapSettingsFileName);
    }

    private void Load()
    {
        _settingsData = DataManager.Instance != null
            ? DataManager.Instance.Load<MapSettingsData>(MapSettingsFileName)
            : new MapSettingsData();

        _settingsData ??= new MapSettingsData();
        _settingsData.Width     = Mathf.Clamp(_settingsData.Width, MinMapSize, MaxMapSize);
        _settingsData.Height    = Mathf.Clamp(_settingsData.Height, MinMapSize, MaxMapSize);
        _settingsData.WallRatio = Mathf.Clamp01(_settingsData.WallRatio);
    }

    private void RefreshFields()
    {
        _isBinding = true;
        _widthField?.SetValueWithoutNotify(_settingsData.Width);
        _heightField?.SetValueWithoutNotify(_settingsData.Height);
        _wallRatioField?.SetValueWithoutNotify(_settingsData.WallRatio);
        _wallColorField?.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(_settingsData.WallColor));
        _floorColorField?.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(_settingsData.FloorColor));
        _isBinding = false;
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _) => Save();
    private void OnLoadAllRequested(LoadAllRequestedEvent _) { Load(); RefreshFields(); }
}
