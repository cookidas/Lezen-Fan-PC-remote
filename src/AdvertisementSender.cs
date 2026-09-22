using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace LezenTray;

public sealed class AdvertisementSender : IDisposable
{
    private static readonly TimeSpan TransitionTimeout = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private int disposed;
    private bool stopFailed;

    public static async Task<string> GetAdapterInfoAsync()
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask().ConfigureAwait(false);
        if (adapter is null) return Strings.T("err.no_adapter");
        var radio = await adapter.GetRadioAsync().AsTask().ConfigureAwait(false);
        return Strings.T("adapter.describe",
            radio?.Name ?? "Bluetooth",
            radio?.State.ToString() ?? Strings.T("radio.state_unknown"),
            adapter.IsLowEnergySupported, adapter.IsPeripheralRoleSupported, adapter.IsAdvertisementOffloadSupported);
    }

    // Completion means the Windows publisher ran; the fan does not acknowledge this broadcast.
    public async Task SendAsync(ushort companyId, byte[] payload, TimeSpan duration)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(payload);
        // Reserve three bytes for OS flags, plus length/type and the two-byte company ID.
        if (payload.Length is < 1 or > 24)
            throw new ArgumentException(Strings.T("err.payload_length"), nameof(payload));
        if (duration <= TimeSpan.Zero || duration.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(duration));
        var bytes = (byte[])payload.Clone();
        var token = shutdown.Token;
        await gate.WaitAsync(token).ConfigureAwait(false);
        var clock = Stopwatch.StartNew();
        var sendId = Guid.NewGuid().ToString("N")[..8];
        DiagnosticLog.Write("send.request", new { sendId, Company = companyId.ToString("X4"), Payload = Convert.ToHexString(bytes), DurationMs = duration.TotalMilliseconds });
        try
        {
            token.ThrowIfCancellationRequested();
            if (stopFailed)
                throw new InvalidOperationException(Strings.T("err.prev_send_unfinished"));
            var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask().WaitAsync(TransitionTimeout, token).ConfigureAwait(false);
            if (adapter is null || !adapter.IsLowEnergySupported)
                throw new InvalidOperationException(Strings.T("err.no_ble_adapter"));
            var radio = await adapter.GetRadioAsync().AsTask().WaitAsync(TransitionTimeout, token).ConfigureAwait(false);
            if (radio is not null && radio.State != RadioState.On)
                throw new InvalidOperationException(Strings.T("err.bluetooth_off"));
            DiagnosticLog.Write("send.adapter", new { sendId, adapter.IsLowEnergySupported, adapter.IsPeripheralRoleSupported, adapter.IsAdvertisementOffloadSupported, Radio = radio?.State.ToString() });

            using var writer = new DataWriter();
            writer.WriteBytes(bytes);
            var publisher = new BluetoothLEAdvertisementPublisher { UseExtendedAdvertisement = false };
            publisher.Advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(companyId, writer.DetachBuffer()));
            var started = new TaskCompletionSource<BluetoothLEAdvertisementPublisherStatusChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ended = new TaskCompletionSource<BluetoothLEAdvertisementPublisherStatusChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            void StatusChanged(BluetoothLEAdvertisementPublisher _, BluetoothLEAdvertisementPublisherStatusChangedEventArgs e)
            {
                DiagnosticLog.Write("send.status", new { sendId, Status = e.Status.ToString(), Error = e.Error.ToString(), ElapsedMs = clock.ElapsedMilliseconds });
                if (e.Status is BluetoothLEAdvertisementPublisherStatus.Started or BluetoothLEAdvertisementPublisherStatus.Stopped or BluetoothLEAdvertisementPublisherStatus.Aborted)
                    started.TrySetResult(e);
                if (e.Status is BluetoothLEAdvertisementPublisherStatus.Stopped or BluetoothLEAdvertisementPublisherStatus.Aborted)
                    ended.TrySetResult(e);
            }

            publisher.StatusChanged += StatusChanged;
            Exception? failure = null;
            try
            {
                token.ThrowIfCancellationRequested();
                publisher.Start();
                var status = await started.Task.WaitAsync(TransitionTimeout, token).ConfigureAwait(false);
                if (status.Status != BluetoothLEAdvertisementPublisherStatus.Started) throw PublishError(status);
                var delay = Task.Delay(duration, token);
                if (await Task.WhenAny(delay, ended.Task).ConfigureAwait(false) == ended.Task)
                    throw PublishError(await ended.Task.ConfigureAwait(false));
                await delay.ConfigureAwait(false);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try
                {
                    if (publisher.Status is not (BluetoothLEAdvertisementPublisherStatus.Created or BluetoothLEAdvertisementPublisherStatus.Stopped or BluetoothLEAdvertisementPublisherStatus.Aborted))
                    {
                        publisher.Stop();
                        var status = await ended.Task.WaitAsync(TransitionTimeout).ConfigureAwait(false);
                        if (status.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
                            failure ??= PublishError(status);
                    }
                    else if (publisher.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
                        failure ??= ended.Task.IsCompletedSuccessfully
                            ? PublishError(ended.Task.Result)
                            : new InvalidOperationException(Strings.T("err.advert_aborted"));
                }
                catch (Exception ex)
                {
                    stopFailed = true;
                    failure ??= new InvalidOperationException(Strings.T("err.send_unconfirmed"), ex);
                }
                finally { publisher.StatusChanged -= StatusChanged; }
            }
            if (failure is OperationCanceledException) throw new OperationCanceledException(token);
            if (failure is TimeoutException)
                throw new InvalidOperationException(Strings.T("err.send_start_failed"), failure);
            if (failure is not null)
                throw new InvalidOperationException(Strings.T("err.send_failed", failure.Message), failure);
            DiagnosticLog.Write("send.completed", new { sendId, ElapsedMs = clock.ElapsedMilliseconds });
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            DiagnosticLog.Write("send.failed", new { sendId, Error = ex.ToString() });
            throw;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or InvalidOperationException))
        {
            DiagnosticLog.Write("send.failed", new { sendId, Error = ex.ToString() });
            throw new InvalidOperationException(Strings.T("err.adapter_unavailable_detail", ex.Message), ex);
        }
        finally { gate.Release(); }
    }

    private static Exception PublishError(BluetoothLEAdvertisementPublisherStatusChangedEventArgs e) =>
        new InvalidOperationException(e.Error switch
        {
            BluetoothError.NotSupported => Strings.T("err.not_supported"),
            BluetoothError.RadioNotAvailable => Strings.T("err.radio_unavailable"),
            BluetoothError.DisabledByPolicy => Strings.T("err.disabled_by_policy"),
            _ => Strings.T("err.advert_stopped_generic", e.Status, e.Error)
        });

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) shutdown.Cancel();
        // In-flight sends finish stopping the publisher before releasing the gate.
    }
}
