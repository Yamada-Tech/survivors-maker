using UnityEngine;

public class ExpGem : MonoBehaviour
{
    [SerializeField] private int _expValue = 10;
    [SerializeField] private float _attractRadius = 4f;
    [SerializeField] private float _attractSpeed = 8f;

    private Transform _player;
    private bool _attracted;

    private void Awake()
    {
        gameObject.tag = "PlayObject";
    }

    private void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;
    }

    private void Update()
    {
        if (_player == null) return;

        float dist = Vector2.Distance(transform.position, _player.position);
        if (dist <= _attractRadius)
            _attracted = true;

        if (_attracted)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, _player.position,
                _attractSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.TryGetComponent<PlayerController>(out var player))
        {
            player.GainExp(_expValue);
            Destroy(gameObject);
        }
    }
}
