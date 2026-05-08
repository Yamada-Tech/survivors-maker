using UnityEngine;

/// <summary>
/// ダメージ数字をOnGUIで描画し、上にフワっと浮かんで消える。
/// DamageNumberSpawner によって生成される。
/// </summary>
public class DamageNumber : MonoBehaviour
{
    private string  _text;
    private Color   _color;
    private float   _duration   = 0.9f;
    private float   _elapsed;
    private Vector3 _startPos;
    private float   _floatSpeed = 1.8f;
    private int     _fontSize   = 24;

    private GUIStyle _style;

    public void Initialize(string text, Color color, float duration = 0.9f, float floatSpeed = 1.8f, int fontSize = 24)
    {
        _text       = text;
        _color      = color;
        _duration   = duration;
        _floatSpeed = floatSpeed;
        _fontSize   = fontSize;
        _startPos   = transform.position;
        _elapsed    = 0f;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        transform.position = _startPos + Vector3.up * (_floatSpeed * _elapsed);
        if (_elapsed >= _duration)
            Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize  = _fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        float alpha = Mathf.Clamp01(1f - (_elapsed / _duration));
        _style.normal.textColor = new Color(_color.r, _color.g, _color.b, alpha);

        // ワールド座標 → スクリーン座標変換
        var cam = Camera.main;
        if (cam == null) return;

        var screenPos = cam.WorldToScreenPoint(transform.position);
        if (screenPos.z < 0) return; // カメラ後ろは描画しない

        float w = 100f, h = 40f;
        float sx = screenPos.x - w * 0.5f;
        float sy = Screen.height - screenPos.y - h * 0.5f;

        GUI.Label(new Rect(sx, sy, w, h), _text, _style);
    }
}
