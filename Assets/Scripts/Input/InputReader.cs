using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cybershi
{
    /// <summary>
    /// Единая точка чтения ввода. Вся игра обращается только сюда (InputReader.Instance.*),
    /// поэтому смена бэкенда ввода не задевает игровую логику.
    ///
    /// По умолчанию используется НОВАЯ Input System (опрос устройств Keyboard/Mouse/Gamepad).
    /// Если пакет com.unity.inputsystem не установлен — автоматически работает классический
    /// UnityEngine.Input (ветка #else), так что проект компилируется в любом случае.
    ///
    /// Раскладка:
    ///   Move           — WASD / стрелки / левый стик
    ///   Pointer        — позиция курсора (экран)
    ///   Fire           — ЛКМ / правый триггер
    ///   Jump           — Space / южная кнопка геймпада
    ///   Dash           — Shift / западная кнопка
    ///   Slide/Slam     — Ctrl / восточная кнопка
    ///   Pause          — Esc / Start
    ///   Оружие         — 1..9 / колесо мыши / бамперы
    ///
    /// Объект создаётся сам (DontDestroyOnLoad) при первом обращении — отдельно ставить не нужно.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        private static InputReader _instance;

        public static InputReader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("InputReader (auto)");
                    _instance = go.AddComponent<InputReader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ----------------------------------------------------------------- движение / прицел

        public Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 v = Vector2.zero;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    Vector2 s = g.leftStick.ReadValue();
                    if (s.sqrMagnitude > v.sqrMagnitude) v = s;
                }
                return Vector2.ClampMagnitude(v, 1f);
#else
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            }
        }

        /// <summary>Позиция указателя в экранных координатах (для прицеливания мышью).</summary>
        public Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) return Mouse.current.position.ReadValue();
                if (Pointer.current != null) return Pointer.current.position.ReadValue();
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
                return Input.mousePosition;
#endif
            }
        }

        // ----------------------------------------------------------------- кнопки

        public bool FireHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                bool m = Mouse.current != null && Mouse.current.leftButton.isPressed;
                bool g = Gamepad.current != null && Gamepad.current.rightTrigger.ReadValue() > 0.5f;
                return m || g;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        public bool FirePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                bool m = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool g = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
                return m || g;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>Альтернативный огонь: ПКМ / левый триггер.</summary>
        public bool AltFirePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                bool m = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
                bool g = Gamepad.current != null && Gamepad.current.leftTrigger.wasPressedThisFrame;
                return m || g;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        public bool AltFireHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                bool m = Mouse.current != null && Mouse.current.rightButton.isPressed;
                bool g = Gamepad.current != null && Gamepad.current.leftTrigger.ReadValue() > 0.5f;
                return m || g;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

        public bool JumpPressed => Pressed(KeyNew.Jump, KeyCode.Space);
        public bool JumpHeld => Held(KeyNew.Jump, KeyCode.Space);
        public bool DashPressed => Pressed(KeyNew.Dash, KeyCode.LeftShift);
        public bool SlideSlamPressed => Pressed(KeyNew.SlideSlam, KeyCode.LeftControl);
        public bool SlideSlamHeld => Held(KeyNew.SlideSlam, KeyCode.LeftControl);
        public bool PausePressed => Pressed(KeyNew.Pause, KeyCode.Escape);

        /// <summary>Прокрутка/бамперы для смены оружия. Возвращает -1 / 0 / +1.</summary>
        public int WeaponCycle
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                if (scroll > 0.01f) return 1;
                if (scroll < -0.01f) return -1;
                var g = Gamepad.current;
                if (g != null)
                {
                    if (g.rightShoulder.wasPressedThisFrame) return 1;
                    if (g.leftShoulder.wasPressedThisFrame) return -1;
                }
                return 0;
#else
                float scroll = Input.mouseScrollDelta.y;
                if (scroll > 0.01f) return 1;
                if (scroll < -0.01f) return -1;
                return 0;
#endif
            }
        }

        /// <summary>Нажата ли клавиша слота оружия с индексом slot (0 = «1», 8 = «9»).</summary>
        public bool WeaponSlotPressed(int slot)
        {
            if (slot < 0 || slot > 8) return false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            Key key = (Key)((int)Key.Digit1 + slot);
            return k[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha1 + slot);
#endif
        }

        // ----------------------------------------------------------------- внутреннее

#if ENABLE_INPUT_SYSTEM
        private enum KeyNew { Jump, Dash, SlideSlam, Pause }

        private static bool Pressed(KeyNew action, KeyCode _)
        {
            var k = Keyboard.current; var g = Gamepad.current;
            switch (action)
            {
                case KeyNew.Jump: return (k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
                case KeyNew.Dash: return (k != null && k.leftShiftKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame);
                case KeyNew.SlideSlam: return (k != null && k.leftCtrlKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
                case KeyNew.Pause: return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame);
            }
            return false;
        }

        private static bool Held(KeyNew action, KeyCode _)
        {
            var k = Keyboard.current; var g = Gamepad.current;
            switch (action)
            {
                case KeyNew.Jump: return (k != null && k.spaceKey.isPressed) || (g != null && g.buttonSouth.isPressed);
                case KeyNew.Dash: return (k != null && k.leftShiftKey.isPressed) || (g != null && g.buttonWest.isPressed);
                case KeyNew.SlideSlam: return (k != null && k.leftCtrlKey.isPressed) || (g != null && g.buttonEast.isPressed);
            }
            return false;
        }
#else
        private enum KeyNew { Jump, Dash, SlideSlam, Pause }
        private static bool Pressed(KeyNew _, KeyCode code) => Input.GetKeyDown(code);
        private static bool Held(KeyNew _, KeyCode code) => Input.GetKey(code);
#endif
    }
}
