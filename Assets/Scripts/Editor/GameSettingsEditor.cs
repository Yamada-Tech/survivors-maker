using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GameSettingsEditor : MonoBehaviour
{
    private const string GameSettingsFileName = "game_settings.json";
    private const int MinTimeLimitSec = 30;
    private const int MaxTimeLimitSec = 3600;
    private const int MinPlayerMaxHp = 1;
    private const int MaxPlayerMaxHp = 9999;
    private const float MinPlayerMoveSpeed = 0.5f;
    private const float MaxPlayerMoveSpeed = 20f;
    private const float MinInvincibleSec = 0f;
    private const float MaxInvincibleSec = 5f;
    private const float MinExpMultiplier = 0.1f;
    private const float MaxExpMultiplier = 10f;

    private GameSettingsData _settingsData = new();
    private IntegerField _timeLimitField;
    private IntegerField _playerMaxHpField;
    private FloatField _playerMoveSpeedField;
    private FloatField _invincibleSecField;
    private FloatField _expMultiplierField;
    private bool _isUiBuilt;
    private bool _isBinding;

    private void OnEnable()
    {
        BuildUi();
        Load();
        RefreshFields();
        EventBus.Subscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Subscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SaveAllRequestedEvent>(OnSaveAllRequested);
        EventBus.Unsubscribe<LoadAllRequestedEvent>(OnLoadAllRequested);
    }

    private void BuildUi()
    {
        if (_isUiBuilt) return;

        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[GameSettingsEditor] UIDocument root not found.");
            return;
        }

        root.Clear();
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;
        root.style.paddingTop = 12f;
        root.style.paddingBottom = 12f;
        root.style.flexDirection = FlexDirection.Column;

        var title = new Label("基本設定");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8f;
        root.Add(title);

        _timeLimitField = new IntegerField("制限時間 (秒)");
        _timeLimitField.tooltip = $"{MinTimeLimitSec}〜{MaxTimeLimitSec}";
        _playerMaxHpField = new IntegerField("プレイヤー最大HP");
        _playerMaxHpField.tooltip = $"{MinPlayerMaxHp}〜{MaxPlayerMaxHp}";
        _playerMoveSpeedField = new FloatField("プレイヤー移動速度");
        _playerMoveSpeedField.tooltip = $"{MinPlayerMoveSpeed:0.0}〜{MaxPlayerMoveSpeed:0}";
        _invincibleSecField = new FloatField("無敵時間 (秒)");
        _invincibleSecField.tooltip = $"{MinInvincibleSec:0.0}〜{MaxInvincibleSec:0.0}";
        _expMultiplierField = new FloatField("経験値倍率");
        _expMultiplierField.tooltip = $"{MinExpMultiplier:0.0}〜{MaxExpMultiplier:0}";

        root.Add(_timeLimitField);
        root.Add(_playerMaxHpField);
        root.Add(_playerMoveSpeedField);
        root.Add(_invincibleSecField);
        root.Add(_expMultiplierField);

        var applyButton = new Button(ApplyToRuntime) { text = "適用" };
        applyButton.style.marginTop = 8f;
        root.Add(applyButton);

        RegisterCallbacks();
        _isUiBuilt = true;
    }

    private void RegisterCallbacks()
    {
        _timeLimitField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.TimeLimitSec = Mathf.Clamp(evt.newValue, MinTimeLimitSec, MaxTimeLimitSec);
            if (_settingsData.TimeLimitSec != evt.newValue)
                _timeLimitField.SetValueWithoutNotify(_settingsData.TimeLimitSec);
        });

        _playerMaxHpField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.PlayerMaxHp = Mathf.Clamp(evt.newValue, MinPlayerMaxHp, MaxPlayerMaxHp);
            if (_settingsData.PlayerMaxHp != evt.newValue)
                _playerMaxHpField.SetValueWithoutNotify(_settingsData.PlayerMaxHp);
        });

        _playerMoveSpeedField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.PlayerMoveSpeed = Mathf.Clamp(evt.newValue, MinPlayerMoveSpeed, MaxPlayerMoveSpeed);
            if (!Mathf.Approximately(_settingsData.PlayerMoveSpeed, evt.newValue))
                _playerMoveSpeedField.SetValueWithoutNotify(_settingsData.PlayerMoveSpeed);
        });

        _invincibleSecField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.InvincibleSec = Mathf.Clamp(evt.newValue, MinInvincibleSec, MaxInvincibleSec);
            if (!Mathf.Approximately(_settingsData.InvincibleSec, evt.newValue))
                _invincibleSecField.SetValueWithoutNotify(_settingsData.InvincibleSec);
        });

        _expMultiplierField?.RegisterValueChangedCallback(evt =>
        {
            if (_isBinding) return;
            _settingsData.ExpMultiplier = Mathf.Clamp(evt.newValue, MinExpMultiplier, MaxExpMultiplier);
            if (!Mathf.Approximately(_settingsData.ExpMultiplier, evt.newValue))
                _expMultiplierField.SetValueWithoutNotify(_settingsData.ExpMultiplier);
        });
    }

    private void ApplyToRuntime()
    {
        var playerController = FindAnyObjectByType<PlayerController>();
        var gameManager = FindAnyObjectByType<GameManager>();

        playerController?.ApplyGameSettings(
            _settingsData.PlayerMaxHp,
            _settingsData.PlayerMoveSpeed,
            _settingsData.InvincibleSec,
            _settingsData.ExpMultiplier);
        gameManager?.ApplyTimeLimitSec(_settingsData.TimeLimitSec);

        if (playerController == null)
            Debug.LogWarning("[GameSettingsEditor] PlayerController not found.");
        if (gameManager == null)
            Debug.LogWarning("[GameSettingsEditor] GameManager not found.");
    }

    private void Save()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[GameSettingsEditor] DataManager.Instance is null.");
            return;
        }

        DataManager.Instance.Save(_settingsData, GameSettingsFileName);
    }

    private void Load()
    {
        _settingsData = DataManager.Instance != null
            ? DataManager.Instance.Load<GameSettingsData>(GameSettingsFileName)
            : new GameSettingsData();

        _settingsData ??= new GameSettingsData();
        _settingsData.TimeLimitSec = Mathf.Clamp(_settingsData.TimeLimitSec, MinTimeLimitSec, MaxTimeLimitSec);
        _settingsData.PlayerMaxHp = Mathf.Clamp(_settingsData.PlayerMaxHp, MinPlayerMaxHp, MaxPlayerMaxHp);
        _settingsData.PlayerMoveSpeed = Mathf.Clamp(_settingsData.PlayerMoveSpeed, MinPlayerMoveSpeed, MaxPlayerMoveSpeed);
        _settingsData.InvincibleSec = Mathf.Clamp(_settingsData.InvincibleSec, MinInvincibleSec, MaxInvincibleSec);
        _settingsData.ExpMultiplier = Mathf.Clamp(_settingsData.ExpMultiplier, MinExpMultiplier, MaxExpMultiplier);
    }

    private void RefreshFields()
    {
        _isBinding = true;
        _timeLimitField?.SetValueWithoutNotify(_settingsData.TimeLimitSec);
        _playerMaxHpField?.SetValueWithoutNotify(_settingsData.PlayerMaxHp);
        _playerMoveSpeedField?.SetValueWithoutNotify(_settingsData.PlayerMoveSpeed);
        _invincibleSecField?.SetValueWithoutNotify(_settingsData.InvincibleSec);
        _expMultiplierField?.SetValueWithoutNotify(_settingsData.ExpMultiplier);
        _isBinding = false;
    }

    private void OnSaveAllRequested(SaveAllRequestedEvent _)
    {
        Save();
    }

    private void OnLoadAllRequested(LoadAllRequestedEvent _)
    {
        Load();
        RefreshFields();
    }
}
