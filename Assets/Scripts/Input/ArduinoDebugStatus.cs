using UnityEngine;
using UnityEngine.InputSystem;

namespace LastShift.Input
{
    /// <summary>
    /// Optional on-screen controller status, hidden by default (enable in
    /// ArduinoSerialSettings or toggle with F9 at runtime). Debug aid only —
    /// gameplay never depends on it.
    /// </summary>
    public sealed class ArduinoDebugStatus : MonoBehaviour
    {
        bool visible;
        bool initialized;

        void Update()
        {
            if (!initialized && ArduinoInputBridge.Instance != null)
            {
                visible = ArduinoInputBridge.Instance.Settings.debugOverlay;
                initialized = true;
            }

            var f9 = Keyboard.current?.f9Key;
            if (f9 != null && f9.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            var bridge = ArduinoInputBridge.Instance;
            if (bridge == null || bridge.Reader == null || bridge.Parser == null) return;

            var reader = bridge.Reader;
            var parser = bridge.Parser;

            string text = reader.IsConnected
                ? "ARDUINO: ПОДКЛЮЧЕН\nПорт: " + reader.ConnectedPort + " @ " + reader.BaudRate
                : "ARDUINO: НЕ НАЙДЕН — КЛАВИАТУРА АКТИВНА";
            text += "\nПоследнее сообщение: " + parser.LastValidLine
                  + "\nДжойстик: " + parser.Stick + " (X=" + parser.JoyX + " Y=" + parser.JoyY + ")"
                  + "\nКнопка джойстика: " + (parser.JoystickButtonHeld ? "НАЖАТА" : "—")
                  + "\nВнешняя кнопка: " + (parser.ExternalButtonHeld ? "НАЖАТА" : "—")
                  + "\nОтброшено строк: " + parser.InvalidLineCount
                  + (parser.ObsoleteLineCount > 0 ? " (устаревших POT: " + parser.ObsoleteLineCount + ")" : "");
            if (!reader.IsConnected && !string.IsNullOrEmpty(reader.LastError))
                text += "\nОшибка: " + reader.LastError;

            GUI.Box(new Rect(8, 8, 420, 136), "");
            GUI.Label(new Rect(16, 12, 408, 130), text);
        }
    }
}
