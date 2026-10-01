using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuickClicker;

public sealed partial class MainForm
{
    // ================= ХУКИ =================

    private void InstallHooks()
    {
        _kbProc = KeyboardProc;
        _mouseProc = MouseProc;

        IntPtr module = Native.GetModuleHandle(null);
        _kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbProc, module, 0);
        _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, module, 0);
    }

    private void UninstallHooks()
    {
        if (_kbHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)data.vkCode;
            bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
            bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

            if (down)
            {
                bool fresh = _keysDown.Add(vk);
                if (fresh)
                {
                    // 1) Захват новой клавиши-переключателя.
                    if (_capturing)
                    {
                        BeginInvoke(() => ApplyCapturedKey(vk));
                        return 1; // не пропускаем нажатую клавишу дальше
                    }

                    // 2) Срабатывание хоткея.
                    if (HotkeyMatches(vk))
                    {
                        BeginInvoke(ToggleClicker);
                        if (_blockToggle) return 1;
                    }
                }
            }
            else if (up)
            {
                _keysDown.Remove(vk);
                if (HotkeyMatches(vk) && _blockToggle) return 1;
            }
        }

        return Native.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);

            bool injected = (data.flags & Native.LLMHF_INJECTED) != 0
                            || data.dwExtraInfo == Native.ClickerSignature;

            if (!injected)
            {
                MouseButtonKind? btn = MouseButtonForMessage(msg, data);

                if (btn is MouseButtonKind pressed)
                {
                    // 1) Захват кнопки мыши в качестве клавиши-переключателя.
                    if (_capturing)
                    {
                        _capturing = false;
                        BeginInvoke(() => ApplyCapturedMouseButton(pressed));
                        return 1; // не пропускаем нажатую кнопку дальше
                    }

                    // 2) Тест ручного клика.
                    BeginInvoke(() => RecordUserClick(pressed));

                    // 3) Срабатывание хоткея, повешенного на кнопку мыши.
                    if (_toggleMouseButton == pressed)
                    {
                        BeginInvoke(ToggleClicker);
                        if (_blockToggle) return 1;
                    }
                }
            }
        }

        return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ================= ХОТКЕЙ =================

    private bool HotkeyMatches(int vk)
    {
        // Переключатель ровно один: если он повешен на кнопку мыши,
        // клавиатурный хоткей не срабатывает (иначе работали бы оба сразу).
        if (_toggleMouseButton is not null) return false;

        if (vk != _toggleVk) return false;

        bool ctrl = _toggleVk != Native.VK_CONTROL && Native.IsKeyDown(Native.VK_CONTROL);
        bool alt = _toggleVk != Native.VK_MENU && Native.IsKeyDown(Native.VK_MENU);
        bool shift = _toggleVk != Native.VK_SHIFT && Native.IsKeyDown(Native.VK_SHIFT);

        return ctrl == _toggleCtrl && alt == _toggleAlt && shift == _toggleShift;
    }

    private void BeginCapture()
    {
        _capturing = true;
        _btnHotkey.Text = "Нажми клавишу или кнопку мыши…";
    }

    private void ApplyCapturedKey(int vk)
    {
        _capturing = false;
        _toggleMouseButton = null;
        _toggleVk = vk;

        // Если сама зажатая клавиша — модификатор, её не считаем за модификатор.
        _toggleCtrl = vk != Native.VK_CONTROL && Native.IsKeyDown(Native.VK_CONTROL);
        _toggleAlt = vk != Native.VK_MENU && Native.IsKeyDown(Native.VK_MENU);
        _toggleShift = vk != Native.VK_SHIFT && Native.IsKeyDown(Native.VK_SHIFT);

        UpdateHotkeyText();
        SaveSettings();
    }

    private void UpdateHotkeyText()
    {
        if (_toggleMouseButton is MouseButtonKind mb)
        {
            _btnHotkey.Text = MouseButtonName(mb);
            return;
        }

        var parts = new List<string>();
        if (_toggleCtrl) parts.Add("Ctrl");
        if (_toggleAlt) parts.Add("Alt");
        if (_toggleShift) parts.Add("Shift");
        parts.Add(FormatKey(_toggleVk));
        _btnHotkey.Text = string.Join(" + ", parts);
    }

    private void ApplyCapturedMouseButton(MouseButtonKind button)
    {
        _capturing = false;
        _toggleMouseButton = button;
        _toggleCtrl = _toggleAlt = _toggleShift = false; // модификаторы к кнопке мыши не применяются
        UpdateHotkeyText();
        SaveSettings();
    }

    private static string MouseButtonName(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => "ЛКМ",
        MouseButtonKind.Right => "ПКМ",
        MouseButtonKind.Middle => "СКМ",
        MouseButtonKind.X1 => "Мышь X1 (боковая)",
        MouseButtonKind.X2 => "Мышь X2 (боковая)",
        _ => button.ToString(),
    };

    /// <summary>Определяет кнопку мыши по сообщению хука (только для нажатий).</summary>
    private static MouseButtonKind? MouseButtonForMessage(int msg, Native.MSLLHOOKSTRUCT data) => msg switch
    {
        Native.WM_LBUTTONDOWN => MouseButtonKind.Left,
        Native.WM_RBUTTONDOWN => MouseButtonKind.Right,
        Native.WM_MBUTTONDOWN => MouseButtonKind.Middle,
        Native.WM_XBUTTONDOWN => ((data.mouseData >> 16) & 0xFFFF) == Native.XBUTTON1
            ? MouseButtonKind.X1 : MouseButtonKind.X2,
        _ => null,
    };

    private static string FormatKey(int vk)
    {
        if (vk >= (int)Keys.A && vk <= (int)Keys.Z) return ((char)vk).ToString();
        if (vk >= (int)Keys.D0 && vk <= (int)Keys.D9) return ((char)('0' + (vk - (int)Keys.D0))).ToString();
        if (vk >= (int)Keys.F1 && vk <= (int)Keys.F24) return "F" + (vk - (int)Keys.F1 + 1);

        return vk switch
        {
            (int)Keys.Space => "Space",
            (int)Keys.Return => "Enter",
            (int)Keys.Escape => "Esc",
            (int)Keys.Tab => "Tab",
            (int)Keys.Back => "Backspace",
            (int)Keys.Oemtilde => "`",
            (int)Keys.OemMinus => "-",
            (int)Keys.Oemplus => "=",
            (int)Keys.OemOpenBrackets => "[",
            (int)Keys.OemCloseBrackets => "]",
            (int)Keys.OemPipe => "\\",
            (int)Keys.OemSemicolon => ";",
            (int)Keys.OemQuotes => "'",
            (int)Keys.Oemcomma => ",",
            (int)Keys.OemPeriod => ".",
            (int)Keys.OemQuestion => "/",
            _ => ((Keys)vk).ToString(),
        };
    }
}
