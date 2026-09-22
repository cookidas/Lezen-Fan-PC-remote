using System.Text.Json;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace LezenTray;

internal static class AdvertisementCapture
{
    internal static async Task RunAsync(string path, int seconds)
    {
        if (seconds is < 5 or > 180) throw new ArgumentOutOfRangeException(nameof(seconds));
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter is null) throw new InvalidOperationException("블루투스 어댑터가 없습니다.");
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var captured = new HashSet<string>();
        var gate = new object();
        string? failure = null;
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        byte[] Read(IBuffer buffer)
        {
            using var reader = DataReader.FromBuffer(buffer);
            var bytes = new byte[buffer.Length];
            reader.ReadBytes(bytes);
            return bytes;
        }
        watcher.Received += (_, e) =>
        {
            var manufacturers = e.Advertisement.ManufacturerData.Where(m => m.CompanyId == FanProtocol.CompanyId).ToArray();
            if (manufacturers.Length == 0) return;
            lock (gate)
            {
                foreach (var manufacturer in manufacturers)
                {
                    string payload = Convert.ToHexString(Read(manufacturer.Data));
                    if (!captured.Add($"{e.BluetoothAddress:X12}:{payload}")) continue;
                    var sections = e.Advertisement.DataSections.Select(s => new { Type = s.DataType.ToString("X2"), Data = Convert.ToHexString(Read(s.Data)) }).ToArray();
                    var record = new
                    {
                        Time = e.Timestamp, Address = e.BluetoothAddress.ToString("X12"), Rssi = e.RawSignalStrengthInDBm,
                        Type = e.AdvertisementType.ToString(), Flags = e.Advertisement.Flags?.ToString(), Payload = payload, Sections = sections
                    };
                    try { File.AppendAllText(path, JsonSerializer.Serialize(record) + Environment.NewLine); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failure = ex.Message; }
                    Console.WriteLine(JsonSerializer.Serialize(record));
                }
            }
        };
        watcher.Stopped += (_, e) =>
        {
            if (e.Error != BluetoothError.Success) lock (gate) failure = e.Error.ToString();
        };
        watcher.Start();
        Console.WriteLine($"READY: {seconds}초 동안 My LEZEN(FFF0) 신호 수신. 휴대폰 원본 앱의 버튼을 누르세요.");
        try { await Task.Delay(TimeSpan.FromSeconds(seconds)); }
        finally { watcher.Stop(); }
        lock (gate)
        {
            if (failure is not null) throw new InvalidOperationException(failure);
            Console.WriteLine($"DONE: 서로 다른 패킷 {captured.Count}개 · {path}");
        }
    }
}
