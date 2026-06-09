using System.Collections;
using UnityEngine;

/// <summary>
/// 武器攻撃範囲を一定時間だけ表示するエフェクト。
/// WeaponSystem から Show() を呼び出す。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class WeaponRangeIndicator : MonoBehaviour
{
    private const int CircleSegments = 40;
    private const float DefaultAlpha = 0.45f;

    private LineRenderer _lineRenderer;
    private Coroutine _hideCoroutine;
    private Material _indicatorMaterial;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.positionCount = CircleSegments;
        _lineRenderer.startWidth = 0.08f;
        _lineRenderer.endWidth = 0.08f;
        _lineRenderer.enabled = false;

        var defaultShader = Shader.Find("Sprites/Default");
        if (defaultShader != null)
        {
            _indicatorMaterial = new Material(defaultShader);
            _lineRenderer.material = _indicatorMaterial;
        }
        else
        {
            Debug.LogWarning("[WeaponRangeIndicator] Shader 'Sprites/Default' not found.");

            var fallbackShader = Shader.Find("Unlit/Color");
            if (fallbackShader != null)
            {
                _indicatorMaterial = new Material(fallbackShader);
                _lineRenderer.material = _indicatorMaterial;
            }
        }
    }

    private void OnDestroy()
    {
        if (_indicatorMaterial != null)
            Destroy(_indicatorMaterial);
    }

    /// <summary>
    /// 攻撃範囲インジケーターを表示する。
    /// </summary>
    /// <param name="radius">半径（Unity単位）</param>
    /// <param name="color">表示色</param>
    /// <param name="duration">表示秒数</param>
    public void Show(float radius, Color color, float duration = 0.25f)
    {
        DrawCircle(radius);
        color.a = DefaultAlpha;
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
        _lineRenderer.enabled = true;

        if (_hideCoroutine != null)
            StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideAfter(duration));
    }

    private void DrawCircle(float radius)
    {
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / CircleSegments;
            _lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _lineRenderer.enabled = false;
    }
}
