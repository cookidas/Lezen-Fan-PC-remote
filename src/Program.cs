using System.Text.Json;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace LezenTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        DiagnosticLog.Write("app.start", new { Version = typeof(Program).Assembly.GetName().Version?.ToString(), OS = Environment.OSVersion.ToString(), Arguments = args });
        try
        {
            if (args.Length == 3 && args[0] == "--capture")
            {
                AdvertisementCapture.RunAsync(args[1], int.Parse(args[2])).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length == 1 && args[0] == "--adapter")
            {
                Console.WriteLine(AdvertisementSender.GetAdapterInfoAsync().GetAwaiter().GetResult());
                return 0;
            }
            if (args.Length is >= 3 and <= 6 && args[0] == "--send")
            {
                int offset = args.Length > 3 ? int.Parse(args[3]) : 15;
                int milliseconds = args.Length > 4 ? int.Parse(args[4]) : 200;
                bool padded = args.Length < 6 ? offset == 15 : args[5] == "pad3";
                if ((args.Length == 6 && args[5] is not ("pad3" or "raw")) || (padded && offset != 15))
                    throw new ArgumentException("형식은 pad3 또는 raw이며, pad3는 offset 15에서만 사용할 수 있습니다.");
                if (milliseconds is < 100 or > 2000) throw new ArgumentOutOfRangeException(nameof(milliseconds), "시험 송신 시간은 100~2000ms입니다.");
                SendOnceAsync(args[1], args[2], offset, milliseconds, padded).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length is 1 or 2 or 3 && args[0] == "--watch")
            {
                var seconds = args.Length > 1 ? int.Parse(args[1]) : 15;
                var showAll = args.Length > 2 && args[2].Equals("all", StringComparison.OrdinalIgnoreCase);
                WatchAsync(TimeSpan.FromSeconds(seconds), showAll).GetAwaiter().GetResult();
                return 0;
            }
            if (args.Length == 2 && args[0] == "--verify-vectors")
            {
                using var stream = File.OpenRead(args[1]);
                VerifyVectors(stream);
                return 0;
            }
            if (args.Length is 1 or 2 && args[0] == "--self-test")
                return SelfTest(args.Length > 1 ? args[1] : null);
            if (args.Length != 0) throw new ArgumentException("지원하지 않는 실행 옵션입니다.");
            using var singleInstance = new Mutex(true, "Local\\LezenTray", out var isFirst);
            if (!isFirst)
            {
                MessageBox.Show("LEZEN Tray가 이미 실행 중입니다. 작업표시줄 오른쪽의 숨겨진 아이콘(∧)도 확인하세요.", "LEZEN Tray");
                return 0;
            }
            Application.ThreadException += (_, e) => ShowError(e.Exception);
            using var context = new TrayContext();
            Application.Run(context);
            return 0;
        }
        catch (Exception e)
        {
            DiagnosticLog.Write("app.failed", e.ToString());
            if (args.Length > 0) Console.Error.WriteLine(e);
            else ShowError(e);
            return 1;
        }
    }

    private static void ShowError(Exception e)
    {
        DiagnosticLog.Write("app.error", e.ToString());
        MessageBox.Show(e.Message, "LEZEN Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static async Task SendOnceAsync(string id, string name, int offset, int milliseconds, bool padded)
    {
        if (!Enum.TryParse<FanCommand>(name, true, out var command) || !Enum.IsDefined(command))
            throw new ArgumentException("알 수 없는 명령입니다. 예: PowerOn, PowerOff, Bind");
        var packet = padded ? FanProtocol.EncodeForWindows(command, id) : FanProtocol.Encode(command, id, offset);
        using var sender = new AdvertisementSender();
        DiagnosticLog.Write("control.cli", new { Id = id, Command = command.ToString(), Offset = offset, DurationMs = milliseconds, Padded = padded });
        await sender.SendAsync(FanProtocol.CompanyId, packet, TimeSpan.FromMilliseconds(milliseconds));
        Console.WriteLine($"Windows 송신 완료 · ID {id.ToUpperInvariant()} · {command} · offset {offset} · {milliseconds}ms · pad3 {padded} · {Convert.ToHexString(packet)}");
        Console.WriteLine("선풍기의 수신·동작 여부는 실물에서 확인해야 합니다.");
    }

    // Passive local check: listens for our own Company ID 0xFFF0 advertisement (or,
    // with showAll, any manufacturer-data advertisement, to sanity-check the receive
    // path itself). Not every adapter can hear its own transmission while it is also
    // advertising, so "0건" here is suggestive, not conclusive — a second device is
    // more reliable when it's available.
    private static async Task WatchAsync(TimeSpan duration, bool showAll)
    {
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        int hits = 0;
        var seenCompanyIds = new HashSet<ushort>();
        watcher.Received += (_, e) =>
        {
            foreach (var md in e.Advertisement.ManufacturerData)
            {
                if (!showAll && md.CompanyId != FanProtocol.CompanyId) continue;
                var reader = DataReader.FromBuffer(md.Data);
                var bytes = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(bytes);
                hits++;
                seenCompanyIds.Add(md.CompanyId);
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} 주소={e.BluetoothAddress:X} RSSI={e.RawSignalStrengthInDBm} CompanyId=0x{md.CompanyId:X4} 데이터={Convert.ToHexString(bytes)}");
            }
        };
        Console.WriteLine(showAll
            ? $"모든 제조사 데이터 광고를 {duration.TotalSeconds:0}초 동안 스캔합니다(필터 없음). 주변에 폰·이어폰 등이 있으면 몇 건은 잡혀야 정상입니다."
            : $"Company ID 0x{FanProtocol.CompanyId:X4} 광고를 {duration.TotalSeconds:0}초 동안 스캔합니다. 이 창은 그대로 두고, 다른 cmd/PowerShell 창에서 --send를 실행하세요.");
        watcher.Start();
        await Task.Delay(duration);
        watcher.Stop();
        Console.WriteLine(hits == 0
            ? "스캔 종료 · 0건 감지."
            : $"스캔 종료 · {hits}건 감지 · CompanyId 종류: {string.Join(", ", seenCompanyIds.Select(id => $"0x{id:X4}"))}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Self-test failed: " + message);
    }

    private static void VerifyVectors(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        int count = 0;
        foreach (var vector in document.RootElement.EnumerateArray())
        {
            string id = vector.GetProperty("id").GetString()!;
            var command = (FanCommand)Convert.ToByte(vector.GetProperty("command").GetString(), 16);
            string expected = vector.GetProperty("payload").GetString()!;
            string actual = Convert.ToHexString(FanProtocol.Encode(command, id));
            Check(actual.Equals(expected, StringComparison.OrdinalIgnoreCase), $"Original ARM64 packet {id}/{command}: expected {expected}, got {actual}");
            string windowsActual = Convert.ToHexString(FanProtocol.EncodeForWindows(command, id));
            Check(windowsActual.Equals("FFF0FF" + expected, StringComparison.OrdinalIgnoreCase), $"Windows RF alignment {id}/{command}");
            count++;
        }
        Check(count > 0, "Golden vectors must not be empty");
        Console.WriteLine($"PASS: {count} original ARM64 vectors and {count} Windows-aligned vectors");
    }

    private static int SelfTest(string? imagePath)
    {
        using (var stream = typeof(Program).Assembly.GetManifestResourceStream("LezenTray.packet-vectors.json")
            ?? throw new FileNotFoundException("원본 대조용 packet-vectors.json이 없습니다.")) VerifyVectors(stream);
        Check(Convert.ToHexString(FanProtocol.EncodeForWindows(FanCommand.PowerOn, "6C3C")) == "FFF0FF6DB6435F6E7F37A14F7B5EBE5DF9266572F0", "Hardware-confirmed power-on packet");
        Check(Convert.ToHexString(FanProtocol.EncodeForWindows(FanCommand.PowerOff, "6C3C")) == "FFF0FF6DB6435F6E7F37A14F7B5FBE5DF92666C2C6", "Hardware-confirmed power-off packet");
        Check(FanProtocol.Duration == TimeSpan.FromMilliseconds(200), "Hardware-confirmed 200ms duration");
        Check(!FanProtocol.IsValidId("123") && !FanProtocol.IsValidId("GGGG") && !FanProtocol.IsValidId(null), "Invalid IDs rejected");
        Check(FanProtocol.Encode(FanCommand.Normal, "abcd").SequenceEqual(FanProtocol.Encode(FanCommand.Normal, "ABCD")), "Case-insensitive IDs");
        bool rejected = false;
        try { FanProtocol.Encode((FanCommand)0xB5, "1234"); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Unsupported commands rejected");
        var device = new FanDevice("1234", "내 선풍기");
        var settings = new AppSettings { Devices = [device], SelectedId = device.Id };
        settings.Validate();
        settings.SelectedId = "FFFF";
        settings.Validate();
        Check(settings.SelectedId is null, "Stale selection cleared");
        settings.Devices.Add(device);
        rejected = false;
        try { settings.Validate(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Duplicate IDs rejected");
        settings = new AppSettings { Devices = [device], SelectedId = device.Id };
        var roundtrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        roundtrip.Validate();
        Check(roundtrip.SelectedId == "1234" && roundtrip.Devices.Single() == device, "Settings round trip");
        using var context = new TrayContext(false, settings, false);
        var menu = context.Menu;
        var text = string.Join("|", menu.Items.Cast<ToolStripItem>().Select(i => i.Text));
        Check(text.Contains("전원") && text.Contains("바람") && text.Contains("타이머") && text.Contains("회전") && text.Contains("선풍기 추가"), "Requested menu structure");
        var mode = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "바람 모드");
        Check(mode.DropDownItems.Count == 4 && mode.DropDownItems.Cast<ToolStripItem>().All(i => i.Enabled), "Four enabled wind modes");
        context.RecordCommand("1234", FanCommand.PowerOff);
        var power = menu.Items.Cast<ToolStripItem>().Single(i => i.Text?.StartsWith("전원") == true);
        Check(power.ForeColor == Color.Firebrick && power.Text!.Contains("꺼짐"), "Power-off display red");
        context.RecordCommand("1234", FanCommand.PowerOn);
        power = menu.Items.Cast<ToolStripItem>().Single(i => i.Text?.StartsWith("전원") == true);
        Check(power.ForeColor == Color.ForestGreen && power.Text!.Contains("켜짐"), "Power-on display green");
        context.RecordCommand("1234", FanCommand.Natural);
        mode = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "바람 모드");
        Check(mode.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Checked).Text == "자연풍", "Mode selection display");
        settings.SelectedId = null;
        context.RecordCommand("1234", FanCommand.SwingOn);
        power = menu.Items.Cast<ToolStripItem>().Single(i => i.Text?.StartsWith("전원") == true);
        Check(!power.Enabled && power.Text!.Contains("미확인"), "No selection disables commands and clears displayed state");
        settings.SelectedId = "1234";
        context.RecordCommand("1234", FanCommand.SwingOn);
        if (imagePath is not null)
        {
            imagePath = Path.GetFullPath(imagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            menu.CreateControl();
            menu.Size = menu.GetPreferredSize(Size.Empty);
            using var bitmap = new Bitmap(menu.Width, menu.Height);
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(imagePath);
            using var sender = new AdvertisementSender();
            using var form = new DeviceForm(settings, sender);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-30000, -30000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            using var formBitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(formBitmap, new Rectangle(Point.Empty, formBitmap.Size));
            formBitmap.Save(Path.Combine(Path.GetDirectoryName(imagePath)!, "devices-preview.png"));
            form.Close();
        }
        Console.WriteLine("PASS: ID validation, settings, menu, power colors and mode state; no radio transmissions");
        return 0;
    }
}
