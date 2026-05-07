using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ModeToggle : MonoBehaviour
{
    private Button _toggleBtn;
    private Label _statusLabel;
    private AppStateMachine _stateMachine;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _toggleBtn = root?.Q<Button>("ToggleModeBtn");
        _statusLabel = root?.Q<Label>("ModeStatus");

        _stateMachine = AppStateMachine.Instance;

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
                EventBus.Publish(new SaveAllRequestedEvent());
                _stateMachine.ChangeState(AppState.Play);
                break;

            case AppState.Play:
            case AppState.Pause:
                _stateMachine.ChangeState(AppState.Editor);
                break;
        }
    }

    private void HandleStateChanged(AppState _oldState, AppState newState)
    {
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
