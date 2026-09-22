namespace LezenTray;

public sealed class DeviceForm : Form
{
    private readonly AppSettings _settings;
    private readonly AdvertisementSender _sender;
    private readonly ListView _saved = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false, AccessibleName = Strings.T("accessible.saved_list")
    };
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, MaxLength = 60 };
    private readonly TextBox _id = new() { Width = 90, MaxLength = 4, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox _savedName = new() { Width = 180, MaxLength = 60 };
    private readonly Button _bind = MakeButton(Strings.T("btn.bind"));
    private readonly Button _newId = MakeButton(Strings.T("btn.new_id"));
    private readonly Button _resend = MakeButton(Strings.T("btn.resend"));
    private readonly Button _select = MakeButton(Strings.T("btn.select"));
    private readonly Button _rename = MakeButton(Strings.T("btn.rename"));
    private readonly Button _remove = MakeButton(Strings.T("btn.remove"));
    private readonly Button _close = MakeButton(Strings.T("btn.close"));
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 44), TextAlign = ContentAlignment.MiddleLeft };
    private bool _sending;

    public DeviceForm(AppSettings settings, AdvertisementSender sender)
    {
        _settings = settings;
        _sender = sender;
        _name.Text = Strings.IsKorean ? "내 선풍기" : "My Fan";
        _name.AccessibleName = Strings.T("accessible.name_input");
        _id.AccessibleName = Strings.T("accessible.id_input");
        _savedName.AccessibleName = Strings.T("accessible.saved_name");
        Text = Strings.T("form.title");
        Font = new Font(Strings.FontFamily, 10F);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(730, 650);
        MinimumSize = new Size(690, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 249, 251);
        ShowIcon = false;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = Strings.T("intro.text"),
            AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14)
        }, 0, 0);

        var addGroup = new GroupBox { Text = Strings.T("group.add"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 12) };
        var addLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 4 };
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addLayout.Controls.Add(FieldLabel(Strings.T("field.name")), 0, 0);
        addLayout.Controls.Add(_name, 1, 0);
        addLayout.Controls.Add(FieldLabel(Strings.T("field.id")), 0, 1);
        var codeRow = Flow();
        codeRow.Controls.AddRange([_id, _newId]);
        addLayout.Controls.Add(codeRow, 1, 1);
        var hint = new Label
        {
            Text = Strings.T("hint.id"),
            Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.FromArgb(85, 92, 104), Margin = new Padding(3, 4, 3, 8)
        };
        addLayout.Controls.Add(hint, 0, 2);
        addLayout.SetColumnSpan(hint, 2);
        var bindRow = Flow();
        bindRow.Controls.Add(_bind);
        addLayout.Controls.Add(bindRow, 1, 3);
        addGroup.Controls.Add(addLayout);
        layout.Controls.Add(addGroup, 0, 1);

        var savedGroup = new GroupBox { Text = Strings.T("group.saved"), Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 8) };
        _saved.Columns.Add(Strings.T("col.name"), 350);
        _saved.Columns.Add(Strings.T("col.id"), 115);
        _saved.Columns.Add(Strings.T("col.inuse"), 85);
        savedGroup.Controls.Add(_saved);
        layout.Controls.Add(savedGroup, 0, 2);
        var editRow = Flow();
        editRow.Controls.AddRange([_savedName, _rename, _remove, _resend]);
        layout.Controls.Add(editRow, 0, 3);
        layout.Controls.Add(_status, 0, 4);
        var bottom = Flow();
        bottom.FlowDirection = FlowDirection.RightToLeft;
        bottom.Controls.AddRange([_close, _select]);
        layout.Controls.Add(bottom, 0, 5);
        Controls.Add(layout);
        CancelButton = _close;

        _id.Text = CreateId();
        _newId.Click += (_, _) => _id.Text = CreateId();
        _bind.Click += async (_, _) => await BindNewAsync();
        _resend.Click += async (_, _) =>
        {
            if (SelectedDevice is FanDevice device) await SendRegistrationAsync(device, add: false);
        };
        _select.Click += (_, _) => SelectSaved();
        _saved.DoubleClick += (_, _) => SelectSaved();
        _rename.Click += (_, _) => RenameSelected();
        _remove.Click += (_, _) => RemoveSelected();
        _close.Click += (_, _) => Close();
        _saved.SelectedIndexChanged += (_, _) =>
        {
            _savedName.Text = SelectedDevice?.Name ?? "";
            UpdateEnabled();
        };
        FormClosing += (_, e) =>
        {
            if (!_sending) return;
            e.Cancel = true;
            _status.Text = Strings.T("status.closing_wait");
        };
        RefreshSaved(_settings.SelectedId);
        _status.Text = Strings.T("status.initial");
    }

    private static Button MakeButton(string text) => new() { Text = text, AutoSize = true, MinimumSize = new Size(0, 31) };
    private static Label FieldLabel(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 6, 10, 0) };
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
    private FanDevice? SelectedDevice => _saved.SelectedItems.Count == 1 ? _saved.SelectedItems[0].Tag as FanDevice : null;

    private string CreateId()
    {
        int start = Random.Shared.Next(65536);
        for (int i = 0; i < 65536; i++)
        {
            string id = ((start + i) & 0xffff).ToString("X4");
            if (!_settings.Devices.Any(d => d.Id == id)) return id;
        }
        return "";
    }

    private async Task BindNewAsync()
    {
        if (_sending) return;
        string name = _name.Text.Trim();
        string id = _id.Text.Trim().ToUpperInvariant();
        if (name.Length == 0)
        {
            _status.Text = Strings.T("status.need_name");
            _name.Focus();
            return;
        }
        if (!FanProtocol.IsValidId(id))
        {
            _status.Text = Strings.T("status.bad_id");
            _id.Focus();
            return;
        }
        if (_settings.Devices.Any(d => d.Id == id))
        {
            RefreshSaved(id);
            _status.Text = Strings.T("status.dup_id");
            return;
        }
        await SendRegistrationAsync(new FanDevice(id, name), add: true);
    }

    private async Task SendRegistrationAsync(FanDevice device, bool add)
    {
        if (_sending) return;
        _sending = true;
        UpdateEnabled();
        _status.Text = Strings.T("status.sending_named", device.Name);
        try
        {
            DiagnosticLog.Write("registration", new { device.Id, Add = add });
            await _sender.SendAsync(FanProtocol.CompanyId, FanProtocol.EncodeForWindows(FanCommand.Bind, device.Id), FanProtocol.Duration);
            bool saved = SaveChange(() =>
            {
                if (add) _settings.Devices.Add(device);
                _settings.SelectedId = device.Id;
            });
            if (!saved)
            {
                _status.Text = Strings.T("status.save_failed", device.Id);
                return;
            }
            RefreshSaved(device.Id);
            _status.Text = Strings.T("status.sent");
            if (add) _id.Text = CreateId();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, Strings.T("title.registration_failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _sending = false;
            UpdateEnabled();
        }
    }

    private void RefreshSaved(string? selectedId)
    {
        _saved.BeginUpdate();
        _saved.Items.Clear();
        foreach (FanDevice device in _settings.Devices)
        {
            var item = new ListViewItem([device.Name, device.Id, device.Id == _settings.SelectedId ? Strings.T("col.selected") : ""]) { Tag = device };
            _saved.Items.Add(item);
            item.Selected = device.Id == selectedId;
        }
        _saved.EndUpdate();
        _savedName.Text = SelectedDevice?.Name ?? "";
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        bool selected = SelectedDevice is not null;
        _name.Enabled = _id.Enabled = _newId.Enabled = _bind.Enabled = _close.Enabled = _saved.Enabled = !_sending;
        _savedName.Enabled = _rename.Enabled = _remove.Enabled = _resend.Enabled = _select.Enabled = selected && !_sending;
    }

    private void SelectSaved()
    {
        if (_sending || SelectedDevice is not FanDevice device) return;
        if (!SaveChange(() => _settings.SelectedId = device.Id)) return;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RenameSelected()
    {
        if (_sending || SelectedDevice is not FanDevice device) return;
        string name = _savedName.Text.Trim();
        if (name.Length == 0) { _status.Text = Strings.T("status.need_name"); _savedName.Focus(); return; }
        if (!SaveChange(() => _settings.Devices[_settings.Devices.FindIndex(d => d.Id == device.Id)] = device with { Name = name })) return;
        RefreshSaved(device.Id);
        _status.Text = Strings.T("status.renamed");
    }

    private void RemoveSelected()
    {
        if (_sending || SelectedDevice is not FanDevice device) return;
        if (!SaveChange(() =>
        {
            _settings.Devices.RemoveAll(d => d.Id == device.Id);
            if (_settings.SelectedId == device.Id) _settings.SelectedId = _settings.Devices.FirstOrDefault()?.Id;
        })) return;
        RefreshSaved(_settings.SelectedId);
        _status.Text = Strings.T("status.removed");
    }

    private bool SaveChange(Action change)
    {
        FanDevice[] previous = _settings.Devices.ToArray();
        string? previousId = _settings.SelectedId;
        try { change(); _settings.Save(); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _settings.Devices.Clear();
            _settings.Devices.AddRange(previous);
            _settings.SelectedId = previousId;
            MessageBox.Show(this, Strings.T("status.list_save_failed", ex.Message), Strings.T("title.lezen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
