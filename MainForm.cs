namespace QuickClicker;

public sealed partial class MainForm : Form
{
    // ---- состояние кликера ----
    private volatile bool _running;
    private Thread? _clickThread;
    private int _intervalMs = 10;
    private int _holdMs = 10; // сколько мс кнопка держится зажатой (down … up)
    private MouseButtonKind _clickButton = MouseButtonKind.Left;
    private ClickMethod _clickMethod = ClickMethod.SendInput;

    // ---- периодическая пауза (опционально) ----
    private bool _pauseEnabled;
    private int _workSeconds = 30; // сколько секунд кликаем
    private int _restSeconds = 10; // сколько секунд «отдыхаем»

    // ---- хоткей вкл/выкл ----
    private int _toggleVk = (int)Keys.F6;              // используется, когда переключатель на клавиатуре
    private MouseButtonKind? _toggleMouseButton;       // если задано — переключатель повешен на кнопку мыши
    private bool _toggleCtrl;
    private bool _toggleAlt;
    private bool _toggleShift;
    private bool _blockToggle;
    private bool _capturing; // идёт захват новой клавиши-переключателя

    // ---- хуки ----
    private IntPtr _kbHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private Native.HookProc? _kbProc;   // держим ссылки, иначе GC соберёт делегаты
    private Native.HookProc? _mouseProc;
    private readonly HashSet<int> _keysDown = new();

    // ---- тест ручного клика ----
    private bool _testing;
    private int _userCount;
    private double _userSumMs;
    private double _userMinMs = double.MaxValue;
    private double _userLastMs;
    private long _lastUserClickTicks;

    // ---- контролы ----
    private ComboBox _cmbButton = null!;
    private ComboBox _cmbMethod = null!;
    private NumericUpDown _numInterval = null!;
    private NumericUpDown _numHold = null!;
    private CheckBox _chkPause = null!;
    private NumericUpDown _numWork = null!;
    private NumericUpDown _numRest = null!;
    private Button _btnHotkey = null!;
    private CheckBox _chkBlock = null!;
    private Button _btnStart = null!;
    private Label _lblStatus = null!;
    private Button _btnTest = null!;
    private Button _btnResetTest = null!;
    private Label _lblCount = null!;
    private Label _lblLast = null!;
    private Label _lblAvg = null!;
    private Label _lblMin = null!;
    private Label _lblCps = null!;
    private readonly ToolTip _tip = new();
    private readonly Func<bool> _isRunning; // «кликер ещё работает» для прерываемого зажатия

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickClicker", "settings.json");

    public MainForm()
    {
        Text = "QuickClicker";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(388, 602);
        Font = new Font("Segoe UI", 9f);
        _isRunning = () => _running;

        BuildUi();
        LoadSettings();
        UpdateHotkeyText();
        InstallHooks();
    }

    private void BuildUi()
    {
        BuildClickerGroup();
        BuildTestGroup();
    }

    /// <summary>
    /// Глушим Enter над полями числовых настроек. У формы нет кнопки по умолчанию
    /// (AcceptButton), поэтому необработанный Enter уходит в DefWindowProc, который
    /// проигрывает системный «дзынь». Возврат true гасит WM_KEYDOWN до генерации
    /// WM_CHAR, так что звука не будет.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Return && IsNumericFieldFocused())
            return true;

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>Фокус стоит на числовом поле (или на его внутреннем редакторе).</summary>
    private bool IsNumericFieldFocused()
    {
        for (Control? c = ActiveControl; c is not null; c = c.Parent)
            if (c is NumericUpDown) return true;

        return false;
    }
}
