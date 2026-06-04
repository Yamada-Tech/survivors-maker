using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SpriteSheetEditor : MonoBehaviour
{
    private const string SpriteSheetFileName = "spritesheet.json";
    private const float LeftPaneWidth = 240f;
    private const float ThumbnailSize = 72f;
    private static readonly Color BorderColor = new(0.25f, 0.25f, 0.25f, 1f);

    private readonly List<string> _textureChoiceLabels = new();
    private readonly List<string> _textureChoiceGuids = new();
    private readonly List<Texture2D> _previewTextures = new();

    private SpriteSheetData _spriteSheetData;
    private ListView _animationListView;
    private DropdownField _textureDropdown;
    private IntegerField _frameWidthField;
    private IntegerField _frameHeightField;
    private IntegerField _columnsField;
    private IntegerField _rowsField;
    private VisualElement _detailPanel;
    private TextField _nameField;
    private IntegerField _startFrameField;
    private IntegerField _frameCountField;
    private IntegerField _fpsField;
    private VisualElement _previewContent;
    private Label _previewStatusLabel;
    private Label _statusLabel;

    private bool _isUiBuilt;
    private bool _isBinding;
    private int _selectedAnimationIndex = -1;

    private void OnEnable()
    {
        EnsureAssetManager();
        LoadData();
        BuildUi();
        RefreshTextureChoices();
        RefreshFrameSettings();
        RefreshAnimationList();

        EventBus.Subscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        if (AssetManager.Instance != null)
            AssetManager.Instance.OnAssetChanged += HandleAssetChanged;
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        if (AssetManager.Instance != null)
            AssetManager.Instance.OnAssetChanged -= HandleAssetChanged;

        ClearPreviewTextures();
    }

    private void EnsureAssetManager()
    {
        if (AssetManager.Instance != null) return;

        var manager = FindAnyObjectByType<AssetManager>();
        if (manager == null)
        {
            var go = new GameObject("AssetManager");
            manager = go.AddComponent<AssetManager>();
        }
    }

    private void LoadData()
    {
        if (_spriteSheetData == null || _spriteSheetData.Animations == null)
        {
            _spriteSheetData = DataManager.Instance != null && DataManager.Instance.Exists(SpriteSheetFileName)
                ? DataManager.Instance.Load<SpriteSheetData>(SpriteSheetFileName)
                : new SpriteSheetData();
        }

        EnsureDataValidity();
    }

    private void Save()
    {
        if (DataManager.Instance == null)
        {
            SetStatus("DataManager not found.");
            return;
        }

        DataManager.Instance.Save(_spriteSheetData, SpriteSheetFileName);
        SetStatus("保存しました。");
    }

    private void Reload()
    {
        _spriteSheetData = DataManager.Instance != null && DataManager.Instance.Exists(SpriteSheetFileName)
            ? DataManager.Instance.Load<SpriteSheetData>(SpriteSheetFileName)
            : new SpriteSheetData();

        EnsureDataValidity();
        RefreshTextureChoices();
        RefreshFrameSettings();
        RefreshAnimationList();
        SetStatus("再読み込みしました。");
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[SpriteSheetEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.flexDirection = FlexDirection.Column;
        root.style.flexGrow = 1f;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.flexWrap = Wrap.Wrap;
        header.style.marginBottom = 8;
        root.Add(header);

        _textureDropdown = new DropdownField("Texture");
        _textureDropdown.style.minWidth = 320;
        _textureDropdown.style.marginRight = 8;
        _textureDropdown.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding || _spriteSheetData == null) return;

            var selectedIndex = _textureChoiceLabels.IndexOf(evt.newValue);
            _spriteSheetData.TextureGuid = selectedIndex >= 0 && selectedIndex < _textureChoiceGuids.Count
                ? _textureChoiceGuids[selectedIndex]
                : string.Empty;
            RefreshPreview();
        });
        header.Add(_textureDropdown);

        _frameWidthField = CreateHeaderIntegerField(header, "Frame W", value =>
        {
            _spriteSheetData.FrameWidth = Mathf.Max(1, value);
            if (_frameWidthField.value != _spriteSheetData.FrameWidth)
                _frameWidthField.SetValueWithoutNotify(_spriteSheetData.FrameWidth);
            RefreshPreview();
        });
        _frameHeightField = CreateHeaderIntegerField(header, "Frame H", value =>
        {
            _spriteSheetData.FrameHeight = Mathf.Max(1, value);
            if (_frameHeightField.value != _spriteSheetData.FrameHeight)
                _frameHeightField.SetValueWithoutNotify(_spriteSheetData.FrameHeight);
            RefreshPreview();
        });
        _columnsField = CreateHeaderIntegerField(header, "Columns", value =>
        {
            _spriteSheetData.Columns = Mathf.Max(1, value);
            if (_columnsField.value != _spriteSheetData.Columns)
                _columnsField.SetValueWithoutNotify(_spriteSheetData.Columns);
            RefreshPreview();
        });
        _rowsField = CreateHeaderIntegerField(header, "Rows", value =>
        {
            _spriteSheetData.Rows = Mathf.Max(1, value);
            if (_rowsField.value != _spriteSheetData.Rows)
                _rowsField.SetValueWithoutNotify(_spriteSheetData.Rows);
            RefreshPreview();
        });

        AddActionButton(header, "Save", Save);
        AddActionButton(header, "Reload", Reload);

        var content = new VisualElement();
        content.style.flexDirection = FlexDirection.Row;
        content.style.flexGrow = 1f;
        root.Add(content);

        var leftPane = new VisualElement();
        leftPane.style.width = LeftPaneWidth;
        leftPane.style.flexShrink = 0f;
        leftPane.style.marginRight = 8;
        leftPane.style.flexDirection = FlexDirection.Column;
        content.Add(leftPane);

        var leftTitle = new Label("アニメーション行");
        leftTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        leftTitle.style.marginBottom = 4;
        leftPane.Add(leftTitle);

        _animationListView = new ListView();
        _animationListView.style.flexGrow = 1f;
        _animationListView.selectionType = SelectionType.Single;
        _animationListView.makeItem = () => new Label();
        _animationListView.bindItem = (element, index) =>
        {
            var animation = _spriteSheetData.Animations[index];
            var label = element as Label;
            if (label == null) return;

            var name = string.IsNullOrWhiteSpace(animation?.Name) ? $"Row {index + 1}" : animation.Name;
            var frameCount = Mathf.Max(animation?.FrameCount ?? 1, 1);
            var startFrame = Mathf.Max(animation?.StartFrame ?? 0, 0);
            label.text = $"{name} [{startFrame}-{startFrame + frameCount - 1}]";
        };
        _animationListView.selectionChanged += _ => ShowAnimationDetail(_animationListView.selectedIndex);
        leftPane.Add(_animationListView);

        var leftButtons = new VisualElement();
        leftButtons.style.flexDirection = FlexDirection.Row;
        leftButtons.style.marginTop = 6;
        leftPane.Add(leftButtons);

        AddActionButton(leftButtons, "追加", AddAnimation);
        AddActionButton(leftButtons, "削除", DeleteAnimation);

        var rightPane = new VisualElement();
        rightPane.style.flexGrow = 1f;
        rightPane.style.flexDirection = FlexDirection.Column;
        content.Add(rightPane);

        _detailPanel = new VisualElement();
        _detailPanel.style.marginBottom = 8;
        _detailPanel.style.paddingLeft = 8;
        _detailPanel.style.paddingRight = 8;
        _detailPanel.style.paddingTop = 8;
        _detailPanel.style.paddingBottom = 8;
        _detailPanel.style.borderBottomWidth = 1;
        _detailPanel.style.borderTopWidth = 1;
        _detailPanel.style.borderLeftWidth = 1;
        _detailPanel.style.borderRightWidth = 1;
        _detailPanel.style.borderBottomColor = BorderColor;
        _detailPanel.style.borderTopColor = BorderColor;
        _detailPanel.style.borderLeftColor = BorderColor;
        _detailPanel.style.borderRightColor = BorderColor;
        rightPane.Add(_detailPanel);

        var detailTitle = new Label("選択中行の設定");
        detailTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        detailTitle.style.marginBottom = 6;
        _detailPanel.Add(detailTitle);

        _nameField = new TextField("行名");
        _nameField.RegisterValueChangedCallback(evt =>
        {
            var animation = GetSelectedAnimation();
            if (_isBinding || animation == null) return;

            animation.Name = evt.newValue;
            RefreshAnimationListLabelsOnly();
        });
        _detailPanel.Add(_nameField);

        _startFrameField = new IntegerField("開始フレーム");
        _startFrameField.RegisterValueChangedCallback(evt =>
        {
            var animation = GetSelectedAnimation();
            if (_isBinding || animation == null) return;

            animation.StartFrame = Mathf.Max(0, evt.newValue);
            if (_startFrameField.value != animation.StartFrame)
                _startFrameField.SetValueWithoutNotify(animation.StartFrame);
            RefreshAnimationListLabelsOnly();
            RefreshPreview();
        });
        _detailPanel.Add(_startFrameField);

        _frameCountField = new IntegerField("フレーム数");
        _frameCountField.RegisterValueChangedCallback(evt =>
        {
            var animation = GetSelectedAnimation();
            if (_isBinding || animation == null) return;

            animation.FrameCount = Mathf.Max(1, evt.newValue);
            if (_frameCountField.value != animation.FrameCount)
                _frameCountField.SetValueWithoutNotify(animation.FrameCount);
            RefreshAnimationListLabelsOnly();
            RefreshPreview();
        });
        _detailPanel.Add(_frameCountField);

        _fpsField = new IntegerField("FPS");
        _fpsField.RegisterValueChangedCallback(evt =>
        {
            var animation = GetSelectedAnimation();
            if (_isBinding || animation == null) return;

            animation.Fps = Mathf.Max(1, evt.newValue);
            if (_fpsField.value != animation.Fps)
                _fpsField.SetValueWithoutNotify(animation.Fps);
            RefreshPreview();
        });
        _detailPanel.Add(_fpsField);

        var previewTitle = new Label("プレビュー");
        previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        previewTitle.style.marginBottom = 4;
        rightPane.Add(previewTitle);

        _previewStatusLabel = new Label();
        _previewStatusLabel.style.marginBottom = 4;
        rightPane.Add(_previewStatusLabel);

        var previewScroll = new ScrollView();
        previewScroll.style.flexGrow = 1f;
        rightPane.Add(previewScroll);

        _previewContent = new VisualElement();
        _previewContent.style.flexDirection = FlexDirection.Row;
        _previewContent.style.flexWrap = Wrap.Wrap;
        previewScroll.Add(_previewContent);

        _statusLabel = new Label();
        _statusLabel.style.marginTop = 6;
        rightPane.Add(_statusLabel);

        _isUiBuilt = true;
    }

    private IntegerField CreateHeaderIntegerField(VisualElement parent, string label, System.Action<int> onChanged)
    {
        var field = new IntegerField(label);
        field.style.width = 110;
        field.style.marginRight = 8;
        field.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding || _spriteSheetData == null) return;
            onChanged?.Invoke(evt.newValue);
        });
        parent.Add(field);
        return field;
    }

    private void AddAnimation()
    {
        _spriteSheetData.Animations.Add(new AnimationRowData
        {
            Name = $"新規アニメーション {_spriteSheetData.Animations.Count + 1}",
            StartFrame = 0,
            FrameCount = 1,
            Fps = AnimationRowData.DefaultFps
        });

        _selectedAnimationIndex = _spriteSheetData.Animations.Count - 1;
        RefreshAnimationList();
    }

    private void DeleteAnimation()
    {
        if (_selectedAnimationIndex < 0 || _selectedAnimationIndex >= _spriteSheetData.Animations.Count)
            return;

        _spriteSheetData.Animations.RemoveAt(_selectedAnimationIndex);
        _selectedAnimationIndex = _spriteSheetData.Animations.Count == 0
            ? -1
            : Mathf.Clamp(_selectedAnimationIndex, 0, _spriteSheetData.Animations.Count - 1);
        RefreshAnimationList();
    }

    private void RefreshFrameSettings()
    {
        if (_spriteSheetData == null) return;

        _isBinding = true;
        _frameWidthField?.SetValueWithoutNotify(_spriteSheetData.FrameWidth);
        _frameHeightField?.SetValueWithoutNotify(_spriteSheetData.FrameHeight);
        _columnsField?.SetValueWithoutNotify(_spriteSheetData.Columns);
        _rowsField?.SetValueWithoutNotify(_spriteSheetData.Rows);
        _isBinding = false;
    }

    private void RefreshTextureChoices()
    {
        if (_textureDropdown == null || _spriteSheetData == null) return;

        _textureChoiceLabels.Clear();
        _textureChoiceGuids.Clear();
        _textureChoiceLabels.Add("未選択");
        _textureChoiceGuids.Add(string.Empty);

        if (AssetManager.Instance != null)
        {
            foreach (var asset in AssetManager.Instance.GetAssets())
            {
                if (asset == null || asset.Kind != AssetKind.Texture)
                    continue;

                var displayName = string.IsNullOrWhiteSpace(asset.OriginalFileName)
                    ? asset.Guid
                    : asset.OriginalFileName;
                _textureChoiceLabels.Add($"{displayName} ({asset.Guid})");
                _textureChoiceGuids.Add(asset.Guid);
            }
        }

        _textureDropdown.choices = new List<string>(_textureChoiceLabels);

        var selectedIndex = _textureChoiceGuids.FindIndex(guid => guid == (_spriteSheetData.TextureGuid ?? string.Empty));
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            _spriteSheetData.TextureGuid = string.Empty;
        }

        _isBinding = true;
        _textureDropdown.SetValueWithoutNotify(_textureChoiceLabels[selectedIndex]);
        _isBinding = false;

        RefreshPreview();
    }

    private void RefreshAnimationList()
    {
        if (_animationListView == null || _spriteSheetData == null) return;

        if (_selectedAnimationIndex < 0 && _spriteSheetData.Animations.Count > 0)
            _selectedAnimationIndex = 0;
        if (_selectedAnimationIndex >= _spriteSheetData.Animations.Count)
            _selectedAnimationIndex = _spriteSheetData.Animations.Count - 1;

        _animationListView.itemsSource = _spriteSheetData.Animations;
        _animationListView.Rebuild();

        if (_selectedAnimationIndex >= 0 && _selectedAnimationIndex < _spriteSheetData.Animations.Count)
            _animationListView.SetSelection(_selectedAnimationIndex);
        else
            ShowAnimationDetail(-1);
    }

    private void RefreshAnimationListLabelsOnly()
    {
        _animationListView?.Rebuild();
    }

    private void ShowAnimationDetail(int index)
    {
        _selectedAnimationIndex = index;
        var animation = GetSelectedAnimation();

        _detailPanel?.SetEnabled(animation != null);

        _isBinding = true;
        _nameField?.SetValueWithoutNotify(animation?.Name ?? string.Empty);
        _startFrameField?.SetValueWithoutNotify(animation?.StartFrame ?? 0);
        _frameCountField?.SetValueWithoutNotify(animation?.FrameCount ?? 1);
        _fpsField?.SetValueWithoutNotify(animation?.Fps ?? AnimationRowData.DefaultFps);
        _isBinding = false;

        RefreshPreview();
    }

    private AnimationRowData GetSelectedAnimation()
    {
        if (_spriteSheetData == null) return null;
        if (_selectedAnimationIndex < 0 || _selectedAnimationIndex >= _spriteSheetData.Animations.Count)
            return null;

        return _spriteSheetData.Animations[_selectedAnimationIndex];
    }

    private void RefreshPreview()
    {
        ClearPreviewTextures();
        _previewContent?.Clear();

        if (_previewStatusLabel == null)
            return;

        var animation = GetSelectedAnimation();
        if (animation == null)
        {
            _previewStatusLabel.text = "アニメーション行を選択してください。";
            return;
        }

        if (string.IsNullOrWhiteSpace(_spriteSheetData?.TextureGuid))
        {
            _previewStatusLabel.text = "テクスチャを選択してください。";
            return;
        }

        if (AssetManager.Instance == null)
        {
            _previewStatusLabel.text = "AssetManagerが見つかりません。";
            return;
        }

        var texture = AssetManager.Instance.LoadTexture(_spriteSheetData.TextureGuid);
        if (texture == null)
        {
            _previewStatusLabel.text = "テクスチャを読み込めませんでした。";
            return;
        }

        var previewCount = 0;
        var totalFrames = Mathf.Max(1, _spriteSheetData.Columns) * Mathf.Max(1, _spriteSheetData.Rows);
        var startFrame = Mathf.Clamp(animation.StartFrame, 0, Mathf.Max(0, totalFrames - 1));
        var frameCount = Mathf.Max(1, animation.FrameCount);

        for (var offset = 0; offset < frameCount && startFrame + offset < totalFrames; offset++)
        {
            var frameIndex = startFrame + offset;
            var thumbnail = CreateThumbnail(texture, frameIndex);
            if (thumbnail == null)
                continue;

            previewCount++;
            _previewContent?.Add(CreateThumbnailElement(frameIndex, thumbnail));
        }

        _previewStatusLabel.text = previewCount > 0
            ? $"{previewCount} フレーム"
            : "表示できるフレームがありません。";
    }

    private Texture2D CreateThumbnail(Texture2D sourceTexture, int frameIndex)
    {
        var frameWidth = Mathf.Max(1, _spriteSheetData.FrameWidth);
        var frameHeight = Mathf.Max(1, _spriteSheetData.FrameHeight);
        var columns = Mathf.Max(1, _spriteSheetData.Columns);

        var column = frameIndex % columns;
        var row = frameIndex / columns;
        var x = column * frameWidth;
        var y = sourceTexture.height - ((row + 1) * frameHeight);

        if (x < 0 || y < 0 || x + frameWidth > sourceTexture.width || y + frameHeight > sourceTexture.height)
            return null;

        var pixels = sourceTexture.GetPixels(x, y, frameWidth, frameHeight);
        var thumbnail = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        thumbnail.SetPixels(pixels);
        thumbnail.Apply();

        _previewTextures.Add(thumbnail);
        return thumbnail;
    }

    private VisualElement CreateThumbnailElement(int frameIndex, Texture2D thumbnail)
    {
        var container = new VisualElement();
        container.style.width = ThumbnailSize + 12f;
        container.style.marginRight = 8;
        container.style.marginBottom = 8;
        container.style.alignItems = Align.Center;

        var image = new Image
        {
            image = thumbnail,
            scaleMode = ScaleMode.ScaleToFit
        };
        image.style.width = ThumbnailSize;
        image.style.height = ThumbnailSize;
        image.style.borderBottomWidth = 1;
        image.style.borderTopWidth = 1;
        image.style.borderLeftWidth = 1;
        image.style.borderRightWidth = 1;
        image.style.borderBottomColor = BorderColor;
        image.style.borderTopColor = BorderColor;
        image.style.borderLeftColor = BorderColor;
        image.style.borderRightColor = BorderColor;
        container.Add(image);

        var label = new Label($"Frame {frameIndex}");
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        container.Add(label);

        return container;
    }

    private void ClearPreviewTextures()
    {
        foreach (var previewTexture in _previewTextures)
        {
            if (previewTexture != null)
                Destroy(previewTexture);
        }

        _previewTextures.Clear();
    }

    private void HandleAssetChanged(string _)
    {
        RefreshTextureChoices();
    }

    private void EnsureDataValidity()
    {
        _spriteSheetData ??= new SpriteSheetData();
        _spriteSheetData.TextureGuid ??= string.Empty;
        _spriteSheetData.FrameWidth = Mathf.Max(1, _spriteSheetData.FrameWidth);
        _spriteSheetData.FrameHeight = Mathf.Max(1, _spriteSheetData.FrameHeight);
        _spriteSheetData.Columns = Mathf.Max(1, _spriteSheetData.Columns);
        _spriteSheetData.Rows = Mathf.Max(1, _spriteSheetData.Rows);
        _spriteSheetData.Animations ??= new List<AnimationRowData>();

        for (var i = 0; i < _spriteSheetData.Animations.Count; i++)
        {
            var animation = _spriteSheetData.Animations[i] ?? new AnimationRowData();
            animation.Name ??= string.Empty;
            animation.StartFrame = Mathf.Max(0, animation.StartFrame);
            animation.FrameCount = Mathf.Max(1, animation.FrameCount);
            animation.Fps = Mathf.Max(1, animation.Fps);
            _spriteSheetData.Animations[i] = animation;
        }

        if (_selectedAnimationIndex >= _spriteSheetData.Animations.Count)
            _selectedAnimationIndex = _spriteSheetData.Animations.Count - 1;
    }

    private static void AddActionButton(VisualElement parent, string label, System.Action callback)
    {
        var button = new Button(() => callback?.Invoke())
        {
            text = label
        };
        button.style.marginRight = 4;
        button.style.marginBottom = 4;
        parent.Add(button);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message;
    }
}
