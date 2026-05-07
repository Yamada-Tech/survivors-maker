using UnityEngine;
using UnityEngine.UIElements;

public class LevelUpUI : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _panel;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _panel = _root.Q("LevelUpPanel");
        _panel.style.display = DisplayStyle.None;
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
    }

    private void OnDisable() => EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);

    private void OnLevelUp(LevelUpEvent evt)
    {
        AppStateMachine.Instance.ChangeState(AppState.Pause);
        _panel.style.display = DisplayStyle.Flex;
        // ランダムに3つの強化選択肢を生成して表示
        ShowUpgradeOptions();
    }

    private void ShowUpgradeOptions()
    {
        // TODO: ランダム3択の強化選択肢を生成・表示
        // 選択後 → AppStateMachine.Instance.ChangeState(AppState.Play);
    }
}
