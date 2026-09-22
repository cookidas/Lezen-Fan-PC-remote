using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace LezenTray;

public sealed class TrayContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly AdvertisementSender _sender = new();
    private readonly Dictionary<string, LastState> _states = [];
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;
    private bool _showingDevices;
    private bool _disposed;
    private bool _sending;
    private bool _keepMenuOpen;
    private string _status = Strings.T("status.default");
    internal ContextMenuStrip Menu { get; } = new();

    private sealed class LastState
    {
        public bool? Power;
        public bool? Swing;
        public FanCommand? Mode;
    }

    public TrayContext(bool showFirstRun = true, AppSettings? settings = null, bool visible = true)
    {
        _settings = settings ?? AppSettings.Load();
        Menu.ShowImageMargin = false;
        Menu.Renderer = new StatusRenderer();
        Menu.Opening += (_, _) => RebuildMenu();
        // Speed / timer / mode need repeated clicks, so those submenus should not
        // close the whole tray menu on every click. Commands() sets _keepMenuOpen
        // right before the click's command is sent, and this cancels that one
        // close attempt on the root menu; other items (exit, device pick, etc.)
        // close normally since they never set the flag.
        Menu.Closing += (_, e) =>
        {
            if (!_keepMenuOpen || e.CloseReason != ToolStripDropDownCloseReason.ItemClicked) return;
            e.Cancel = true;
            _keepMenuOpen = false;
        };
        _icon = CreateFanIcon();
        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = Strings.T("tray.text"),
            ContextMenuStrip = Menu,
            Visible = visible
        };
        _tray.DoubleClick += (_, _) => ShowDevices();
        Strings.Language = _settings.Language;
        ApplyLanguage();
        RebuildMenu();
        if (showFirstRun && _settings.Devices.Count == 0)
            Application.Idle += ShowFirstRun;
    }

    public static Color PowerColor(bool? isOn) => isOn switch
    {
        true => Color.ForestGreen,
        false => Color.Firebrick,
        null => Color.Gray
    };

    private void ShowFirstRun(object? sender, EventArgs e)
    {
        Application.Idle -= ShowFirstRun;
        ShowDevices();
    }

    private void ApplyLanguage()
    {
        Menu.Font = new Font(Strings.FontFamily, 10F);
    }

    private void SetLanguage(string language)
    {
        if (_settings.Language == language) return;
        var previous = _settings.Language;
        _settings.Language = language;
        Strings.Language = language;
        try { _settings.Save(); }
        catch (Exception ex)
        {
            _settings.Language = previous;
            Strings.Language = previous;
            MessageBox.Show(ex.Message, Strings.T("title.lezen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ApplyLanguage();
        _status = Strings.T("status.default");
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        foreach (ToolStripItem item in Menu.Items.Cast<ToolStripItem>().ToArray())
            item.Dispose();
        Menu.Items.Clear();
        FanDevice? selected = _settings.Devices.Find(d => d.Id == _settings.SelectedId);
        LastState state = selected is not null && _states.TryGetValue(selected.Id, out var known) ? known : new();
        bool available = selected is not null && !_sending && !_showingDevices;
        Menu.Items.Add(new ToolStripMenuItem(selected is null ? Strings.T("menu.title.none") : Strings.T("menu.title.device", selected.Name))
        {
            Enabled = false,
            ForeColor = Color.FromArgb(40, 48, 60),
            Tag = "status"
        });
        Menu.Items.Add(new ToolStripSeparator());
        var power = new ToolStripMenuItem(state.Power switch
        {
            true => Strings.T("power.on_to_off"),
            false => Strings.T("power.off_to_on"),
            null => Strings.T("power.unknown_to_on")
        })
        {
            Enabled = available,
            ForeColor = PowerColor(state.Power),
            Tag = "status"
        };
        power.Click += async (_, _) => await SendAsync(state.Power == true ? FanCommand.PowerOff : FanCommand.PowerOn);
        Menu.Items.Add(power);
        Menu.Items.Add(Commands(Strings.T("menu.speed"), available, null,
            (Strings.T("menu.speed_up"), FanCommand.SpeedUp), (Strings.T("menu.speed_down"), FanCommand.SpeedDown)));
        Menu.Items.Add(Commands(Strings.T("menu.timer"), available, null,
            (Strings.T("menu.timer_up"), FanCommand.TimerUp), (Strings.T("menu.timer_down"), FanCommand.TimerDown)));
        var swing = new ToolStripMenuItem(state.Swing switch
        {
            true => Strings.T("swing.on_to_off"),
            false => Strings.T("swing.off_to_on"),
            null => Strings.T("swing.unknown_to_on")
        }) { Enabled = available };
        swing.Click += async (_, _) => await SendAsync(state.Swing == true ? FanCommand.SwingOff : FanCommand.SwingOn);
        Menu.Items.Add(swing);
        Menu.Items.Add(Commands(Strings.T("menu.mode"), available, state.Mode,
            (Strings.T("mode.normal"), FanCommand.Normal), (Strings.T("mode.natural"), FanCommand.Natural),
            (Strings.T("mode.sleep"), FanCommand.Sleep), (Strings.T("mode.temperature"), FanCommand.Temperature)));
        Menu.Items.Add(new ToolStripSeparator());

        var devices = new ToolStripMenuItem(Strings.T("menu.devices")) { Enabled = !_sending && !_showingDevices };
        devices.DropDownItems.Add(Strings.T("menu.devices.add_mode"), null, (_, _) => ShowDevices());
        devices.DropDownItems.Add(new ToolStripSeparator());
        if (_settings.Devices.Count == 0)
            devices.DropDownItems.Add(new ToolStripMenuItem(Strings.T("menu.devices.none")) { Enabled = false });
        foreach (FanDevice device in _settings.Devices)
        {
            var item = new ToolStripMenuItem(device.Name)
            {
                Checked = device.Id == _settings.SelectedId,
                ToolTipText = Strings.T("device.tooltip", device.Id)
            };
            item.Click += (_, _) =>
            {
                var previous = _settings.SelectedId;
                _settings.SelectedId = device.Id;
                try { _settings.Save(); }
                catch (Exception ex)
                {
                    _settings.SelectedId = previous;
                    MessageBox.Show(ex.Message, Strings.T("err.device_select_save"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                RebuildMenu();
            };
            devices.DropDownItems.Add(item);
        }
        devices.DropDownItems.Add(new ToolStripSeparator());
        devices.DropDownItems.Add(Strings.T("menu.devices.manage"), null, (_, _) => ShowDevices());
        Menu.Items.Add(devices);

        var language = new ToolStripMenuItem(Strings.T("menu.language")) { Enabled = !_sending };
        var english = new ToolStripMenuItem(Strings.T("lang.english")) { Checked = _settings.Language == "en" };
        english.Click += (_, _) => SetLanguage("en");
        var korean = new ToolStripMenuItem(Strings.T("lang.korean")) { Checked = _settings.Language == "ko" };
        korean.Click += (_, _) => SetLanguage("ko");
        language.DropDownItems.Add(english);
        language.DropDownItems.Add(korean);
        Menu.Items.Add(language);

        Menu.Items.Add(new ToolStripSeparator());
        Menu.Items.Add(new ToolStripMenuItem(_sending ? Strings.T("status.sending") : _status) { Enabled = false });
        Menu.Items.Add(Strings.T("menu.log"), null, (_, _) =>
        {
            DiagnosticLog.Write("log.open");
            try { Process.Start(new ProcessStartInfo(DiagnosticLog.PathName) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, Strings.T("title.log")); }
        });
        var exit = new ToolStripMenuItem(Strings.T("menu.exit")) { Enabled = !_sending && !_showingDevices };
        exit.Click += (_, _) => ExitThread();
        Menu.Items.Add(exit);
        var tooltip = selected is null ? Strings.T("tooltip.add_fan") : Strings.T("tooltip.device", selected.Name);
        _tray.Text = tooltip[..Math.Min(tooltip.Length, 63)];
    }

    private ToolStripMenuItem Commands(string text, bool enabled, FanCommand? selected, params (string Text, FanCommand Command)[] children)
    {
        var menu = new ToolStripMenuItem(text) { Enabled = enabled };
        foreach (var child in children)
        {
            var item = new ToolStripMenuItem(child.Text) { Enabled = enabled, Checked = child.Command == selected };
            item.Click += async (_, _) =>
            {
                _keepMenuOpen = true;
                await SendAsync(child.Command);
            };
            menu.DropDownItems.Add(item);
        }
        // Keep the submenu itself (speed/timer/mode) open across a click too.
        menu.DropDown.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
        return menu;
    }

    internal void RecordCommand(string id, FanCommand command)
    {
        if (!_states.TryGetValue(id, out var state)) _states[id] = state = new();
        switch (command)
        {
            case FanCommand.PowerOn: state.Power = true; break;
            case FanCommand.PowerOff: state.Power = false; break;
            case FanCommand.SwingOn: state.Swing = true; break;
            case FanCommand.SwingOff: state.Swing = false; break;
            case FanCommand.Normal or FanCommand.Natural or FanCommand.Sleep or FanCommand.Temperature: state.Mode = command; break;
        }
        RebuildMenu();
    }

    private async Task SendAsync(FanCommand command)
    {
        var device = _settings.Devices.Find(d => d.Id == _settings.SelectedId);
        if (_sending || _showingDevices || _disposed || device is null) return;
        _sending = true;
        RebuildMenu();
        try
        {
            DiagnosticLog.Write("control", new { device.Id, Command = command.ToString() });
            await _sender.SendAsync(FanProtocol.CompanyId, FanProtocol.EncodeForWindows(command, device.Id), FanProtocol.Duration);
            if (_disposed) return;
            RecordCommand(device.Id, command);
            _status = Strings.T("status.default");
        }
        catch (Exception ex)
        {
            if (_disposed) return;
            // A partial/failed transmission may still have reached the fan.
            _states.Remove(device.Id);
            _status = Strings.T("status.send_failed");
            MessageBox.Show(ex.Message, Strings.T("title.send_failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _sending = false;
            if (!_disposed) RebuildMenu();
        }
    }

    private void ShowDevices()
    {
        if (_showingDevices || _sending || _disposed) return;
        _showingDevices = true;
        try
        {
            using var form = new DeviceForm(_settings, _sender);
            form.ShowDialog();
        }
        finally
        {
            _showingDevices = false;
            _states.Clear();
            RebuildMenu();
        }
    }

    protected override void ExitThreadCore()
    {
        Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _sender.Dispose();
            Application.Idle -= ShowFirstRun;
            _tray.Visible = false;
            _tray.Dispose();
            Menu.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Icon CreateFanIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(25, 47, 62));
            graphics.FillEllipse(background, 1, 1, 62, 62);
            using var blade = new SolidBrush(Color.FromArgb(112, 221, 198));
            graphics.TranslateTransform(32, 32);
            for (int i = 0; i < 3; i++)
            {
                graphics.FillEllipse(blade, -6, -25, 14, 25);
                graphics.RotateTransform(120);
            }
            using var center = new SolidBrush(Color.White);
            graphics.FillEllipse(center, -6, -6, 12, 12);
        }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private sealed class StatusRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (Equals(e.Item.Tag, "status")) e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }
}
