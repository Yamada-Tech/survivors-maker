using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ModeToggle : MonoBehaviour
{
    private Button _toggleBtn;
    private Label _statusLabel;
    private AppStateMachine _stateMachine;
    private DataManager _dataManager;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _toggleBtn = root?.Q<Button>("ToggleModeBtn");
        _statusLabel = root?.Q<Label>("ModeStatus");

        _stateMachine = AppStateMachine.Instance;
        _dataManager = DataManager.Instance;

        if (_toggleBtn != null)
            _toggleBtn.clicked += OnToggle;

        if (_stateMachine != null)
        {
            _stateMachine.OnStateChanged += HandleStateChanged;
            UpdateLabel(_stateMachine.CurrentState);
        }
        else
        {
            Debug.LogWarning("[ModeToggle] AppStateMachine.Instance is null.");
        }
    }

    private void OnDisable()
    {
        if (_toggleBtn != null)
            _toggleBtn.clicked -= OnToggle;

        if (_stateMachine != null)
            _stateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void OnToggle()
    {
        if (_stateMachine == null) return;

        switch (_stateMachine.CurrentState)
        {
            case AppState.Editor:
                if (_dataManager != null)
                    // 現在の実装では一元管理されたProjectDataインスタンスが無いため、
                    // 永続化済みスナップショットを再ロードして即保存することで
                    // Play遷移前の保存フローを統一する。
                    _dataManager.SaveProject(_dataManager.LoadProject());
                else
                    Debug.LogWarning("[ModeToggle] DataManager.Instance is null. Skip auto-save.");

                _stateMachine.ChangeState(AppState.Play);
                break;

            case AppState.Play:
            case AppState.Pause:
                _stateMachine.ChangeState(AppState.Editor);
                break;
        }
    }

    private void HandleStateChanged(AppState oldState, AppState newState)
    {
        _ = oldState;
        UpdateLabel(newState);
    }

    private void UpdateLabel(AppState state)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = state switch
            {
                AppState.Editor => "📝 EDITOR",
                AppState.Play => "▶ PLAY",
                AppState.Pause => "⏸ PAUSE",
                _ => "---"
            };
        }

        if (_toggleBtn != null)
            _toggleBtn.text = state == AppState.Editor ? "▶ Play" : "🛑 Stop";
    }
}
