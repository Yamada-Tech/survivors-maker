using UnityEngine;

public class TitleScreen : MonoBehaviour
{
    private bool _isVisible = true;

    private GUIStyle _bgStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _startButtonStyle;
    private GUIStyle _versionStyle;
    private Texture2D _bgTexture;
    private bool _stylesInit;

    private void OnEnable()
    {
        EventBus.Subscribe<AppStateChangedEvent>(OnStateChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AppStateChangedEvent>(OnStateChanged);
    }

    private void OnDestroy()
    {
        if (_bgTexture != null)
            Destroy(_bgTexture);
    }

    private void OnStateChanged(AppStateChangedEvent evt)
    {
        _isVisible = evt.NewState == AppState.Title;
    }

    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _bgTexture = new Texture2D(1, 1);
        _bgTexture.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.1f));
        _bgTexture.Apply();

        _bgStyle = new GUIStyle();
        _bgStyle.normal.background = _bgTexture;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 52,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }
        };

        _subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.8f, 0.8f, 1f, 0.9f) }
        };

        _startButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        _versionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(1f, 1f, 1f, 0.5f) }
        };
    }

    private void OnGUI()
    {
        if (!_isVisible) return;

        InitStyles();

        float w = Screen.width;
        float h = Screen.height;

        // 背景
        GUI.Box(new Rect(0, 0, w, h), GUIContent.none, _bgStyle);

        // タイトルロゴ
        float titleY = h * 0.25f;
        GUI.Label(new Rect(0, titleY, w, 70), "⚔️ SURVIVORS MAKER", _titleStyle);

        // サブタイトル
        GUI.Label(new Rect(0, titleY + 78, w, 36), "〜 あなただけのサバイバーを作ろう 〜", _subtitleStyle);

        // スタートボタン
        float btnW = 320f;
        float btnH = 64f;
        float btnX = (w - btnW) * 0.5f;
        float btnY = h * 0.55f;
        if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "▶ ゲームをはじめる", _startButtonStyle))
        {
            AppStateMachine.Instance?.ChangeState(AppState.Editor);
        }

        // バージョン表示
        GUI.Label(new Rect(12, h - 30, 300, 24), "v0.1.0 - survivors-maker", _versionStyle);
    }
}
