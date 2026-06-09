using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AttackRangeEffect : MonoBehaviour
{
    private const int CircleSegments = 32;

    private LineRenderer _lineRenderer;
    private Material _material;

    private void Awake()
    {
        gameObject.tag = "PlayObject";

        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.startWidth = 0.06f;
        _lineRenderer.endWidth = 0.06f;
        _lineRenderer.positionCount = CircleSegments;
        _lineRenderer.sortingOrder = 5;

        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            _material = new Material(shader);
            _lineRenderer.material = _material;
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    public void Initialize(float radius, Color color, float duration)
    {
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / CircleSegments;
            _lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;

        StartCoroutine(FadeOut(duration, color));
    }

    private IEnumerator FadeOut(float duration, Color color)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float alpha = Mathf.Lerp(color.a, 0f, t);
            var fadeColor = new Color(color.r, color.g, color.b, alpha);
            _lineRenderer.startColor = fadeColor;
            _lineRenderer.endColor = fadeColor;
            yield return null;
        }

        Destroy(gameObject);
    }
}
