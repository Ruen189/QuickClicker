using System.Diagnostics;

namespace QuickClicker;

public sealed partial class MainForm
{
    // ================= АВТОКЛИКЕР =================

    private void ToggleClicker()
    {
        if (_running) StopClicker();
        else StartClicker();
    }

    private void StartClicker()
    {
        if (_running) return;

        _running = true;
        _clickThread = new Thread(ClickLoop) { IsBackground = true, Name = "QuickClicker" };
        _clickThread.Start();

        _btnStart.Text = "Стоп";
        string pauseInfo = _pauseEnabled ? $", пауза {_workSeconds}/{_restSeconds} с" : "";
        _lblStatus.Text = $"Работает · {_intervalMs} мс, зажатие {_holdMs} мс{pauseInfo}";
        _lblStatus.ForeColor = Color.SeaGreen;
        Text = $"QuickClicker — РАБОТАЕТ ({MouseButtonName(_clickButton)}, {_intervalMs} мс, зажатие {_holdMs} мс{pauseInfo})";
    }

    private void StopClicker()
    {
        if (!_running) return;

        _running = false;
        _clickThread?.Join(500);
        _clickThread = null;

        _btnStart.Text = "Старт";
        _lblStatus.Text = "Остановлен";
        _lblStatus.ForeColor = Color.DimGray;
        Text = "QuickClicker";
    }

    private void ClickLoop()
    {
        var sw = Stopwatch.StartNew();
        long nextClick = 0;      // когда делать следующий клик
        long workStartedAt = 0;  // начало текущей «рабочей» фазы
        long restUntil = 0;      // до какого момента отдыхаем (0 — не отдыхаем)

        while (_running)
        {
            if (_pauseEnabled)
            {
                long now = sw.ElapsedMilliseconds;

                if (restUntil != 0)
                {
                    if (now < restUntil)
                    {
                        // Отдыхаем: спим мелкими порциями, «Стоп» срабатывает сразу.
                        Thread.Sleep(20);
                        continue;
                    }

                    // Отдых закончился — начинаем новый цикл работы.
                    restUntil = 0;
                    workStartedAt = now;
                    nextClick = now;
                    continue;
                }

                if (now - workStartedAt >= (long)_workSeconds * 1000)
                {
                    restUntil = now + (long)_restSeconds * 1000;
                    continue;
                }
            }

            if (_clickMethod == ClickMethod.WindowMessages) Native.PostMouseClick(_clickButton, _holdMs, _isRunning);
            else Native.SendMouseClick(_clickButton, _holdMs, _isRunning);

            nextClick += _intervalMs;
            long remaining = nextClick - sw.ElapsedMilliseconds;

            // Спим мелкими порциями, чтобы «Стоп» срабатывал мгновенно.
            while (remaining > 0 && _running)
            {
                int slice = (int)Math.Min(remaining, 20);
                Thread.Sleep(slice);
                remaining = nextClick - sw.ElapsedMilliseconds;
            }

            if (remaining <= 0) nextClick = sw.ElapsedMilliseconds; // не копим отставание
        }
    }

    // ================= ТЕСТ РУЧНОГО КЛИКА =================

    private void ToggleTest()
    {
        if (_testing)
        {
            _testing = false;
            _btnTest.Text = "Начать тест";
        }
        else
        {
            ResetTest();
            _testing = true;
            _btnTest.Text = "Остановить тест";
        }
    }

    private void ResetTest()
    {
        _userCount = 0;
        _userSumMs = 0;
        _userMinMs = double.MaxValue;
        _userLastMs = 0;
        _lastUserClickTicks = 0;
        UpdateTestLabels();
    }

    /// <summary>
    /// Вызывается из хука мыши только для «живых» нажатий (инжектированные отсеиваются).
    /// </summary>
    private void RecordUserClick(MouseButtonKind button)
    {
        if (!_testing || button != _clickButton) return;

        long now = Stopwatch.GetTimestamp();

        if (_lastUserClickTicks != 0)
        {
            double dt = (now - _lastUserClickTicks) * 1000.0 / Stopwatch.Frequency;
            _userSumMs += dt;
            _userLastMs = dt;
            if (dt < _userMinMs) _userMinMs = dt;
        }

        _lastUserClickTicks = now;
        _userCount++;
        UpdateTestLabels();
    }

    private void UpdateTestLabels()
    {
        _lblCount.Text = $"Кликов: {_userCount}";

        if (_userCount >= 2)
        {
            double avg = _userSumMs / (_userCount - 1);
            _lblLast.Text = $"Последний интервал: {_userLastMs:F0} мс";
            _lblAvg.Text = $"Средний интервал: {avg:F1} мс";
            _lblMin.Text = $"Минимальный интервал: {_userMinMs:F0} мс";
            _lblCps.Text = avg > 0
                ? $"Скорость: {1000.0 / avg:F1} клик/с"
                : "Скорость: —";
        }
        else
        {
            _lblLast.Text = "Последний интервал: —";
            _lblAvg.Text = "Средний интервал: —";
            _lblMin.Text = "Минимальный интервал: —";
            _lblCps.Text = "Скорость: —";
        }
    }
}
