using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DotArtEditor : MonoBehaviour
{
    private const int CanvasSize = 32;
    private const int CellSize = 12;
    private const int PaletteSize = 16;
    private static readonly Color CellBorderColor = new(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color TransparentCellColor = new(0f, 0f, 0f, 0.35f);
    private static readonly Color SelectedColor = new(0.2f, 0.6f, 1f, 1f);
    private static readonly Color NormalButtonColor = new(0.2f, 0.2f, 0.24f, 1f);
    private static readonly Color[] DefaultPalette =
    {
        Color.black, Color.white, Color.red, Color.green,
        Color.blue, Color.yellow, Color.cyan, Color.magenta,
        new Color(1f, 0.5f, 0f),
        new Color(0.5f, 0f, 0.5f),
        new Color(0.6f, 0.4f, 0.2f),
        new Color(0.5f, 0.5f, 0.5f),
        new Color(1f, 0.75f, 0.8f),
        new Color(0f, 0.5f, 0f),
        new Color(0f, 0f, 0.5f),
        new Color(0.9f, 0.9f, 0.7f)
    };

    private enum ToolType
    {
        Pen,
        Eraser,
        Bucket
    }

    private sealed class DotArtCommand
    {
        public int[] Indices;
        public Color[] Before;
        public Color[] After;
    }

    private readonly Color[] _pixels = new Color[CanvasSize * CanvasSize];
    private readonly VisualElement[] _cellElements = new VisualElement[CanvasSize * CanvasSize];
    private readonly Stack<DotArtCommand> _undoStack = new();
    private readonly Stack<DotArtCommand> _redoStack = new();
    private readonly List<Button> _paletteButtons = new(PaletteSize);
    private readonly Dictionary<ToolType, Button> _toolButtons = new();

    private bool _isUiBuilt;
    private bool _isPainting;
    private int _activePointerId = -1;
    private ToolType _selectedTool = ToolType.Pen;
    private Color _selectedPaintColor = Color.black;

    private VisualElement _canvasRoot;
    private VisualElement _selectedColorPreview;
    private Label _selectedToolLabel;
    private Label _statusLabel;

    private void OnEnable()
    {
        BuildUi();
        RefreshAllCells();
        RefreshPaletteSelection();
        RefreshToolSelection();
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[DotArtEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.flexDirection = FlexDirection.Row;
        root.style.paddingLeft = 8;
        root.style.paddingRight = 8;
        root.style.paddingTop = 8;
        root.style.paddingBottom = 8;
        root.style.flexGrow = 1f;

        var sidebar = new VisualElement();
        sidebar.style.width = 220;
        sidebar.style.flexShrink = 0f;
        sidebar.style.marginRight = 8;
        sidebar.style.flexDirection = FlexDirection.Column;
        root.Add(sidebar);

        var toolHeader = new Label("ツール");
        toolHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolHeader.style.marginBottom = 4;
        sidebar.Add(toolHeader);

        AddToolButton(sidebar, ToolType.Pen, "✏️ ペン");
        AddToolButton(sidebar, ToolType.Eraser, "🧽 消しゴム");
        AddToolButton(sidebar, ToolType.Bucket, "🪣 バケツ");

        _selectedToolLabel = new Label();
        _selectedToolLabel.style.marginTop = 6;
        _selectedToolLabel.style.marginBottom = 8;
        sidebar.Add(_selectedToolLabel);

        var colorHeader = new Label("カラー");
        colorHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        colorHeader.style.marginBottom = 4;
        sidebar.Add(colorHeader);

        _selectedColorPreview = new VisualElement();
        _selectedColorPreview.style.width = 48;
        _selectedColorPreview.style.height = 24;
        _selectedColorPreview.style.marginBottom = 8;
        _selectedColorPreview.style.borderBottomWidth = 1;
        _selectedColorPreview.style.borderTopWidth = 1;
        _selectedColorPreview.style.borderLeftWidth = 1;
        _selectedColorPreview.style.borderRightWidth = 1;
        sidebar.Add(_selectedColorPreview);

        var paletteRoot = new VisualElement();
        paletteRoot.style.flexDirection = FlexDirection.Row;
        paletteRoot.style.flexWrap = Wrap.Wrap;
        paletteRoot.style.marginBottom = 8;
        sidebar.Add(paletteRoot);

        for (var i = 0; i < PaletteSize; i++)
        {
            var paletteIndex = i;
            var colorButton = new Button(() =>
            {
                _selectedPaintColor = DefaultPalette[paletteIndex];
                RefreshPaletteSelection();
            });
            colorButton.style.width = 24;
            colorButton.style.height = 24;
            colorButton.style.marginRight = 4;
            colorButton.style.marginBottom = 4;
            colorButton.style.backgroundColor = DefaultPalette[paletteIndex];
            paletteRoot.Add(colorButton);
            _paletteButtons.Add(colorButton);
        }

        AddActionButton(sidebar, "Undo (Ctrl+Z)", Undo);
        AddActionButton(sidebar, "Redo (Ctrl+Y)", Redo);
        AddActionButton(sidebar, "Clear", ClearCanvas);
        AddActionButton(sidebar, "PNG書き出し", ExportPng);

        _statusLabel = new Label("Ready");
        _statusLabel.style.marginTop = 8;
        sidebar.Add(_statusLabel);

        _canvasRoot = new VisualElement();
        _canvasRoot.style.flexDirection = FlexDirection.Column;
        _canvasRoot.style.width = CanvasSize * CellSize;
        _canvasRoot.style.height = CanvasSize * CellSize;
        _canvasRoot.style.flexShrink = 0f;
        root.Add(_canvasRoot);

        BuildCanvasGrid();

        root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        root.RegisterCallback<PointerCaptureOutEvent>(_ => StopPainting());
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        root.RegisterCallback<PointerDownEvent>(_ => root.Focus());
        root.focusable = true;
        root.tabIndex = 0;

        _isUiBuilt = true;
    }

    private void BuildCanvasGrid()
    {
        if (_canvasRoot == null) return;

        _canvasRoot.Clear();
        for (var y = 0; y < CanvasSize; y++)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            _canvasRoot.Add(row);

            for (var x = 0; x < CanvasSize; x++)
            {
                var index = y * CanvasSize + x;
                var cell = new VisualElement
                {
                    userData = index
                };

                cell.style.width = CellSize;
                cell.style.height = CellSize;
                cell.style.borderBottomWidth = 1;
                cell.style.borderTopWidth = 1;
                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;
                cell.style.borderBottomColor = CellBorderColor;
                cell.style.borderTopColor = CellBorderColor;
                cell.style.borderLeftColor = CellBorderColor;
                cell.style.borderRightColor = CellBorderColor;

                cell.RegisterCallback<PointerDownEvent>(evt => OnCellPointerDown(evt, index));
                cell.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (_isPainting)
                        ApplyTool(index, true);
                });

                _cellElements[index] = cell;
                row.Add(cell);
            }
        }
    }

    private void AddToolButton(VisualElement parent, ToolType toolType, string label)
    {
        var button = new Button(() =>
        {
            _selectedTool = toolType;
            RefreshToolSelection();
        })
        {
            text = label
        };
        button.style.marginBottom = 4;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        parent.Add(button);
        _toolButtons[toolType] = button;
    }

    private static void AddActionButton(VisualElement parent, string label, System.Action action)
    {
        var button = new Button(() => action?.Invoke())
        {
            text = label
        };
        button.style.marginBottom = 4;
        parent.Add(button);
    }

    private void OnCellPointerDown(PointerDownEvent evt, int index)
    {
        _isPainting = true;
        _activePointerId = evt.pointerId;
        _canvasRoot?.CapturePointer(_activePointerId);
        ApplyTool(index, false);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isPainting || evt.pointerId != _activePointerId) return;
        StopPainting();
    }

    private void StopPainting()
    {
        if (_activePointerId >= 0 && _canvasRoot != null && _canvasRoot.HasPointerCapture(_activePointerId))
            _canvasRoot.ReleasePointer(_activePointerId);

        _isPainting = false;
        _activePointerId = -1;
    }

    private void ApplyTool(int index, bool isDrag)
    {
        if (index < 0 || index >= _pixels.Length) return;

        switch (_selectedTool)
        {
            case ToolType.Pen:
                ApplySinglePixel(index, _selectedPaintColor);
                break;
            case ToolType.Eraser:
                ApplySinglePixel(index, Color.clear);
                break;
            case ToolType.Bucket:
                if (!isDrag)
                    FloodFill(index, _selectedPaintColor);
                break;
        }
    }

    private void ApplySinglePixel(int index, Color color)
    {
        var before = _pixels[index];
        if (AreSameColor(before, color)) return;

        PushCommand(new[] { index }, new[] { before }, new[] { color });
        UpdateCellVisual(index);
    }

    private void FloodFill(int startIndex, Color fillColor)
    {
        var startColor = _pixels[startIndex];
        if (AreSameColor(startColor, fillColor)) return;

        var queue = new Queue<int>();
        var visited = new bool[_pixels.Length];
        var changedIndices = new List<int>();
        var beforeColors = new List<Color>();
        var afterColors = new List<Color>();

        queue.Enqueue(startIndex);
        visited[startIndex] = true;

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            if (!AreSameColor(_pixels[index], startColor)) continue;

            changedIndices.Add(index);
            beforeColors.Add(_pixels[index]);
            afterColors.Add(fillColor);

            var x = index % CanvasSize;
            var y = index / CanvasSize;

            EnqueueNeighbor(x - 1, y);
            EnqueueNeighbor(x + 1, y);
            EnqueueNeighbor(x, y - 1);
            EnqueueNeighbor(x, y + 1);
        }

        if (changedIndices.Count == 0) return;

        PushCommand(changedIndices.ToArray(), beforeColors.ToArray(), afterColors.ToArray());
        for (var i = 0; i < changedIndices.Count; i++)
            UpdateCellVisual(changedIndices[i]);

        void EnqueueNeighbor(int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= CanvasSize || ny >= CanvasSize) return;
            var neighborIndex = ny * CanvasSize + nx;
            if (visited[neighborIndex]) return;
            visited[neighborIndex] = true;
            queue.Enqueue(neighborIndex);
        }
    }

    private void ClearCanvas()
    {
        var changedIndices = new List<int>();
        var beforeColors = new List<Color>();
        var afterColors = new List<Color>();

        for (var i = 0; i < _pixels.Length; i++)
        {
            if (AreSameColor(_pixels[i], Color.clear)) continue;
            changedIndices.Add(i);
            beforeColors.Add(_pixels[i]);
            afterColors.Add(Color.clear);
        }

        if (changedIndices.Count == 0) return;

        PushCommand(changedIndices.ToArray(), beforeColors.ToArray(), afterColors.ToArray());
        RefreshAllCells();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var command = _undoStack.Pop();
        ApplyCommandColors(command.Indices, command.Before);
        _redoStack.Push(command);
        _statusLabel.text = "Undo";
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var command = _redoStack.Pop();
        ApplyCommandColors(command.Indices, command.After);
        _undoStack.Push(command);
        _statusLabel.text = "Redo";
    }

    private void ApplyCommandColors(int[] indices, Color[] colors)
    {
        for (var i = 0; i < indices.Length; i++)
        {
            var index = indices[i];
            _pixels[index] = colors[i];
            UpdateCellVisual(index);
        }
    }

    private void PushCommand(int[] indices, Color[] before, Color[] after)
    {
        var command = new DotArtCommand
        {
            Indices = indices,
            Before = before,
            After = after
        };

        ApplyCommandColors(indices, after);
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (!evt.ctrlKey && !evt.commandKey) return;

        if (evt.keyCode == KeyCode.Z)
        {
            Undo();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Y)
        {
            Redo();
            evt.StopPropagation();
        }
    }

    private void ExportPng()
    {
        var exportDirectory = Path.Combine(Application.persistentDataPath, "ProjectData", "Assets");
        Directory.CreateDirectory(exportDirectory);
        var exportPath = Path.Combine(exportDirectory, "dotart_export.png");

        var tex = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };

        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                tex.SetPixel(x, CanvasSize - 1 - y, _pixels[y * CanvasSize + x]);
            }
        }

        tex.Apply();
        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(exportPath, bytes);
        Destroy(tex);

        EnsureAssetManager();
        var importedGuid = AssetManager.Instance?.ImportTexture(exportPath) ?? string.Empty;
        _statusLabel.text = string.IsNullOrWhiteSpace(importedGuid)
            ? $"PNG保存: {exportPath}"
            : $"PNG保存+Import: {importedGuid}";
    }

    private static void EnsureAssetManager()
    {
        if (AssetManager.Instance != null) return;

        var manager = FindAnyObjectByType<AssetManager>();
        if (manager == null)
        {
            var go = new GameObject("AssetManager");
            go.AddComponent<AssetManager>();
        }
    }

    private void RefreshAllCells()
    {
        for (var i = 0; i < _cellElements.Length; i++)
            UpdateCellVisual(i);
    }

    private void UpdateCellVisual(int index)
    {
        var cell = _cellElements[index];
        if (cell == null) return;
        var color = _pixels[index];
        cell.style.backgroundColor = color.a <= 0.001f ? TransparentCellColor : color;
    }

    private void RefreshPaletteSelection()
    {
        for (var i = 0; i < _paletteButtons.Count; i++)
        {
            var isSelected = AreSameColor(DefaultPalette[i], _selectedPaintColor);
            _paletteButtons[i].style.borderBottomWidth = isSelected ? 2 : 0;
            _paletteButtons[i].style.borderTopWidth = isSelected ? 2 : 0;
            _paletteButtons[i].style.borderLeftWidth = isSelected ? 2 : 0;
            _paletteButtons[i].style.borderRightWidth = isSelected ? 2 : 0;
            _paletteButtons[i].style.borderBottomColor = SelectedColor;
            _paletteButtons[i].style.borderTopColor = SelectedColor;
            _paletteButtons[i].style.borderLeftColor = SelectedColor;
            _paletteButtons[i].style.borderRightColor = SelectedColor;
        }

        if (_selectedColorPreview != null)
            _selectedColorPreview.style.backgroundColor = _selectedPaintColor;
    }

    private void RefreshToolSelection()
    {
        foreach (var pair in _toolButtons)
            pair.Value.style.backgroundColor = pair.Key == _selectedTool ? SelectedColor : NormalButtonColor;

        if (_selectedToolLabel != null)
            _selectedToolLabel.text = $"選択ツール: {GetToolLabel(_selectedTool)}";
    }

    private static string GetToolLabel(ToolType toolType)
    {
        return toolType switch
        {
            ToolType.Pen => "ペン",
            ToolType.Eraser => "消しゴム",
            ToolType.Bucket => "バケツ",
            _ => "不明"
        };
    }

    private static bool AreSameColor(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) &&
               Mathf.Approximately(a.g, b.g) &&
               Mathf.Approximately(a.b, b.b) &&
               Mathf.Approximately(a.a, b.a);
    }
}
