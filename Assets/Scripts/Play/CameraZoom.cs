using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スクロールホイールでカメラズームを調整する
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    [SerializeField] private float _minSize = 5f;
    [SerializeField] private float _maxSize = 20f;
    [SerializeField] private float _zoomSpeed = 2f;

    private const float ScrollRawScale = 0.01f; // new Input Systemのスクロール生値（~120/tick）を正規化

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        _cam.orthographicSize = Mathf.Clamp(
            _cam.orthographicSize - scroll * _zoomSpeed * ScrollRawScale,
            _minSize,
            _maxSize);
    }
}
