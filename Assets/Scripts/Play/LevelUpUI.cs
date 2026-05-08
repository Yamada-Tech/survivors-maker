using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レベルアップ時に武器3択UIを表示し、選択後ゲーム再開する
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [SerializeField] private WeaponSystem _weaponSystem;

    private bool _isShowing;
    private List<WeaponData> _choices = new();
    private GUIStyle _titleStyle;
    private GUIStyle _buttonStyle;
    private bool _stylesInitialized;
    private bool _gameEnded;
    private readonly List<Texture2D> _textures = new();

    private static readonly WeaponData[] WeaponPool = new WeaponData[]
    {
        new WeaponData { Id = "sword", Name = "⚔ 剣", Type = WeaponType.Melee, Damage = 20, Cooldown = 0.7f, Range = 1.8f, Description = "近くの敵全員を斬る。射程短め。" },
        new WeaponData { Id = "bow", Name = "🏹 弓", Type = WeaponType.Projectile, Damage = 15, Cooldown = 0.9f, Range = 8f, ProjectileSpeed = 10f, Description = "最も近い敵に矢を放つ。" },
        new WeaponData { Id = "blast", Name = "💥 爆発", Type = WeaponType.Area, Damage = 25, Cooldown = 2.0f, Range = 2.5f, Description = "自分の周囲の敵を吹き飛ばす。" },
        new WeaponData { Id = "dagger", Name = "🗡 短剣", Type = WeaponType.Projectile, Damage = 10, Cooldown = 0.3f, Range = 5f, ProjectileSpeed = 14f, Description = "高速で短剣を連射する。" },
        new WeaponData { Id = "hammer", Name = "🔨 ハンマー", Type = WeaponType.Melee, Damage = 40, Cooldown = 1.5f, Range = 1.2f, Description = "遅いが超威力の一撃。" },
        new WeaponData { Id = "nova", Name = "✨ 光波", Type = WeaponType.Area, Damage = 12, Cooldown = 0.8f, Range = 4.0f, Description = "広範囲に光の波を放つ。" },
    };

    private void OnEnable()
    {
        _gameEnded = false;
        EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Subscribe<GameOverEvent>(OnGameEnded);
        EventBus.Subscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        EventBus.Unsubscribe<GameOverEvent>(OnGameEnded);
        EventBus.Unsubscribe<TimeLimitReachedEvent>(OnTimeLimitReached);
    }

    private void Start()
    {
        if (_weaponSystem == null)
            _weaponSystem = FindAnyObjectByType<WeaponSystem>();
    }

    private void OnDestroy()
    {
        foreach (var texture in _textures)
        {
            if (texture != null)
                Destroy(texture);
        }

        _textures.Clear();
    }

    private void OnLevelUp(LevelUpEvent evt)
    {
        if (_gameEnded)
            return;

        _choices = PickRandomChoices(3);
        _isShowing = _choices.Count > 0;

        if (_isShowing)
            Time.timeScale = 0f;
    }

    private void OnGameEnded(GameOverEvent evt)
    {
        _gameEnded = true;
        _isShowing = false;
        Time.timeScale = 1f;
    }

    private void OnTimeLimitReached(TimeLimitReachedEvent evt)
    {
        _gameEnded = true;
        _isShowing = false;
        Time.timeScale = 1f;
    }

    private void OnGUI()
    {
        if (!_isShowing)
            return;

        InitStyles();

        float panelWidth = 700f;
        float panelHeight = 320f;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = (Screen.height - panelHeight) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(panelX, panelY + 10f, panelWidth, 50f), "⭐ LEVEL UP！ 武器を選んでください", _titleStyle);

        float buttonWidth = 200f;
        float buttonHeight = 210f;
        float gap = (panelWidth - buttonWidth * _choices.Count) / (_choices.Count + 1);

        for (int i = 0; i < _choices.Count; i++)
        {
            float buttonX = panelX + gap + i * (buttonWidth + gap);
            float buttonY = panelY + 68f;
            var weapon = _choices[i];
            string label = $"{weapon.Name}\n\n<size=13>{weapon.Description}</size>\n\n<size=12>ダメージ: {weapon.Damage}\nCD: {weapon.Cooldown:F1}秒</size>";

            if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), label, _buttonStyle))
                SelectWeapon(weapon);
        }
    }

    private void SelectWeapon(WeaponData data)
    {
        _weaponSystem?.EquipWeapon(data);
        _isShowing = false;
        Time.timeScale = 1f;
    }

    private List<WeaponData> PickRandomChoices(int count)
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

        var normalTexture = MakeTex(new Color(0.20f, 0.25f, 0.40f));
        var hoverTexture = MakeTex(new Color(0.30f, 0.40f, 0.65f));
        var activeTexture = MakeTex(new Color(0.40f, 0.55f, 0.85f));
        _textures.AddRange(new[] { normalTexture, hoverTexture, activeTexture });

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true,
            padding = new RectOffset(10, 10, 10, 10),
            normal = { textColor = Color.white, background = normalTexture },
            hover = { textColor = Color.white, background = hoverTexture },
            active = { textColor = Color.white, background = activeTexture },
        };
    }

    private Texture2D MakeTex(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
