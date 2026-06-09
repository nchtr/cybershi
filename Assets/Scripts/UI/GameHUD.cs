using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cybershi
{
    /// <summary>
    /// Игровой интерфейс (HUD), целиком построенный из кода: полоска здоровья, заряды рывка,
    /// текущее оружие, перекрестие, индикатор боя, счётчик волн и полоска здоровья босса.
    /// Данные тянет из <see cref="PlayerController"/>, <see cref="WeaponController"/>,
    /// <see cref="CombatStateTracker"/> и <see cref="WaveEncounter"/>.
    ///
    /// Это плейсхолдер-HUD — легко заменить на свои спрайты/префабы.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private Canvas _canvas;

        private Image _healthFill;
        private Text _healthText;
        private Text _weaponText;
        private Text _combatText;
        private Text _waveText;
        private RectTransform _crosshair;

        private RectTransform _dashRow;
        private readonly List<Image> _dashPips = new();

        private GameObject _bossGroup;
        private Image _bossFill;
        private Text _bossName;

        private PlayerController _player;
        private Health _playerHealth;
        private WeaponController _weapon;

        private void Start()
        {
            GameSettings.EnsureLoaded();
            GameSettings.Apply();
            BuildUI();
        }

        private void BuildUI()
        {
            _canvas = UIBuilder.CreateCanvas("HUDCanvas", 10);

            // --- Полоска здоровья (сверху слева) ---
            var hpContainer = new GameObject("HealthBar", typeof(RectTransform));
            hpContainer.transform.SetParent(_canvas.transform, false);
            var hpRt = UIBuilder.SetRect(hpContainer.transform.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(420f, 36f));
            _healthFill = UIBuilder.Bar(hpRt, new Color(0.1f, 0.05f, 0.07f, 0.85f), new Color(1f, 0.3f, 0.35f));
            _healthText = UIBuilder.Label(hpContainer.transform, "100 / 100", 22, TextAnchor.MiddleCenter, Color.white);
            UIBuilder.Stretch((RectTransform)_healthText.transform, Vector2.zero, Vector2.one);

            // --- Заряды рывка (под здоровьем) ---
            var dashGo = new GameObject("DashCharges", typeof(RectTransform));
            dashGo.transform.SetParent(_canvas.transform, false);
            _dashRow = UIBuilder.SetRect(dashGo.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -86f), new Vector2(420f, 18f));

            // --- Оружие (снизу слева) ---
            _weaponText = UIBuilder.Label(_canvas.transform, "", 30, TextAnchor.LowerLeft, Color.white);
            UIBuilder.SetRect(_weaponText, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 40f), new Vector2(700f, 44f));

            // --- Индикатор боя (сверху справа) ---
            _combatText = UIBuilder.Label(_canvas.transform, "", 26, TextAnchor.UpperRight, new Color(1f, 0.5f, 0.3f));
            UIBuilder.SetRect(_combatText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(360f, 36f));

            // --- Счётчик волн (сверху по центру) ---
            _waveText = UIBuilder.Label(_canvas.transform, "", 30, TextAnchor.UpperCenter, UIBuilder.Accent);
            UIBuilder.SetRect(_waveText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(800f, 40f));

            // --- Полоска босса (снизу по центру) ---
            _bossGroup = new GameObject("BossBar", typeof(RectTransform));
            _bossGroup.transform.SetParent(_canvas.transform, false);
            UIBuilder.SetRect(_bossGroup.transform.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(900f, 30f));
            _bossName = UIBuilder.Label(_bossGroup.transform, "BOSS", 24, TextAnchor.LowerCenter, new Color(1f, 0.7f, 0.4f));
            UIBuilder.SetRect(_bossName, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(900f, 30f));
            _bossFill = UIBuilder.Bar((RectTransform)_bossGroup.transform.GetComponent<RectTransform>(),
                new Color(0.1f, 0.07f, 0.05f, 0.9f), new Color(1f, 0.55f, 0.2f));
            _bossGroup.SetActive(false);

            // --- Перекрестие ---
            var cross = UIBuilder.Image(_canvas.transform, new Color(1f, 1f, 1f, 0.85f));
            _crosshair = (RectTransform)cross.transform;
            _crosshair.anchorMin = Vector2.zero;
            _crosshair.anchorMax = Vector2.zero;
            _crosshair.pivot = new Vector2(0.5f, 0.5f);
            _crosshair.sizeDelta = new Vector2(14f, 14f);
            cross.sprite = PlaceholderFactory.Circle;
        }

        private void Update()
        {
            ResolveRefs();
            UpdateHealth();
            UpdateDash();
            UpdateWeapon();
            UpdateCombat();
            UpdateWaves();
            UpdateCrosshair();
        }

        private void ResolveRefs()
        {
            if (_player == null) _player = PlayerController.Instance;
            if (_player != null)
            {
                if (_playerHealth == null) _playerHealth = _player.Health;
                if (_weapon == null) _weapon = _player.GetComponent<WeaponController>();
            }
        }

        private void UpdateHealth()
        {
            if (_playerHealth == null) return;
            _healthFill.fillAmount = _playerHealth.Normalized;
            _healthText.text = $"{Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}";
        }

        private void UpdateDash()
        {
            if (_player == null) return;
            int max = Mathf.Max(0, _player.MaxDashCharges);

            // Создаём недостающие «пипсы».
            while (_dashPips.Count < max)
            {
                var pip = UIBuilder.Image(_dashRow, Color.white);
                var rt = (RectTransform)pip.transform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(34f, 12f);
                rt.anchoredPosition = new Vector2(_dashPips.Count * 42f, 0f);
                _dashPips.Add(pip);
            }

            int charges = _player.DashCharges;
            for (int i = 0; i < _dashPips.Count; i++)
            {
                bool ready = i < charges;
                _dashPips[i].color = ready ? UIBuilder.Accent : new Color(0.25f, 0.3f, 0.36f, 0.7f);
                _dashPips[i].gameObject.SetActive(i < max);
            }
        }

        private void UpdateWeapon()
        {
            if (_weapon == null || _weapon.Current == null) { _weaponText.text = ""; return; }
            _weaponText.text = $"[{_weapon.CurrentIndex + 1}/{_weapon.weapons.Count}]  {_weapon.Current.displayName}";
        }

        private void UpdateCombat()
        {
            bool inCombat = CombatStateTracker.Instance != null && CombatStateTracker.Instance.InCombat;
            _combatText.text = inCombat ? "⚔ В БОЮ" : "";
        }

        private void UpdateWaves()
        {
            var enc = WaveEncounter.Active;
            if (enc != null && enc.InProgress)
            {
                _waveText.text = enc.TotalWaves > 0
                    ? $"{enc.EncounterName}  —  Волна {enc.CurrentWaveIndex}/{enc.TotalWaves}  ·  врагов: {enc.AliveCount}"
                    : "";
            }
            else
            {
                _waveText.text = "";
            }

            var boss = enc != null ? enc.CurrentBoss : null;
            if (boss != null && boss.IsAlive)
            {
                _bossGroup.SetActive(true);
                _bossFill.fillAmount = boss.Normalized;
                _bossName.text = enc.BossName;
            }
            else
            {
                _bossGroup.SetActive(false);
            }
        }

        private void UpdateCrosshair()
        {
            if (_crosshair == null || _canvas == null) return;
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _crosshair.anchoredPosition = InputReader.Instance.PointerPosition / scale;
        }
    }
}
