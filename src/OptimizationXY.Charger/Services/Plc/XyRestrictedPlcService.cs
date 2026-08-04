using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using Arbor.Common.PLC.XYRestricted;
using NLog;
using OptimizationXY.Charger.Models.Plc;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Plc;

/// <summary>
/// Owns the single XYRestricted connection for the application lifetime.
/// The restricted surface exposes only the five approved tags. This service
/// never writes ScanStart or ScanComplete and contains no rotation command.
/// </summary>
public sealed class XyRestrictedPlcService(PlcIntegrationSettings settings) : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string ScanStartName = XyTag.ScanStart.ToTagName();
    private static readonly string ScanCompleteName = XyTag.ScanComplete.ToTagName();

    private readonly PlcIntegrationSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly ConcurrentDictionary<string, TaskCompletionSource<TagDataChangedEventArgs>>
        _pendingReadbacks = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _scanStateLock = new();

    private XY? _xy;
    private bool _hasScanStartState;
    private bool _lastScanStart;
    private bool _scanCycleActive;
    private bool _scanCompleteState;
    private bool _scanCompleteGood;
    private TaskCompletionSource<bool>? _scanCompleteWaiter;
    private bool _disposed;

    public event EventHandler<string>? DiagnosticsMessage;
    public event EventHandler? ScanStartRequested;
    public event EventHandler<bool>? ScanStartChanged;
    public event EventHandler<bool>? ScanCompleteChanged;
    public event EventHandler<bool>? ConnectionStateChanged;

    public bool IsConnected => _xy?.IsConnected == true;

    public bool IsScanCycleActive
    {
        get
        {
            lock (_scanStateLock)
                return _scanCycleActive;
        }
    }

    public async Task WaitForScanCompleteAsync(
        CancellationToken token = default)
    {
        ThrowIfDisposed();

        if (!_settings.RequireScanCompleteBeforeProcessing)
            return;

        if (_xy is null || !_xy.IsConnected)
        {
            throw new InvalidOperationException(
                "PLC is disconnected; ScanComplete cannot be verified.");
        }

        Task waitTask;

        lock (_scanStateLock)
        {
            if (!_scanCycleActive)
            {
                throw new InvalidOperationException(
                    "No PLC ScanStart cycle is active. A fresh low-to-high ScanStart edge is required before production capture.");
            }

            if (_scanCompleteState &&
                (!_settings.RequireGoodQuality || _scanCompleteGood))
            {
                return;
            }

            _scanCompleteWaiter ??=
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            waitTask = _scanCompleteWaiter.Task;
        }

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(_settings.ScanCompleteTimeoutMilliseconds);

        try
        {
            await waitTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out after {_settings.ScanCompleteTimeoutMilliseconds} ms waiting for a good PLC ScanComplete signal.");
        }
    }

    public void Start()
    {
        ThrowIfDisposed();

        if (!_settings.Enabled)
        {
            Log("PLC integration is disabled in settings.");
            return;
        }

        if (_xy is not null)
            return;

        try
        {
            _xy = new XY();
            _xy.TagDataChanged += OnTagDataChanged;

            bool connected = _xy.IsConnected;
            ConnectionStateChanged?.Invoke(this, connected);

            if (!connected)
            {
                Log("XYRestricted initialized in disconnected mode. PLC writes remain blocked.");
                return;
            }

            Log("XYRestricted PLC connection initialized.");

            // Establish current input state. Results arrive through the event.
            _xy.ReadTag(XyTag.ScanStart);
            _xy.ReadTag(XyTag.ScanComplete);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PLC initialization failed.");
            Log($"PLC initialization failed: {ex.Message}");
            DisposeConnection();
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    public async Task<PlcTransmissionRecord> SendPayloadAsync(
        PlcCommandPayload payload,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ThrowIfDisposed();

        var record = new PlcTransmissionRecord
        {
            AttemptedUtc = DateTime.UtcNow
        };

        if (!_settings.Enabled)
        {
            record.Message = "PLC integration is disabled.";
            return record;
        }

        if (_xy is null || !_xy.IsConnected)
        {
            record.Message = "PLC is disconnected. No tags were written.";
            return record;
        }

        var written = new List<string>();

        try
        {
            if (_settings.WriteCarriageRetractFirst)
            {
                await WriteAndVerifyAsync(
                    XyTag.CarriageRetract,
                    payload.CarriageRetract,
                    written,
                    token).ConfigureAwait(false);

                await WriteAndVerifyAsync(
                    XyTag.RoundupComplete,
                    payload.RoundupComplete,
                    written,
                    token).ConfigureAwait(false);
            }

            // ScannedBlockData is written last and acts as the complete result packet.
            await WriteAndVerifyAsync(
                XyTag.ScannedBlockData,
                payload.ScannedBlockData,
                written,
                token).ConfigureAwait(false);

            if (!_settings.WriteCarriageRetractFirst)
            {
                await WriteAndVerifyAsync(
                    XyTag.CarriageRetract,
                    payload.CarriageRetract,
                    written,
                    token).ConfigureAwait(false);

                await WriteAndVerifyAsync(
                    XyTag.RoundupComplete,
                    payload.RoundupComplete,
                    written,
                    token).ConfigureAwait(false);
            }

            record.Success = true;
            record.ReadbackVerified = _settings.RequireReadbackVerification;
            record.TagsWritten = [.. written];
            record.Message = _settings.RequireReadbackVerification
                ? "All restricted PLC payloads were written and read back successfully."
                : "All restricted PLC payloads were written; readback verification was disabled.";

            Log(record.Message);
            return record;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "PLC payload transmission failed.");
            record.Success = false;
            record.ReadbackVerified = false;
            record.TagsWritten = [.. written];
            record.Message = $"PLC payload transmission failed: {ex.Message}";
            Log(record.Message);
            return record;
        }
    }

    private async Task WriteAndVerifyAsync(
        XyTag tag,
        float[] values,
        List<string> written,
        CancellationToken token)
    {
        if (_xy is null || !_xy.IsConnected)
            throw new InvalidOperationException("PLC connection was lost before write.");

        token.ThrowIfCancellationRequested();

        string tagName = tag.ToTagName();
        _xy.WriteTag(tag, values);
        written.Add(tagName);
        Log($"PLC write: {tagName} [{string.Join(", ", values.Select(v => v.ToString("F3", CultureInfo.InvariantCulture)))}]");

        if (!_settings.RequireReadbackVerification)
            return;

        var tcs = new TaskCompletionSource<TagDataChangedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingReadbacks.TryAdd(tagName, tcs))
        {
            throw new InvalidOperationException(
                $"A readback request is already pending for {tagName}.");
        }

        try
        {
            _xy.ReadTag(tag);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_settings.ReadbackTimeoutMilliseconds);

            TagDataChangedEventArgs readback = await tcs.Task
                .WaitAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            if (_settings.RequireGoodQuality && !readback.IsGood)
            {
                throw new InvalidOperationException(
                    $"PLC readback quality was bad for {tagName}.");
            }

            double[] actual = FlattenNumericValues(readback);
            if (!ValuesMatch(values, actual, _settings.FloatComparisonTolerance))
            {
                throw new InvalidOperationException(
                    $"PLC readback mismatch for {tagName}. " +
                    $"Expected [{string.Join(", ", values)}], " +
                    $"received [{string.Join(", ", actual.Select(v => v.ToString("F3", CultureInfo.InvariantCulture)))}].");
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out waiting for PLC readback of {tagName}.");
        }
        finally
        {
            _pendingReadbacks.TryRemove(tagName, out _);
        }
    }

    private void OnTagDataChanged(object? sender, TagDataChangedEventArgs e)
    {
        if (_disposed)
            return;

        if (_pendingReadbacks.TryGetValue(e.TagName, out var readbackWaiter))
            readbackWaiter.TrySetResult(e);

        if (string.Equals(e.TagName, ScanStartName, StringComparison.OrdinalIgnoreCase))
        {
            if (_settings.RequireGoodQuality && !e.IsGood)
            {
                Log("PLC ScanStart update ignored because tag quality was bad.");
                return;
            }

            bool current = ToBoolean(e.FirstValue);

            // The first read establishes the input baseline. A stale high value
            // at application startup must never be interpreted as a new cycle.
            if (!_hasScanStartState)
            {
                _hasScanStartState = true;
                _lastScanStart = current;
                ScanStartChanged?.Invoke(this, current);
                Log($"PLC ScanStart baseline established at {current}; no capture was started.");
                return;
            }

            bool risingEdge = current && !_lastScanStart;
            _lastScanStart = current;
            ScanStartChanged?.Invoke(this, current);

            if (risingEdge)
            {
                lock (_scanStateLock)
                {
                    _scanCycleActive = true;
                    _scanCompleteState = false;
                    _scanCompleteGood = false;

                    _scanCompleteWaiter?.TrySetException(
                        new InvalidOperationException(
                            "A new PLC ScanStart cycle replaced the previous pending cycle."));
                    _scanCompleteWaiter = null;
                }

                Log("PLC ScanStart rising edge received; a new capture cycle is active.");
                ScanStartRequested?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (string.Equals(e.TagName, ScanCompleteName, StringComparison.OrdinalIgnoreCase))
        {
            if (_settings.RequireGoodQuality && !e.IsGood)
            {
                Log("PLC ScanComplete update ignored because tag quality was bad.");
                return;
            }

            bool current = ToBoolean(e.FirstValue);
            TaskCompletionSource<bool>? scanCompleteWaiter = null;

            lock (_scanStateLock)
            {
                _scanCompleteState = current;
                _scanCompleteGood = e.IsGood;

                if (current && _scanCycleActive)
                {
                    scanCompleteWaiter = _scanCompleteWaiter;
                    _scanCompleteWaiter = null;
                }
            }

            scanCompleteWaiter?.TrySetResult(true);
            ScanCompleteChanged?.Invoke(this, current);
            Log($"PLC ScanComplete changed to {current} (quality good={e.IsGood}).");
        }
    }

    private static double[] FlattenNumericValues(TagDataChangedEventArgs e)
    {
        var values = new List<double>();
        AddNumeric(e.Values, values);

        if (values.Count == 0)
            AddNumeric(e.FirstValue, values);

        return [.. values];
    }

    private static void AddNumeric(object? value, List<double> output)
    {
        if (value is null)
            return;

        if (value is string)
        {
            if (double.TryParse(
                    value.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsed))
            {
                output.Add(parsed);
            }
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
                AddNumeric(item, output);
            return;
        }

        try
        {
            output.Add(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (
            ex is InvalidCastException or
                  FormatException or
                  OverflowException)
        {
            // Non-numeric metadata is ignored.
        }
    }

    private static bool ValuesMatch(
         float[] expected,
        double[] actual,
        double tolerance)
    {
        if (actual.Length != expected.Length)
            return false;

        for (int index = 0; index < expected.Length; index++)
        {
            if (Math.Abs(expected[index] - actual[index]) > tolerance)
                return false;
        }

        return true;
    }

    private static bool ToBoolean(object? value)
    {
        if (value is bool boolean)
            return boolean;

        if (value is null)
            return false;

        if (bool.TryParse(value.ToString(), out bool parsedBoolean))
            return parsedBoolean;

        try
        {
            return Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > 0.5;
        }
        catch
        {
            return false;
        }
    }

    private void Log(string message)
    {
        Logger.Info(message);
        DiagnosticsMessage?.Invoke(this, message);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void DisposeConnection()
    {
        if (_xy is null)
            return;

        _xy.TagDataChanged -= OnTagDataChanged;
        _xy.Dispose();
        _xy = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var waiter in _pendingReadbacks.Values)
        {
            waiter.TrySetException(
                new ObjectDisposedException(nameof(XyRestrictedPlcService)));
        }

        _pendingReadbacks.Clear();

        lock (_scanStateLock)
        {
            _scanCompleteWaiter?.TrySetException(
                new ObjectDisposedException(nameof(XyRestrictedPlcService)));
            _scanCompleteWaiter = null;
            _scanCycleActive = false;
        }

        DisposeConnection();
    }
}
