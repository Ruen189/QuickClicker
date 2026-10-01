using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuickClicker;

/// <summary>Кнопки мыши, которые умеет эмулировать кликер.</summary>
internal enum MouseButtonKind
{
    Left,
    Right,
    Middle,
    X1,
    X2,
}

/// <summary>Обёртки над user32.dll: низкоуровневые хуки клавиатуры/мыши и SendInput.</summary>
internal static class Native
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    // Флаги низкоуровневого хука: событие инжектировано через SendInput.
    public const uint LLKHF_INJECTED = 0x00000010;
    public const uint LLMHF_INJECTED = 0x00000001;

    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt

    public const uint INPUT_MOUSE = 0;

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_XDOWN = 0x0080;
    public const uint MOUSEEVENTF_XUP = 0x0100;

    public const uint XBUTTON1 = 0x0001;
    public const uint XBUTTON2 = 0x0002;

    /// <summary>
    /// Магическая метка в dwExtraInfo, по которой хуки отличают наши синтетические
    /// клики от нажатий живого пользователя.
    /// </summary>
    public static readonly IntPtr ClickerSignature = unchecked((IntPtr)0x5143_4C4B); // "QCLK"

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    /// <summary>Клавиша сейчас физически зажата.</summary>
    public static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>Отправляет клик выбранной кнопкой мыши (нажатие + отпускание).</summary>
    /// <param name="holdMs">
    /// Сколько миллисекунд держать кнопку зажатой между DOWN и UP.
    /// 0 — мгновенный клик (down+up одним пакетом); &gt;0 — реальное зажатие,
    /// которое нужно части игр, чтобы клик вообще распознался.
    /// </param>
    /// <param name="keepGoing">
    /// Проверка «кликер ещё работает». Пока зажатие идёт, кнопка удерживается
    /// мелкими порциями, и при остановке она отпускается мгновенно.
    /// </param>
    public static void SendMouseClick(MouseButtonKind button, int holdMs = 0, Func<bool>? keepGoing = null)
    {
        uint down, up, data = 0;
        switch (button)
        {
            case MouseButtonKind.Right:
                down = MOUSEEVENTF_RIGHTDOWN; up = MOUSEEVENTF_RIGHTUP; break;
            case MouseButtonKind.Middle:
                down = MOUSEEVENTF_MIDDLEDOWN; up = MOUSEEVENTF_MIDDLEUP; break;
            case MouseButtonKind.X1:
                down = MOUSEEVENTF_XDOWN; up = MOUSEEVENTF_XUP; data = XBUTTON1; break;
            case MouseButtonKind.X2:
                down = MOUSEEVENTF_XDOWN; up = MOUSEEVENTF_XUP; data = XBUTTON2; break;
            default:
                down = MOUSEEVENTF_LEFTDOWN; up = MOUSEEVENTF_LEFTUP; break;
        }

        if (holdMs <= 0)
        {
            // Быстрый путь: down и up в одном вызове SendInput.
            INPUT[] both = { MakeMouseInput(down, data), MakeMouseInput(up, data) };
            SendInput((uint)both.Length, both, Marshal.SizeOf<INPUT>());
            return;
        }

        // Медленный путь: держим кнопку зажатой holdMs миллисекунд.
        SendMouseInput(down, data);
        Hold(holdMs, keepGoing);
        SendMouseInput(up, data);
    }

    private static void SendMouseInput(uint flags, uint mouseData)
    {
        INPUT[] inputs = { MakeMouseInput(flags, mouseData) };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Удерживает кнопку зажатой, но спит мелкими порциями: если кликер остановили,
    /// зажатие прерывается сразу и кнопка не «залипает».
    /// </summary>
    private static void Hold(int holdMs, Func<bool>? keepGoing)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < holdMs)
        {
            if (keepGoing is not null && !keepGoing()) return;
            Thread.Sleep(5);
        }
    }


    private static INPUT MakeMouseInput(uint flags, uint mouseData) => new()
    {
        type = INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dx = 0,
                dy = 0,
                mouseData = mouseData,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = ClickerSignature,
            },
        },
    };

    // ===================== Клик через WM-сообщения =====================
    // Некоторые игры (в т.ч. отдельные Unity-проекты) игнорируют инжектированный
    // ввод, но реагируют на обычные оконные сообщения WM_*. Это запасной метод.

    public const uint MK_LBUTTON = 0x0001;
    public const uint MK_RBUTTON = 0x0002;
    public const uint MK_MBUTTON = 0x0010;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT p);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Клик доставкой WM-сообщений активному окну.</summary>
    /// <param name="holdMs">Сколько миллисекунд держать кнопку зажатой между DOWN и UP.</param>
    /// <param name="keepGoing">Проверка «кликер ещё работает» (см. SendMouseClick).</param>
    public static void PostMouseClick(MouseButtonKind button, int holdMs = 0, Func<bool>? keepGoing = null)
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        if (!GetCursorPos(out POINT pt)) return;
        ScreenToClient(hwnd, ref pt);
        IntPtr lParam = (IntPtr)((pt.Y << 16) | (pt.X & 0xFFFF));

        uint downMsg, upMsg;
        uint downW, upW;
        switch (button)
        {
            case MouseButtonKind.Right:
                downMsg = WM_RBUTTONDOWN; upMsg = WM_RBUTTONUP; downW = MK_RBUTTON; upW = 0; break;
            case MouseButtonKind.Middle:
                downMsg = WM_MBUTTONDOWN; upMsg = WM_MBUTTONUP; downW = MK_MBUTTON; upW = 0; break;
            case MouseButtonKind.X1:
                downMsg = WM_XBUTTONDOWN; upMsg = WM_XBUTTONUP; downW = XBUTTON1 << 16; upW = XBUTTON1 << 16; break;
            case MouseButtonKind.X2:
                downMsg = WM_XBUTTONDOWN; upMsg = WM_XBUTTONUP; downW = XBUTTON2 << 16; upW = XBUTTON2 << 16; break;
            default:
                downMsg = WM_LBUTTONDOWN; upMsg = WM_LBUTTONUP; downW = MK_LBUTTON; upW = 0; break;
        }

        PostMessage(hwnd, downMsg, (IntPtr)downW, lParam);
        if (holdMs > 0) Hold(holdMs, keepGoing);
        PostMessage(hwnd, upMsg, (IntPtr)upW, lParam);
    }
}

/// <summary>Способ, которым кликер отправляет нажатия.</summary>
internal enum ClickMethod
{
    /// <summary>SendInput — системная инъекция ввода (по умолчанию).</summary>
    SendInput,

    /// <summary>WM-сообщения активному окну — запасной вариант для проблемных игр.</summary>
    WindowMessages,
}
