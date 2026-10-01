using System.Text.Json;

namespace QuickClicker;

public sealed partial class MainForm
{
    private sealed class SettingsDto
    {
        public int ClickButton { get; set; }
        public int IntervalMs { get; set; } = 10;
        public int HoldMs { get; set; } = 10;
        public bool PauseEnabled { get; set; }
        public int WorkSeconds { get; set; } = 30;
        public int RestSeconds { get; set; } = 10;
        public int ClickMethod { get; set; }
        public int ToggleVk { get; set; } = (int)Keys.F6;
        public int ToggleMouse { get; set; } = -1; // -1 = переключатель на клавиатуре
        public bool ToggleCtrl { get; set; }
        public bool ToggleAlt { get; set; }
        public bool ToggleShift { get; set; }
        public bool BlockToggle { get; set; }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;

            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsPath));
            if (dto is null) return;

            _intervalMs = Math.Clamp(dto.IntervalMs, 1, 60000);
            _holdMs = Math.Clamp(dto.HoldMs, 0, 2000);
            _workSeconds = Math.Clamp(dto.WorkSeconds, 1, 86400);
            _restSeconds = Math.Clamp(dto.RestSeconds, 1, 86400);
            _pauseEnabled = dto.PauseEnabled;
            _clickButton = (MouseButtonKind)Math.Clamp(dto.ClickButton, 0, 4);
            _clickMethod = (ClickMethod)Math.Clamp(dto.ClickMethod, 0, 1);
            _toggleVk = dto.ToggleVk;
            _toggleMouseButton = dto.ToggleMouse >= 0
                ? (MouseButtonKind)Math.Clamp(dto.ToggleMouse, 0, 4)
                : null;
            _toggleCtrl = dto.ToggleCtrl;
            _toggleAlt = dto.ToggleAlt;
            _toggleShift = dto.ToggleShift;
            _blockToggle = dto.BlockToggle;

            _cmbButton.SelectedIndex = (int)_clickButton;
            _cmbMethod.SelectedIndex = (int)_clickMethod;
            _numInterval.Value = _intervalMs;
            _numHold.Value = _holdMs;
            _numWork.Value = _workSeconds;
            _numRest.Value = _restSeconds;
            _chkBlock.Checked = _blockToggle;
            _chkPause.Checked = _pauseEnabled;
            _numWork.Enabled = _numRest.Enabled = _pauseEnabled;
        }
        catch
        {
            // повреждённый конфиг — просто стартуем со значениями по умолчанию
        }
    }

    private void SaveSettings()
    {
        try
        {
            var dto = new SettingsDto
            {
                ClickButton = (int)_clickButton,
                IntervalMs = _intervalMs,
                HoldMs = _holdMs,
                PauseEnabled = _pauseEnabled,
                WorkSeconds = _workSeconds,
                RestSeconds = _restSeconds,
                ClickMethod = (int)_clickMethod,
                ToggleVk = _toggleVk,
                ToggleMouse = _toggleMouseButton is MouseButtonKind mb ? (int)mb : -1,
                ToggleCtrl = _toggleCtrl,
                ToggleAlt = _toggleAlt,
                ToggleShift = _toggleShift,
                BlockToggle = _blockToggle,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // сохранение не критично — молча игнорируем
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopClicker();
        UninstallHooks();
        SaveSettings();
        base.OnFormClosing(e);
    }
}
