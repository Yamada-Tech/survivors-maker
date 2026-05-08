using UnityEngine;

/// <summary>
/// プレイヤースプライトアニメーションを管理する。
/// スプライトシートをUnityでスライス後、インスペクタから各配列に設定する。
///
/// スプライトシート仕様: 36x24px/フレーム、右向き。左向きは flipX で対応。
///
/// 【Unityでのスライス手順】
/// 1. スプライトシート画像を Assets/Sprites/ にインポート
/// 2. Texture Type: Sprite、Sprite Mode: Multiple、Pixels Per Unit: 36 に設定
/// 3. Sprite Editor → Slice → Type: Grid By Cell Size → 36x24 でスライス
/// 4. 各行のスプライトを PlayerAnimator の対応する配列にドラッグ：
///    - _idleFrames : 行3のフレーム
///    - _walkFrames : 行1〜2のフレーム
///    - _hitFrames  : 行6の3フレーム
///    - _dieFrames  : 行7の6フレーム
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    public enum AnimState { Idle, Walk, Hit, Die }

    [Header("アニメーションフレーム")]
    [Tooltip("アイドルアニメーション（行3）")]
    [SerializeField] private Sprite[] _idleFrames;

    [Tooltip("歩行アニメーション（行1〜2）")]
    [SerializeField] private Sprite[] _walkFrames;

    [Tooltip("被弾アニメーション（行6、3フレーム）")]
    [SerializeField] private Sprite[] _hitFrames;

    [Tooltip("死亡アニメーション（行7、6フレーム）")]
    [SerializeField] private Sprite[] _dieFrames;

    [Header("再生速度（秒/フレーム）")]
    [SerializeField] private float _idleFrameDuration = 0.2f;
    [SerializeField] private float _walkFrameDuration = 0.1f;
    [SerializeField] private float _hitFrameDuration = 0.1f;
    [SerializeField] private float _dieFrameDuration = 0.12f;

    private SpriteRenderer _sr;
    private AnimState _currentState = AnimState.Idle;
    private int _frameIndex;
    private float _frameTimer;
    private bool _locked;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        Sprite[] frames = GetFrames(_currentState);
        if (frames == null || frames.Length == 0) return;

        float frameDuration = GetFrameDuration(_currentState);
        _frameTimer += Time.deltaTime;

        if (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex++;

            if (_currentState == AnimState.Die)
            {
                if (_frameIndex >= frames.Length)
                    _frameIndex = frames.Length - 1;
            }
            else if (_frameIndex >= frames.Length)
            {
                _frameIndex = 0;

                if (_currentState == AnimState.Hit)
                {
                    _locked = false;
                    SetState(AnimState.Idle);
                    return;
                }
            }

            if (_frameIndex < frames.Length && frames[_frameIndex] != null)
                _sr.sprite = frames[_frameIndex];
        }
    }

    /// <summary>外部からステートを設定する（ロック中は Hit / Die のみ上書き可）</summary>
    public void SetState(AnimState state)
    {
        if (state == AnimState.Die)
        {
            _locked = true;
            ChangeState(state);
            return;
        }

        if (state == AnimState.Hit && !_locked)
        {
            _locked = true;
            ChangeState(state);
            return;
        }

        if (_locked) return;

        if (_currentState != state)
            ChangeState(state);
    }

    private void ChangeState(AnimState state)
    {
        _currentState = state;
        _frameIndex = 0;
        _frameTimer = 0f;

        var frames = GetFrames(state);
        if (frames != null && frames.Length > 0 && frames[0] != null)
            _sr.sprite = frames[0];
    }

    /// <summary>移動方向に応じて左右反転を設定する</summary>
    public void SetFacing(Vector2 moveDir)
    {
        if (moveDir.x < -0.01f)
            _sr.flipX = true;
        else if (moveDir.x > 0.01f)
            _sr.flipX = false;
    }

    private Sprite[] GetFrames(AnimState state) => state switch
    {
        AnimState.Idle => _idleFrames,
        AnimState.Walk => _walkFrames,
        AnimState.Hit => _hitFrames,
        AnimState.Die => _dieFrames,
        _ => _idleFrames
    };

    private float GetFrameDuration(AnimState state) => state switch
    {
        AnimState.Idle => _idleFrameDuration,
        AnimState.Walk => _walkFrameDuration,
        AnimState.Hit => _hitFrameDuration,
        AnimState.Die => _dieFrameDuration,
        _ => _idleFrameDuration
    };
}
