using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Simulates keyboard and mouse input via Windows user32.
    /// Used by the Action Timeline for key/mouse commands.
    /// </summary>
    public static class WindowsInput
    {
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// Simulate pressing a key (down then up). For key combinations, call this for each key in order
        /// (e.g. Ctrl down, K down, K up, Ctrl up can be done with SimulateKey for each with separate down/up).
        /// </summary>
        public static void SimulateKey(byte vk, bool keyUp)
        {
            keybd_event(vk, 0, keyUp ? KEYEVENTF_KEYUP : 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulate a full key press (down + up) for a single virtual key.
        /// </summary>
        public static void SimulateKeyPress(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulate a key combination: all keys down in order, then all keys up in reverse order.
        /// </summary>
        public static void SimulateKeyCombination(byte[] vkCodes)
        {
            if (vkCodes == null || vkCodes.Length == 0) return;
            for (int i = 0; i < vkCodes.Length; i++)
                keybd_event(vkCodes[i], 0, 0, UIntPtr.Zero);
            for (int i = vkCodes.Length - 1; i >= 0; i--)
                keybd_event(vkCodes[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Move cursor to screen position (Windows coordinates: origin top-left).
        /// </summary>
        public static void SetMousePosition(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>
        /// Simulate mouse button at current cursor position. button: 0 = left, 1 = right, 2 = middle.
        /// Call SetMousePosition first if you need a specific position.
        /// </summary>
        public static void SimulateMouseButton(int button, bool buttonUp)
        {
            uint down = 0, up = 0;
            switch (button)
            {
                case 0: down = MOUSEEVENTF_LEFTDOWN; up = MOUSEEVENTF_LEFTUP; break;
                case 1: down = MOUSEEVENTF_RIGHTDOWN; up = MOUSEEVENTF_RIGHTUP; break;
                case 2: down = MOUSEEVENTF_MIDDLEDOWN; up = MOUSEEVENTF_MIDDLEUP; break;
                default: return;
            }
            mouse_event(buttonUp ? up : down, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Move cursor to (x, y) and perform a full click with the given button.
        /// screenX, screenY are in Windows screen coordinates (top-left origin).
        /// </summary>
        public static void SimulateMouseClickAt(int screenX, int screenY, int button)
        {
            SetCursorPos(screenX, screenY);
            SimulateMouseButton(button, false);
            SimulateMouseButton(button, true);
        }

        /// <summary>
        /// Simulate mouse wheel scroll at the current cursor position.
        /// delta: positive = scroll up (away from user), negative = scroll down. One notch = 120.
        /// </summary>
        public static void SimulateScroll(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
        }

        /// <summary>
        /// Convert Unity KeyCode to Windows virtual key code (VK). Returns 0 if no mapping.
        /// </summary>
        public static byte KeyCodeToVk(KeyCode key)
        {
            int k = (int)key;
            if (k >= (int)KeyCode.A && k <= (int)KeyCode.Z)
                return (byte)(k - (int)KeyCode.A + 0x41);
            if (k >= (int)KeyCode.Alpha0 && k <= (int)KeyCode.Alpha9)
                return (byte)(k - (int)KeyCode.Alpha0 + 0x30);
            if (k >= (int)KeyCode.Keypad0 && k <= (int)KeyCode.Keypad9)
                return (byte)(k - (int)KeyCode.Keypad0 + 0x60);
            switch (key)
            {
                case KeyCode.Return: return 0x0D;
                case KeyCode.Escape: return 0x1B;
                case KeyCode.Space: return 0x20;
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return 0x11;
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return 0x10;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return 0x12;
                case KeyCode.Tab: return 0x09;
                case KeyCode.Backspace: return 0x08;
                case KeyCode.F1: return 0x70;
                case KeyCode.F2: return 0x71;
                case KeyCode.F3: return 0x72;
                case KeyCode.F4: return 0x73;
                case KeyCode.F5: return 0x74;
                case KeyCode.F6: return 0x75;
                case KeyCode.F7: return 0x76;
                case KeyCode.F8: return 0x77;
                case KeyCode.F9: return 0x78;
                case KeyCode.F10: return 0x79;
                case KeyCode.F11: return 0x7A;
                case KeyCode.F12: return 0x7B;
                default: return (byte)(k <= 0xFF ? k : 0);
            }
        }

        /// <summary>
        /// Parses multiple key combinations separated by commas (e.g. "Ctrl+A, F5, Escape").
        /// Returns a list of VK arrays and whether every combo parsed successfully.
        /// Empty or whitespace-only segments are skipped. Empty input yields empty list and valid=true.
        /// </summary>
        public static (List<byte[]> combos, bool allValid) ParseMultipleKeyCombos(string? text)
        {
            var combos = new List<byte[]>();
            if (string.IsNullOrWhiteSpace(text)) return (combos, true);
            string[] parts = text!.Split(',');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0) continue;
                byte[]? vks = ParseKeyCombo(trimmed);
                if (vks == null) return (combos, false);
                combos.Add(vks);
            }
            return (combos, true);
        }

        /// <summary>
        /// Returns true only if every non-empty comma-separated key combo in text parses successfully.
        /// </summary>
        public static bool ValidateKeyCombos(string? text)
        {
            var (_, allValid) = ParseMultipleKeyCombos(text);
            return allValid;
        }

        /// <summary>
        /// Parse a key combination string like "Ctrl+A", "Ctrl+Shift+K", "F5", "Escape" into Windows VK codes.
        /// Format: parts separated by '+', case-insensitive. Modifiers: Ctrl, Control, Shift, Alt. Keys: A-Z, 0-9, F1-F12, Enter, Escape, Tab, Space, Backspace.
        /// Arrow: Left, Up, Right, Down. Navigation: Home/Pos1, End, Insert, Delete, PageUp/PgUp, PageDown/PgDown.
        /// Numpad: Numpad0-Numpad9 (or Num0-Num9), Numpad*, Numpad+, Numpad-, Numpad., Numpad/ (and Num* etc.).
        /// Returns null if parsing fails or string is empty.
        /// </summary>
        public static byte[]? ParseKeyCombo(string? combo)
        {
            if (string.IsNullOrWhiteSpace(combo)) return null;
            string s = combo!.Trim();
            var parts = s.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count == 0) return null;
            var vks = new List<byte>();
            foreach (string part in parts)
            {
                byte? vk = ParseSingleKey(part);
                if (vk == null) return null;
                vks.Add(vk.Value);
            }
            return vks.Count > 0 ? vks.ToArray() : null;
        }

        private static byte? ParseSingleKey(string part)
        {
            if (part.Length == 0) return null;
            switch (part.Length)
            {
                case 1:
                    char c = part[0];
                    if (c >= 'A' && c <= 'Z') return (byte)(0x41 + (c - 'A'));
                    if (c >= 'a' && c <= 'z') return (byte)(0x41 + (c - 'a'));
                    if (c >= '0' && c <= '9') return (byte)(0x30 + (c - '0'));
                    return null;
                default:
                    break;
            }
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL": return 0x11;
                case "SHIFT": return 0x10;
                case "ALT": return 0x12;
                case "ENTER":
                case "RETURN": return 0x0D;
                case "ESC":
                case "ESCAPE": return 0x1B;
                case "TAB": return 0x09;
                case "SPACE": return 0x20;
                case "BACKSPACE": return 0x08;
                case "F1": return 0x70;
                case "F2": return 0x71;
                case "F3": return 0x72;
                case "F4": return 0x73;
                case "F5": return 0x74;
                case "F6": return 0x75;
                case "F7": return 0x76;
                case "F8": return 0x77;
                case "F9": return 0x78;
                case "F10": return 0x79;
                case "F11": return 0x7A;
                case "F12": return 0x7B;
                // Numpad 0-9 (VK 0x60-0x69)
                case "NUMPAD0": case "NUM0": return 0x60;
                case "NUMPAD1": case "NUM1": return 0x61;
                case "NUMPAD2": case "NUM2": return 0x62;
                case "NUMPAD3": case "NUM3": return 0x63;
                case "NUMPAD4": case "NUM4": return 0x64;
                case "NUMPAD5": case "NUM5": return 0x65;
                case "NUMPAD6": case "NUM6": return 0x66;
                case "NUMPAD7": case "NUM7": return 0x67;
                case "NUMPAD8": case "NUM8": return 0x68;
                case "NUMPAD9": case "NUM9": return 0x69;
                // Numpad operators
                case "NUMPAD*": case "NUMPADMULTIPLY": case "NUMMULTIPLY": case "NUM*": return 0x6A;
                case "NUMPAD+": case "NUMPADADD": case "NUMADD": case "NUM+": return 0x6B;
                case "NUMPAD-": case "NUMPADSUBTRACT": case "NUMSUBTRACT": case "NUM-": return 0x6D;
                case "NUMPAD.": case "NUMPADDECIMAL": case "NUMDECIMAL": case "NUM.": return 0x6E;
                case "NUMPAD/": case "NUMPADDIVIDE": case "NUMDIVIDE": case "NUM/": return 0x6F;
                // Arrow keys
                case "LEFT": return 0x25;
                case "UP": return 0x26;
                case "RIGHT": return 0x27;
                case "DOWN": return 0x28;
                // Navigation
                case "HOME": case "POS1": return 0x24;
                case "END": return 0x23;
                case "INSERT": return 0x2D;
                case "DELETE": return 0x2E;
                case "PAGEUP": case "PGUP": return 0x21;
                case "PAGEDOWN": case "PGDN": return 0x22;
                default: return null;
            }
        }
    }
}
