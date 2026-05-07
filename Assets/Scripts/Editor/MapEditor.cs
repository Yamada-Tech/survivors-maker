using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MapEditor : MonoBehaviour
{
    private const string MapFileName = "map.json";
    private const int DefaultMapSize = 32;
    private const int TileSize = 32;
    private const int DefaultPaletteMax = 9;

    [SerializeField] private int _mapWidth = DefaultMapSize;
    [SerializeField] private int _mapHeight = DefaultMapSize;

    private MapData _mapData;
    private int _selectedTileId = 1;
    private int _activeLayerIndex;

    private readonly Stack<MapCommand> _undoStack = new();
    private readonly Stack<MapCommand> _redoStack = new();

    private UIDocument _uiDocument;
    private DropdownField _layerDropdown;
    private VisualElement _paletteRoot;
    private VisualElement _gridRoot;

    private readonly List<VisualElement> _cellElements = new();
    private readonly List<Button> _paletteButtons = new();
    private bool _isUiBuilt;
    private bool _isPainting;
    private int _activePointerId = -1;

    public void Initialize(MapData data)
    {
        _mapData = data ?? CreateDefaultMapData();
        EnsureMapDataValidity(_mapData);
        BuildUi();
        RefreshAll();
    }

    private void OnEnable()
    {
        if (_mapData == null || _mapData.Layers == null)
        {
            _mapData = DataManager.Instance != null
                ? DataManager.Instance.Load<MapData>(MapFileName)
                : CreateDefaultMapData();
            EnsureMapDataValidity(_mapData);
        }

        BuildUi();
        RefreshAll();
    }

    public void PlaceTile(int x, int y)
    {
        if (_mapData == null) return;
        if (x < 0 || y < 0 || x >= _mapData.Width || y >= _mapData.Height) return;
        if (_activeLayerIndex < 0 || _activeLayerIndex >= _mapData.Layers.Count) return;

        var layer = _mapData.Layers[_activeLayerIndex];
        var index = y * _mapData.Width + x;
        if (index < 0 || index >= layer.Tiles.Length) return;

        var oldTile = layer.Tiles[index];
        if (oldTile == _selectedTileId) return;

        var cmd = new MapCommand(index, oldTile, _selectedTileId, layer);
        cmd.Execute();
        _undoStack.Push(cmd);
        _redoStack.Clear();

        UpdateCellVisual(index);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        UpdateCellVisual(cmd.Index);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        UpdateCellVisual(cmd.Index);
    }

    public void Clear()
    {
        if (_mapData == null) return;
        if (_activeLayerIndex < 0 || _activeLayerIndex >= _mapData.Layers.Count) return;

        var layer = _mapData.Layers[_activeLayerIndex];
        for (var i = 0; i < layer.Tiles.Length; i++)
        {
            layer.Tiles[i] = 0;
        }

        _undoStack.Clear();
        _redoStack.Clear();
        RefreshGridVisuals();
    }

    public void Save()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[MapEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_mapData, MapFileName);
    }

    public void Load()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[MapEditor] DataManager.Instance is null.");
            return;
        }

        _mapData = DataManager.Instance.Load<MapData>(MapFileName);
        EnsureMapDataValidity(_mapData);
        _undoStack.Clear();
        _redoStack.Clear();
        RefreshAll();
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[MapEditor] UIDocument root not found.");
            return;
        }

        _layerDropdown = root.Q<DropdownField>("LayerDropdown");
        _paletteRoot = root.Q<VisualElement>("TilePalette");
        _gridRoot = root.Q<VisualElement>("MapGrid");

        if (_layerDropdown == null || _paletteRoot == null || _gridRoot == null)
        {
            BuildFallbackUi(root);
        }

        if (_layerDropdown != null)
        {
            _layerDropdown.RegisterValueChangedCallback(evt =>
            {
                var index = _layerDropdown.choices.IndexOf(evt.newValue);
                if (index >= 0)
                {
                    _activeLayerIndex = index;
                    RefreshGridVisuals();
                }
            });
        }

        root.Q<Button>("SaveBtn")?.clicked += Save;
        root.Q<Button>("LoadBtn")?.clicked += Load;
        root.Q<Button>("UndoBtn")?.clicked += Undo;
        root.Q<Button>("RedoBtn")?.clicked += Redo;
        root.Q<Button>("ClearBtn")?.clicked += Clear;

        BuildPalette();
        BuildGrid();

        root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        root.RegisterCallback<PointerCaptureOutEvent>(_ => StopPainting());

        _isUiBuilt = true;
    }

    private void BuildFallbackUi(VisualElement root)
    {
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;

        var header = new VisualElement { name = "MapEditorHeader" };
        header.style.flexDirection = FlexDirection.Row;
        header.style.marginBottom = 8;
        root.Add(header);

        _layerDropdown ??= new DropdownField { name = "LayerDropdown", label = "Layer" };
        _layerDropdown.choices = new List<string>();
        _layerDropdown.index = 0;
        _layerDropdown.style.width = 180;
        header.Add(_layerDropdown);

        AddHeaderButton(header, "SaveBtn", "Save", Save);
        AddHeaderButton(header, "LoadBtn", "Load", Load);
        AddHeaderButton(header, "UndoBtn", "Undo", Undo);
        AddHeaderButton(header, "RedoBtn", "Redo", Redo);
        AddHeaderButton(header, "ClearBtn", "Clear", Clear);

        _paletteRoot ??= new VisualElement { name = "TilePalette" };
        _paletteRoot.style.flexDirection = FlexDirection.Row;
        _paletteRoot.style.flexWrap = Wrap.Wrap;
        _paletteRoot.style.marginBottom = 8;
        root.Add(_paletteRoot);

        _gridRoot ??= new VisualElement { name = "MapGrid" };
        _gridRoot.style.flexDirection = FlexDirection.Column;
        _gridRoot.style.width = _mapWidth * TileSize;
        _gridRoot.style.height = _mapHeight * TileSize;
        root.Add(_gridRoot);
    }

    private void BuildPalette()
    {
        if (_paletteRoot == null) return;

        _paletteRoot.Clear();
        _paletteButtons.Clear();

        for (var tileId = 0; tileId <= DefaultPaletteMax; tileId++)
        {
            var id = tileId;
            var button = new Button(() =>
            {
                _selectedTileId = id;
                RefreshPaletteSelection();
            })
            {
                text = id.ToString()
            };

            button.style.width = TileSize;
            button.style.height = TileSize;
            button.style.marginRight = 2;
            button.style.marginBottom = 2;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;

            _paletteButtons.Add(button);
            _paletteRoot.Add(button);
        }

        RefreshPaletteSelection();
    }

    private void BuildGrid()
    {
        if (_gridRoot == null || _mapData == null) return;

        _gridRoot.Clear();
        _cellElements.Clear();

        _gridRoot.style.flexDirection = FlexDirection.Column;

        for (var y = 0; y < _mapData.Height; y++)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            _gridRoot.Add(row);

            for (var x = 0; x < _mapData.Width; x++)
            {
                var cell = new Label
                {
                    name = $"Tile_{x}_{y}",
                    userData = y * _mapData.Width + x
                };
                cell.style.width = TileSize;
                cell.style.height = TileSize;
                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;
                cell.style.borderTopWidth = 1;
                cell.style.borderBottomWidth = 1;
                cell.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
                cell.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
                cell.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
                cell.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
                cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.style.fontSize = 10;

                cell.RegisterCallback<PointerDownEvent>(evt => OnCellPointerDown(evt, cell));
                cell.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (_isPainting)
                    {
                        PaintCellByElement(cell);
                    }
                });

                _cellElements.Add(cell);
                row.Add(cell);
            }
        }

        RefreshGridVisuals();
    }

    private void OnCellPointerDown(PointerDownEvent evt, VisualElement cell)
    {
        _isPainting = true;
        _activePointerId = evt.pointerId;
        _gridRoot?.CapturePointer(_activePointerId);
        PaintCellByElement(cell);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isPainting || evt.pointerId != _activePointerId) return;
        StopPainting();
    }

    private void StopPainting()
    {
        if (_activePointerId >= 0 && _gridRoot != null && _gridRoot.HasPointerCapture(_activePointerId))
        {
            _gridRoot.ReleasePointer(_activePointerId);
        }

        _isPainting = false;
        _activePointerId = -1;
    }

    private void PaintCellByElement(VisualElement cell)
    {
        if (cell?.userData is not int index || _mapData == null) return;
        var x = index % _mapData.Width;
        var y = index / _mapData.Width;
        PlaceTile(x, y);
    }

    private void RefreshAll()
    {
        RefreshLayerChoices();
        if (_cellElements.Count == 0)
        {
            BuildGrid();
        }
        else
        {
            RefreshGridVisuals();
        }

        RefreshPaletteSelection();
    }

    private void RefreshLayerChoices()
    {
        if (_layerDropdown == null || _mapData == null) return;

        var choices = new List<string>();
        for (var i = 0; i < _mapData.Layers.Count; i++)
        {
            var layerName = _mapData.Layers[i].LayerName;
            choices.Add(string.IsNullOrWhiteSpace(layerName) ? $"Layer {i}" : layerName);
        }

        _layerDropdown.choices = choices;
        _activeLayerIndex = Mathf.Clamp(_activeLayerIndex, 0, _mapData.Layers.Count - 1);
        if (_layerDropdown.choices.Count > 0)
        {
            _layerDropdown.SetValueWithoutNotify(_layerDropdown.choices[_activeLayerIndex]);
        }
    }

    private void RefreshPaletteSelection()
    {
        for (var i = 0; i < _paletteButtons.Count; i++)
        {
            var isSelected = i == _selectedTileId;
            _paletteButtons[i].style.backgroundColor = isSelected
                ? new Color(0.2f, 0.6f, 1f, 1f)
                : new Color(0.15f, 0.15f, 0.15f, 1f);
        }
    }

    private void RefreshGridVisuals()
    {
        for (var i = 0; i < _cellElements.Count; i++)
        {
            UpdateCellVisual(i);
        }
    }

    private void UpdateCellVisual(int index)
    {
        if (_mapData == null || index < 0 || index >= _cellElements.Count) return;
        if (_activeLayerIndex < 0 || _activeLayerIndex >= _mapData.Layers.Count) return;

        var layer = _mapData.Layers[_activeLayerIndex];
        if (index >= layer.Tiles.Length) return;

        var tileId = layer.Tiles[index];
        if (_cellElements[index] is Label label)
        {
            label.text = tileId == 0 ? string.Empty : tileId.ToString();
            label.style.backgroundColor = tileId == 0
                ? new Color(0f, 0f, 0f, 0.25f)
                : GetTileColor(tileId, _activeLayerIndex);
        }
    }

    private static Color GetTileColor(int tileId, int layerIndex)
    {
        var baseHue = (tileId * 0.11f) % 1f;
        var saturation = Mathf.Clamp01(0.35f + layerIndex * 0.2f);
        var value = Mathf.Clamp01(0.45f + layerIndex * 0.12f);
        return Color.HSVToRGB(baseHue, saturation, value);
    }

    private MapData CreateDefaultMapData()
    {
        _mapWidth = DefaultMapSize;
        _mapHeight = DefaultMapSize;

        return new MapData
        {
            Width = _mapWidth,
            Height = _mapHeight,
            TileSize = TileSize,
            Layers = new List<TileLayer>()
        };
    }

    private void EnsureMapDataValidity(MapData data)
    {
        data ??= CreateDefaultMapData();

        _mapWidth = DefaultMapSize;
        _mapHeight = DefaultMapSize;

        data.Width = _mapWidth;
        data.Height = _mapHeight;
        data.TileSize = TileSize;
        data.Layers ??= new List<TileLayer>();

        EnsureLayer(data, 0, "Ground");
        EnsureLayer(data, 1, "Object");
        EnsureLayer(data, 2, "Event");
    }

    private void EnsureLayer(MapData data, int index, string defaultName)
    {
        while (data.Layers.Count <= index)
        {
            data.Layers.Add(new TileLayer());
        }

        var layer = data.Layers[index];
        layer.LayerName = string.IsNullOrWhiteSpace(layer.LayerName) ? defaultName : layer.LayerName;

        var expectedLength = data.Width * data.Height;
        if (layer.Tiles == null || layer.Tiles.Length != expectedLength)
        {
            var newTiles = new int[expectedLength];
            if (layer.Tiles != null)
            {
                var copyLength = Mathf.Min(layer.Tiles.Length, newTiles.Length);
                for (var i = 0; i < copyLength; i++)
                {
                    newTiles[i] = layer.Tiles[i];
                }
            }

            layer.Tiles = newTiles;
        }
    }

    private static void AddHeaderButton(VisualElement parent, string name, string text, System.Action callback)
    {
        var button = new Button(callback)
        {
            name = name,
            text = text
        };
        button.style.marginLeft = 4;
        parent.Add(button);
    }
}

public class MapCommand
{
    private readonly int _index;
    private readonly int _oldTile;
    private readonly int _newTile;
    private readonly TileLayer _layer;

    public int Index => _index;

    public MapCommand(int index, int oldTile, int newTile, TileLayer layer)
    {
        _index = index;
        _oldTile = oldTile;
        _newTile = newTile;
        _layer = layer;
    }

    public void Execute()
    {
        _layer.Tiles[_index] = _newTile;
    }

    public void Undo()
    {
        _layer.Tiles[_index] = _oldTile;
    }
}
