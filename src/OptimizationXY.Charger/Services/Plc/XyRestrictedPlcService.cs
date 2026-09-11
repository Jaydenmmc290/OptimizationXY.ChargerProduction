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

    /// <summary>
    /// Waits for the PLC ScanComplete signal to be asserted (true) with good quality.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the ScanComplete signal.</param>
    /// <returns>A task that completes when the ScanComplete signal is asserted with good quality.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PLC is disconnected or no scan cycle is active.</exception>
    /// <exception cref="TimeoutException">Thrown if the wait times out before the ScanComplete signal is asserted with good quality.</exception>
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

    /// <summary>
    /// Initializes the XYRestricted connection and subscribes to tag change events.
    /// </summary>
    /// <summary>
    /// Initializes the single XYRestricted connection used for the application lifetime.
    /// Before creating XY, the service briefly waits for the required PLC network.
    /// No PLC writes are allowed unless XY reports that it is connected.
    /// </summary>
    public async Task StartAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();

        if (!_settings.Enabled)
        {
            Log("PLC integration is disabled in settings.");
            return;
        }

        // Only one XY instance is allowed to own the connection.
        if (_xy is not null)
            return;

        try
        {
            Log("Waiting for XY PLC network...");

            DateTime timeout =
                DateTime.UtcNow.AddMilliseconds(
                    _settings.StartupNetworkWaitMilliseconds);

            while (!PlcNetwork.IsRequiredNetworkAvailable())
            {
                token.ThrowIfCancellationRequested();

                if (DateTime.UtcNow >= timeout)
                {
                    Log(
                        "XY PLC network was not available before the startup timeout. " +
                        "PLC integration will remain disconnected.");

                    ConnectionStateChanged?.Invoke(this, false);
                    return;
                }

                await Task.Delay(
                    _settings.StartupNetworkCheckIntervalMilliseconds,
                    token);
            }

            Log("XY PLC network detected. Initializing connection.");

            // IMPORTANT:
            // Create exactly one XY instance and retain it for the
            // application lifetime, per the XYRestricted consumer guide.
            _xy = new XY();
            _xy.TagDataChanged += OnTagDataChanged;

            bool connected = _xy.IsConnected;

            ConnectionStateChanged?.Invoke(this, connected);

            if (!connected)
            {
                Log(
                    "XYRestricted initialized but reports disconnected. " +
                    "PLC writes remain blocked.");

                return;
            }

            Log("XYRestricted PLC connection initialized.");

            // Establish current input state.
            // These results return through TagDataChanged.
            _xy.ReadTag(XyTag.ScanStart);
            _xy.ReadTag(XyTag.ScanComplete);
        }
        catch (OperationCanceledException)
        {
            Log("PLC initialization was cancelled.");
            ConnectionStateChanged?.Invoke(this, false);
        }
        catch (InvalidOperationException ex)
        {
            // The guide specifically identifies InvalidOperationException
            // as an initialization failure such as a licensing problem.
            Logger.Error(ex, "PLC initialization failed.");
            Log($"PLC initialization failed: {ex.Message}");

            DisposeConnection();

            ConnectionStateChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected PLC initialization failure.");
            Log($"Unexpected PLC initialization failure: {ex.Message}");

            DisposeConnection();

            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Sends a payload to the PLC by writing the specified tags and optionally verifying the readback values.
    /// </summary>
    /// <param name="payload">The payload containing the tags and values to write to the PLC.</param>
    /// <param name="token">A cancellation token to observe while sending the payload.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a PlcTransmissionRecord with the details of the transmission.</returns>
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

            // Spindle rotation tag write support.
            // SpindleRotate is a single REAL (float) value written when kinematics rotation is enabled.
            // This write occurs after ScannedBlockData to maintain the PLC write order.
            if (_settings.EnableRotationTagWrite && payload.SpindleRotationDegrees != 0.0)
            {
                try
                {
                    // Write single float to SpindleRotate tag
                    _xy.WriteTag(XyTag.SpindleRotate, payload.SpindleRotateValue);
                    written.Add(XyTag.SpindleRotate.ToTagName());
                    Log($"PLC write: SpindleRotate [{payload.SpindleRotateValue:F3}]");

                    if (_settings.RequireReadbackVerification)
                    {
                        // Readback verification for single float value
                        var tcs = new TaskCompletionSource<TagDataChangedEventArgs>(
                            TaskCreationOptions.RunContinuationsAsynchronously);

                        string tagName = XyTag.SpindleRotate.ToTagName();
                        if (!_pendingReadbacks.TryAdd(tagName, tcs))
                        {
                            throw new InvalidOperationException(
                                $"A readback request is already pending for {tagName}.");
                        }

                        try
                        {
                            _xy.ReadTag(XyTag.SpindleRotate);

                            await tcs.Task.WaitAsync(
                                TimeSpan.FromMilliseconds(_settings.ReadbackTimeoutMilliseconds),
                                token).ConfigureAwait(false);
                        }
                        finally
                        {
                            _pendingReadbacks.TryRemove(tagName, out _);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "SpindleRotate tag write or readback failed.");
                    throw;
                }
            }

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

    /// <summary>
    /// Writes the specified tag and values to the PLC, and optionally verifies the readback values if required by settings.
    /// </summary>
    /// <param name="tag">The PLC tag to write.</param>
    /// <param name="values">The values to write to the tag.</param>
    /// <param name="written">A list to track the names of tags that have been written.</param>
    /// <param name="token">A cancellation token to observe while writing and verifying the tag.</param>
    /// <returns>A task that represents the asynchronous write and verify operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the PLC connection is lost or the readback quality is bad.</exception>
    /// <exception cref="TimeoutException">Thrown when the readback operation times out.</exception>
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

    /// <summary>
    /// Handles tag data change events from the XYRestricted PLC connection. Updates internal state and raises events for ScanStart and ScanComplete changes.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
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

    /// <summary>
    /// Flattens the numeric values from the TagDataChangedEventArgs into a single array of doubles, handling nested collections and string representations of numbers.
    /// </summary>
    /// <param name="e">The event data containing the values to flatten.</param>
    /// <returns>An array of doubles representing the flattened numeric values.</returns>
    private static double[] FlattenNumericValues(TagDataChangedEventArgs e)
    {
        var values = new List<double>();
        AddNumeric(e.Values, values);

        if (values.Count == 0)
            AddNumeric(e.FirstValue, values);

        return [.. values];
    }

    /// <summary>
    /// Recursively adds numeric values from the provided object to the output list, handling strings, collections, and convertible types.
    /// </summary>
    /// <param name="value">The value to process and extract numeric values from.</param>
    /// <param name="output">The list to which numeric values will be added.</param>
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

    /// <summary>
    /// Compares two arrays of numeric values (expected and actual) to determine if they match within a specified tolerance.
    /// Returns true if the arrays are of equal length and all corresponding elements are within the tolerance; otherwise, returns false.
    /// </summary>
    /// <param name="expected">The array of expected values.</param>
    /// <param name="actual">The array of actual values.</param>
    /// <param name="tolerance">The tolerance within which corresponding elements must match.</param>
    /// <returns>True if the arrays match within the specified tolerance; otherwise, false.</returns>
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

    /// <summary>
    /// Converts an object to a boolean value. If the object is a boolean, it returns its value. If the object is null, it returns false. If the object can be parsed as a boolean string, it returns the parsed value. Otherwise, it attempts to
    /// convert the object to a double and returns true if the absolute value is greater than 0.5; otherwise, it returns false.
    /// </summary>
    /// <param name="value">The value to convert to a boolean.</param>
    /// <returns>True if the value represents a boolean true or a numeric value greater than 0.5; otherwise, false.</returns>
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

    /// <summary>
    /// Logs a message using the NLog logger and raises the DiagnosticsMessage event with the same message.
    /// </summary>
    /// <param name="message">The message to log and raise as a diagnostics event.</param>
    private void Log(string message)
    {
        Logger.Info(message);
        DiagnosticsMessage?.Invoke(this, message);
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the service has been disposed. This method is called at the
    /// beginning of public methods to ensure that operations are not performed on a disposed instance.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Disposes of the XYRestricted connection and unsubscribes from tag change events.
    /// This method is called during disposal to clean up resources associated with the PLC connection.
    /// </summary>
    private void DisposeConnection()
    {
        if (_xy is null)
            return;

        _xy.TagDataChanged -= OnTagDataChanged;
        _xy.Dispose();
        _xy = null;
    }

    /// <summary>
    /// Disposes of the XyRestrictedPlcService instance, releasing all resources and notifying any
    /// pending readback or scan complete waiters of the disposal.
    /// </summary>
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
