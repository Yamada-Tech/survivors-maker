using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _smoothSpeed = 5f;
    [SerializeField] private Vector3 _offset = new Vector3(0, 0, -10f);

    public void SetTarget(Transform target) => _target = target;

    private void LateUpdate()
    {
        if (_target == null) return;
        var desired = _target.position + _offset;
        transform.position = Vector3.Lerp(transform.position, desired, _smoothSpeed * Time.deltaTime);
    }
}
