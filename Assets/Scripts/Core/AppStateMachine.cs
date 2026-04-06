using System;
using UnityEngine;

public class AppStateMachine : MonoBehaviour
{
    public static AppStateMachine Instance { get; private set; }

    public AppState CurrentState { get; private set; } = AppState.Editor;

    public event Action<AppState, AppState> OnStateChanged; // (oldState, newState)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ChangeState(AppState newState)
    {
        if (CurrentState == newState) return;

        var old = CurrentState;
        CurrentState = newState;

        // TODO: シーン切替やサブシステム初期化/停止をここで呼ぶ
        HandleStateTransition(old, newState);

        OnStateChanged?.Invoke(old, newState);
        EventBus.Publish(new AppStateChangedEvent(old, newState));
    }

    private void HandleStateTransition(AppState from, AppState to)
    {
        switch (to)
        {
            case AppState.Play:
                // TODO: プレイモード初期化
                Time.timeScale = 1f;
                break;
            case AppState.Pause:
                Time.timeScale = 0f;
                break;
            case AppState.Editor:
                // TODO: エディタモード復帰処理
                Time.timeScale = 1f;
                break;
        }
    }
}
