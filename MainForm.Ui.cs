namespace QuickClicker;

public sealed partial class MainForm
{
    private void BuildClickerGroup()
    {
        var gb = new GroupBox { Text = "Автокликер", Location = new Point(12, 8), Size = new Size(364, 326) };

        var lblButton = new Label { Text = "Кнопка клика:", Location = new Point(14, 30), AutoSize = true };
        _cmbButton = new ComboBox
        {
            Location = new Point(150, 26),
            Size = new Size(198, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _cmbButton.Items.AddRange(new object[]
        {
            "ЛКМ (левая)", "ПКМ (правая)", "СКМ (средняя)", "Боковая X1", "Боковая X2",
        });
        _cmbButton.SelectedIndex = 0;
        _cmbButton.SelectedIndexChanged += (_, _) =>
        {
            _clickButton = (MouseButtonKind)_cmbButton.SelectedIndex;
            SaveSettings();
        };

        var lblInterval = new Label { Text = "Интервал (мс):", Location = new Point(14, 62), AutoSize = true };
        _numInterval = new NumericUpDown
        {
            Location = new Point(150, 58),
            Size = new Size(198, 23),
            Minimum = 1,
            Maximum = 60000,
            Value = _intervalMs,
            ThousandsSeparator = true,
        };
        _numInterval.KeyDown += SuppressBeepOnEnter;
        _numInterval.ValueChanged += (_, _) =>
        {
            _intervalMs = (int)_numInterval.Value;
            SaveSettings();
        };

        var lblHold = new Label { Text = "Зажатие (мс):", Location = new Point(14, 94), AutoSize = true };
        _numHold = new NumericUpDown
        {
            Location = new Point(150, 90),
            Size = new Size(198, 23),
            Minimum = 0,
            Maximum = 2000,
            Value = _holdMs,
        };
        _numHold.KeyDown += SuppressBeepOnEnter;
        _numHold.ValueChanged += (_, _) =>
        {
            _holdMs = (int)_numHold.Value;
            SaveSettings();
        };
        _tip.SetToolTip(_numHold,
            "Сколько миллисекунд кнопка остаётся зажатой (down … up).\r\n" +
            "0 — мгновенный клик. Многим играм нужно 10–30 мс,\r\n" +
            "иначе клик не успевает попасть в кадр и не регистрируется.");

        var lblHotkey = new Label { Text = "Кнопка вкл/выкл:", Location = new Point(14, 126), AutoSize = true };
        _btnHotkey = new Button
        {
            Location = new Point(150, 122),
            Size = new Size(198, 23),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "F6",
        };
        _btnHotkey.Click += (_, _) => BeginCapture();
        _tip.SetToolTip(_btnHotkey,
            "Переключатель вкл/выкл — ровно один.\r\n" +
            "Нажми поле и нажми нужную клавишу (можно с Ctrl/Alt/Shift)\r\n" +
            "или кнопку мыши, включая боковые X1/X2.\r\n" +
            "Мышь: предыдущая клавиша перестаёт работать.\r\n" +
            "Клавиша: кнопка мыши перестаёт работать.");

        var lblMethod = new Label { Text = "Метод клика:", Location = new Point(14, 158), AutoSize = true };
        _cmbMethod = new ComboBox
        {
            Location = new Point(150, 154),
            Size = new Size(198, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _cmbMethod.Items.AddRange(new object[]
        {
            "SendInput (обычный)",
            "WM-сообщения (для игр)",
        });
        _cmbMethod.SelectedIndex = 0;
        _cmbMethod.SelectedIndexChanged += (_, _) =>
        {
            _clickMethod = (ClickMethod)_cmbMethod.SelectedIndex;
            SaveSettings();
        };

        _chkBlock = new CheckBox
        {
            Text = "Блокировать кнопку (не уходит в игру)",
            Location = new Point(14, 184),
            Size = new Size(340, 20),
        };
        _chkBlock.CheckedChanged += (_, _) => { _blockToggle = _chkBlock.Checked; SaveSettings(); };

        _chkPause = new CheckBox
        {
            Text = "Периодическая пауза (опционально)",
            Location = new Point(14, 210),
            Size = new Size(340, 20),
        };
        _tip.SetToolTip(_chkPause,
            "Кликер работает циклами: кликает заданное число секунд,\r\n" +
            "затем столько же «отдыхает» и снова кликает.\r\n" +
            "Выключено — кликер работает непрерывно.");
        _chkPause.CheckedChanged += (_, _) =>
        {
            _pauseEnabled = _chkPause.Checked;
            _numWork.Enabled = _numRest.Enabled = _pauseEnabled;
            SaveSettings();
        };

        var lblWork = new Label { Text = "Кликать (с):", Location = new Point(14, 242), AutoSize = true };
        _numWork = new NumericUpDown
        {
            Location = new Point(150, 238),
            Size = new Size(90, 23),
            Minimum = 1,
            Maximum = 86400,
            Value = _workSeconds,
            Enabled = _pauseEnabled,
        };
        _numWork.KeyDown += SuppressBeepOnEnter;
        _numWork.ValueChanged += (_, _) => { _workSeconds = (int)_numWork.Value; SaveSettings(); };

        var lblRest = new Label { Text = "Отдыхать (с):", Location = new Point(14, 266), AutoSize = true };
        _numRest = new NumericUpDown
        {
            Location = new Point(150, 262),
            Size = new Size(90, 23),
            Minimum = 1,
            Maximum = 86400,
            Value = _restSeconds,
            Enabled = _pauseEnabled,
        };
        _numRest.KeyDown += SuppressBeepOnEnter;
        _numRest.ValueChanged += (_, _) => { _restSeconds = (int)_numRest.Value; SaveSettings(); };

        _btnStart = new Button { Text = "Старт", Location = new Point(14, 288), Size = new Size(120, 28) };
        _btnStart.Click += (_, _) => ToggleClicker();

        _lblStatus = new Label
        {
            Text = "Остановлен",
            Location = new Point(146, 294),
            AutoSize = true,
            ForeColor = Color.DimGray,
        };

        gb.Controls.AddRange(new Control[]
        {
            lblButton, _cmbButton, lblInterval, _numInterval, lblHold, _numHold,
            lblHotkey, _btnHotkey, lblMethod, _cmbMethod,
            _chkBlock, _chkPause, lblWork, _numWork, lblRest, _numRest,
            _btnStart, _lblStatus,
        });

        Controls.Add(gb);
    }

    /// <summary>
    /// Enter в поле числа не должен вызывать системный «дзынь»: гасим само нажатие.
    /// </summary>
    private static void SuppressBeepOnEnter(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Return) return;

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void BuildTestGroup()
    {
        var gb = new GroupBox { Text = "Тест ручного клика", Location = new Point(12, 346), Size = new Size(364, 244) };

        var lblHint = new Label
        {
            Text = "Нажми «Начать тест» и кликай выбранной кнопкой вручную.\nКлики самого кликера не учитываются.",
            Location = new Point(12, 22),
            Size = new Size(340, 48),
            ForeColor = Color.DimGray,
        };

        _lblCount = new Label { Text = "Кликов: 0", Location = new Point(12, 78), AutoSize = true };
        _lblLast = new Label { Text = "Последний интервал: —", Location = new Point(12, 102), AutoSize = true };
        _lblAvg = new Label { Text = "Средний интервал: —", Location = new Point(12, 126), AutoSize = true };
        _lblMin = new Label { Text = "Минимальный интервал: —", Location = new Point(12, 150), AutoSize = true };
        _lblCps = new Label { Text = "Скорость: —", Location = new Point(12, 174), AutoSize = true };

        _btnTest = new Button { Text = "Начать тест", Location = new Point(12, 198), Size = new Size(140, 30) };
        _btnTest.Click += (_, _) => ToggleTest();

        _btnResetTest = new Button { Text = "Сброс", Location = new Point(160, 198), Size = new Size(90, 30) };
        _btnResetTest.Click += (_, _) => ResetTest();

        gb.Controls.AddRange(new Control[]
        {
            lblHint, _lblCount, _lblLast, _lblAvg, _lblMin, _lblCps, _btnTest, _btnResetTest,
        });

        Controls.Add(gb);
    }
}
