using System.Collections.Concurrent;
using System.Diagnostics;
using Arbor.Common.PLC.XYRestricted;

namespace OptimizationXY.Charger.Services.Plc;

// =============================================================================
// The seam between XyRestrictedPlcService and the PLC.
// -----------------------------------------------------------------------------
// XyRestrictedConnection is the real XY PLC (the XYRestricted library, exactly
// as before). FakeXyPlcConnection is an in-memory PLC with the same five tags,
// used by the dev-PC bench (--bench) and the automated tests, so the whole
// handshake / write / readback logic can run with no PLC. Same idea as the
// Rebuild program's FakePlcBackend.
// =============================================================================

/// <summary>One tag value as the PLC library reports it.</summary>
public sealed record PlcTagUpdate(string TagName, object? FirstValue, object? Values, bool IsGood);

/// <summary>The five-tag XY PLC surface the service uses.</summary>
public interface IXyPlcConnection : IDisposable
{
    /// <summary>True when the PLC is reachable.</summary>
    bool IsConnected { get; }

    /// <summary>Raised (on any thread) when a tag value arrives: polling or a requested read.</summary>
    event EventHandler<PlcTagUpdate>? TagUpdated;

    /// <summary>Requests a read; the value arrives through <see cref="TagUpdated"/>.</summary>
    void ReadTag(XyTag tag);

    /// <summary>Writes one of the writable tags.</summary>
    void WriteTag(XyTag tag, float[] values);
}

/// <summary>The real XY PLC through the XYRestricted library.</summary>
public sealed class XyRestrictedConnection : IXyPlcConnection
{
    private readonly XY _xy;

    /// <summary>Creates the one XY connection (see the XYRestricted consumer guide).</summary>
    public XyRestrictedConnection()
    {
        _xy = new XY();
        _xy.TagDataChanged += OnTagDataChanged;
    }

    public bool IsConnected => _xy.IsConnected;

    public event EventHandler<PlcTagUpdate>? TagUpdated;

    public void ReadTag(XyTag tag) => _xy.ReadTag(tag);

    public void WriteTag(XyTag tag, float[] values) => _xy.WriteTag(tag, values);

    private void OnTagDataChanged(object? sender, TagDataChangedEventArgs e) =>
        TagUpdated?.Invoke(this, new PlcTagUpdate(e.TagName, e.FirstValue, e.Values, e.IsGood));

    public void Dispose()
    {
        _xy.TagDataChanged -= OnTagDataChanged;
        _xy.Dispose();
    }
}

/// <summary>
/// An in-memory XY PLC with the same five tags. Reads answer after
/// <see cref="ReadDelayMilliseconds"/> on a background thread, like the real
/// library. The bench and the tests drive ScanStart / ScanComplete and read
/// back what was written.
/// </summary>
public sealed class FakeXyPlcConnection : IXyPlcConnection
{
    private readonly ConcurrentDictionary<XyTag, object> _values = new();
    private readonly ConcurrentQueue<(XyTag Tag, float[] Values, long Ticks)> _writes = new();

    /// <summary>Starts with ScanStart / ScanComplete low and zeroed result tags.</summary>
    public FakeXyPlcConnection()
    {
        _values[XyTag.ScanStart] = false;
        _values[XyTag.ScanComplete] = false;
        _values[XyTag.ScannedBlockData] = new float[10];
        _values[XyTag.CarriageRetract] = new float[5];
        _values[XyTag.RoundupComplete] = new float[5];
    }

    public bool IsConnected { get; set; } = true;

    /// <summary>How long a requested read takes to answer (ms).</summary>
    public int ReadDelayMilliseconds { get; set; } = 15;

    /// <summary>Test switch: reads of written tags never answer (readback timeout).</summary>
    public bool DropReadbacks { get; set; }

    /// <summary>Test switch: reads of written tags answer with this added to every value (readback mismatch).</summary>
    public float ReadbackError { get; set; }

    /// <summary>Every write, in order, with its Stopwatch time.</summary>
    public IReadOnlyCollection<(XyTag Tag, float[] Values, long Ticks)> Writes => _writes;

    public event EventHandler<PlcTagUpdate>? TagUpdated;

    /// <summary>The PLC tag names written, in order (for tests that do not reference XYRestricted).</summary>
    public IReadOnlyList<string> WrittenTagNames => [.. _writes.Select(w => w.Tag.ToTagName())];

    /// <summary>The last values written to ScannedBlockData (zeros before any write).</summary>
    public float[] LastScannedBlockData => LastWritten(XyTag.ScannedBlockData);

    /// <summary>The last values written to a tag (zeros before any write).</summary>
    public float[] LastWritten(XyTag tag) =>
        _values.TryGetValue(tag, out object? v) && v is float[] f ? (float[])f.Clone() : [];

    /// <summary>Sets ScanStart as the PLC would and reports it (polled tag).</summary>
    public void SetScanStart(bool value) => SetSignal(XyTag.ScanStart, value);

    /// <summary>Sets ScanComplete as the PLC would and reports it (polled tag).</summary>
    public void SetScanComplete(bool value) => SetSignal(XyTag.ScanComplete, value);

    public void ReadTag(XyTag tag)
    {
        bool written = tag is XyTag.ScannedBlockData or XyTag.CarriageRetract or XyTag.RoundupComplete;

        if (written && DropReadbacks)
            return;

        object value = _values.TryGetValue(tag, out object? v) ? v : 0f;

        if (written && ReadbackError != 0 && value is float[] f)
            value = f.Select(x => x + ReadbackError).ToArray();

        int delay = ReadDelayMilliseconds;
        _ = Task.Run(async () =>
        {
            if (delay > 0)
                await Task.Delay(delay).ConfigureAwait(false);

            Raise(tag, value);
        });
    }

    public void WriteTag(XyTag tag, float[] values)
    {
        if (tag is not (XyTag.ScannedBlockData or XyTag.CarriageRetract or XyTag.RoundupComplete))
            throw new InvalidOperationException($"{tag} is read-only.");

        float[] copy = (float[])values.Clone();
        _values[tag] = copy;
        _writes.Enqueue((tag, copy, Stopwatch.GetTimestamp()));
    }

    private void SetSignal(XyTag tag, bool value)
    {
        _values[tag] = value;
        Raise(tag, value);
    }

    private void Raise(XyTag tag, object value)
    {
        object first = value is float[] f && f.Length > 0 ? f[0] : value;
        TagUpdated?.Invoke(this, new PlcTagUpdate(tag.ToTagName(), first, value, IsGood: true));
    }

    public void Dispose()
    {
        IsConnected = false;
    }
}
