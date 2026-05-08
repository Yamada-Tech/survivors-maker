using System.Collections.Generic;
using UnityEngine;

public class LevelUpUI : MonoBehaviour
{
    [SerializeField] private WeaponSystem _weaponSystem;

    private bool _isShowing;
    private List<WeaponData> _choices = new();

    private GUIStyle _titleStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInitialized;
    private Texture2D _buttonNormalTexture;
    private Texture2D _buttonHoverTexture;
    private Texture2D _buttonActiveTexture;

    private static readonly WeaponData[] WeaponPool =
    {
        new() { Id = "sword", Name = "⚔ 剣", Type = WeaponType.Melee, Damage = 20, Cooldown = 0.7f, Range = 1.8f, Description = "近くの敵全員を斬る。射程短め。" },
        new() { Id = "bow", Name = "🏹 弓", Type = WeaponType.Projectile, Damage = 15, Cooldown = 0.9f, Range = 8f, ProjectileSpeed = 10f, Description = "最も近い敵に矢を放つ。" },
        new() { Id = "blast", Name = "💥 爆発", Type = WeaponType.Area, Damage = 25, Cooldown = 2f, Range = 2.5f, Description = "自分の周囲の敵を吹き飛ばす。" },
        new() { Id = "dagger", Name = "🗡 短剣", Type = WeaponType.Projectile, Damage = 10, Cooldown = 0.3f, Range = 5f, ProjectileSpeed = 14f, Description = "高速で短剣を連射する。" },
        new() { Id = "hammer", Name = "🔨 ハンマー", Type = WeaponType.Melee, Damage = 40, Cooldown = 1.5f, Range = 1.2f, Description = "遅いが超威力の一撃。" },
        new() { Id = "nova", Name = "✨ 光波", Type = WeaponType.Area, Damage = 12, Cooldown = 0.8f, Range = 4f, Description = "広範囲に光の波を放つ。" },
    };

    private void OnEnable()
    {
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Subscribe<AppStateChangedEvent>(OnAppStateChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Unsubscribe<AppStateChangedEvent>(OnAppStateChanged);
    }

    private void OnDestroy()
    {
        DestroyTexture(ref _buttonNormalTexture);
        DestroyTexture(ref _buttonHoverTexture);
        DestroyTexture(ref _buttonActiveTexture);
    }

    private void Start()
    {
        if (_weaponSystem == null)
            _weaponSystem = FindAnyObjectByType<WeaponSystem>();
    }

    private void OnLevelUp(LevelUpEvent evt)
    {
        _choices = PickRandomChoices(3);
        _isShowing = _choices.Count > 0;

        if (_isShowing)
        {
            if (AppStateMachine.Instance != null)
                AppStateMachine.Instance.ChangeState(AppState.Pause);
            else
                Time.timeScale = 0f;
        }
    }

    private void OnAppStateChanged(AppStateChangedEvent evt)
    {
        if (evt.NewState == AppState.Editor)
        {
            _choices.Clear();
            _isShowing = false;
        }
    }

    private void OnGUI()
    {
        if (!_isShowing)
            return;

        InitStyles();

        const float panelW = 700f;
        const float panelH = 320f;
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(px, py + 10f, panelW, 50f), "⭐ LEVEL UP！ 武器を選んでください", _titleStyle);

        const float btnW = 200f;
        const float btnH = 200f;
        float gap = (panelW - btnW * _choices.Count) / (_choices.Count + 1);

        for (int i = 0; i < _choices.Count; i++)
        {
            float bx = px + gap + i * (btnW + gap);
            float by = py + 70f;

            var weapon = _choices[i];
            string actionLabel = _weaponSystem != null && _weaponSystem.HasWeapon(weapon.Id) ? "強化" : "新規";
            string label =
                $"{weapon.Name} [{actionLabel}]\n\n<size=13>{weapon.Description}</size>\n\n<size=12>ダメージ: {weapon.Damage}\nクールダウン: {weapon.Cooldown:F1}秒</size>";

            if (GUI.Button(new Rect(bx, by, btnW, btnH), label, _buttonStyle))
                SelectWeapon(weapon);
        }
    }

    private void SelectWeapon(WeaponData data)
    {
        _weaponSystem?.EquipWeapon(data);
        _choices.Clear();
        _isShowing = false;

        if (AppStateMachine.Instance != null)
            AppStateMachine.Instance.ChangeState(AppState.Play);
        else
            Time.timeScale = 1f;
    }

    private static List<WeaponData> PickRandomChoices(int count)
    {
        var pool = new List<WeaponData>(WeaponPool);
        var result = new List<WeaponData>();
        count = Mathf.Min(count, pool.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    private void InitStyles()
    {
        if (_stylesInitialized)
            return;

        _stylesInitialized = true;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.3f) }
        };

        _buttonNormalTexture = MakeTex(new Color(0.2f, 0.25f, 0.4f));
        _buttonHoverTexture = MakeTex(new Color(0.3f, 0.4f, 0.65f));
        _buttonActiveTexture = MakeTex(new Color(0.4f, 0.55f, 0.85f));

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true,
            normal = { textColor = Color.white, background = _buttonNormalTexture },
            hover = { textColor = Color.white, background = _buttonHoverTexture },
            active = { textColor = Color.white, background = _buttonActiveTexture },
        };
        _buttonStyle.padding = new RectOffset(10, 10, 10, 10);
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture == null)
            return;

        Destroy(texture);
        texture = null;
    }
}
