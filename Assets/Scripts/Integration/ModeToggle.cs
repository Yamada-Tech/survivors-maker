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
            _stateMachine.OnStateChanged += UpdateLabel;
            UpdateLabel(_stateMachine.CurrentState, _stateMachine.CurrentState);
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
            _stateMachine.OnStateChanged -= UpdateLabel;
    }

    private void OnToggle()
    {
        if (_stateMachine == null) return;

        switch (_stateMachine.CurrentState)
        {
            case AppState.Editor:
                if (_dataManager != null)
                    _dataManager.SaveProject(_dataManager.LoadProject()); // 自動保存
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

    private void UpdateLabel(AppState _, AppState newState)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = newState switch
            {
                AppState.Editor => "📝 EDITOR",
                AppState.Play => "▶ PLAY",
                AppState.Pause => "⏸ PAUSE",
                _ => "---"
            };
        }

        if (_toggleBtn != null)
            _toggleBtn.text = newState == AppState.Editor ? "▶ Play" : "🛑 Stop";
    }
}
