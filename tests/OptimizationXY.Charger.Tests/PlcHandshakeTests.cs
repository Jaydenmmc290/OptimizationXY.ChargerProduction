using OptimizationXY.Charger.Models.Plc;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Plc;
using OptimizationXY.Charger.Services.Replay;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The real XyRestrictedPlcService against the in-memory PLC: the ScanStart /
/// ScanComplete handshake, the write order and the readback check.
/// </summary>
public class PlcHandshakeTests
{
    private static PlcIntegrationSettings Settings() => new()
    {
        Enabled = true,
        RequireScanCompleteBeforeProcessing = true,
        RequireReadbackVerification = true,
        VerifyReadbackAfterAllWrites = true,
        ReadbackTimeoutMilliseconds = 1000,
        ScanCompleteTimeoutMilliseconds = 2000,
        WriteCarriageRetractFirst = true
    };

    private static PlcCommandPayload Payload() => new()
    {
        BlockCount = 900001,
        HeadXMM = 0.25,
        HeadYMM = -0.10,
        TailXMM = 0.40,
        TailYMM = 0.05,
        SmallDiameterMM = 17.0,
        LargeDiameterMM = 18.2,
        LargestMeasuredRadiusMM = 9.1,
        SmallestMeasuredRadiusMM = 8.5,
        KnifeClearanceMM = 1.26,
        SafeCarriageRetractRadiusMM = 10.4
    };

    private static async Task<(XyRestrictedPlcService Plc, FakeXyPlcConnection Fake)> Connect(PlcIntegrationSettings settings)
    {
        var fake = new FakeXyPlcConnection();
        var plc = new XyRestrictedPlcService(settings, () => fake);
        await ProductionBench.StartAsync(plc, fake);
        return (plc, fake);
    }

    [Fact]
    public async Task ScanCompleteAfterAScanStartEdgeEndsTheWait()
    {
        (XyRestrictedPlcService plc, FakeXyPlcConnection fake) = await Connect(Settings());
        using var _ = plc;

        fake.SetScanStart(true);
        Assert.True(plc.IsScanCycleActive);

        Task wait = plc.WaitForScanCompleteAsync();
        fake.SetScanComplete(true);

        await wait.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AScanStartAlreadyHighAtStartUpIsNotANewCycle()
    {
        var fake = new FakeXyPlcConnection();
        fake.SetScanStart(true);

        using var plc = new XyRestrictedPlcService(Settings(), () => fake);
        await ProductionBench.StartAsync(plc, fake);

        Assert.False(plc.IsScanCycleActive);
    }

    [Fact]
    public async Task AllThreeTagsAreWrittenInOrderAndVerified()
    {
        (XyRestrictedPlcService plc, FakeXyPlcConnection fake) = await Connect(Settings());
        using var _ = plc;

        PlcCommandPayload payload = Payload();
        PlcTransmissionRecord record = await plc.SendPayloadAsync(payload);

        Assert.True(record.Success, record.Message);
        Assert.True(record.ReadbackVerified);
        Assert.Equal(
            ["TruLog_W_CarriageRetract", "TruLog_W_RoundupComplete", "TruLog_W_ScannedBlockData"],
            fake.WrittenTagNames);
        Assert.Equal(payload.ScannedBlockData, fake.LastScannedBlockData);
    }

    [Fact]
    public async Task AReadbackMismatchFailsTheSend()
    {
        (XyRestrictedPlcService plc, FakeXyPlcConnection fake) = await Connect(Settings());
        using var _ = plc;
        fake.ReadbackError = 0.5f;

        PlcTransmissionRecord record = await plc.SendPayloadAsync(Payload());

        Assert.False(record.Success);
        Assert.Contains("mismatch", record.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AMissingReadbackTimesOut()
    {
        PlcIntegrationSettings settings = Settings();
        settings.ReadbackTimeoutMilliseconds = 200;
        (XyRestrictedPlcService plc, FakeXyPlcConnection fake) = await Connect(settings);
        using var _ = plc;
        fake.DropReadbacks = true;

        PlcTransmissionRecord record = await plc.SendPayloadAsync(Payload());

        Assert.False(record.Success);
        Assert.Contains("Timed out", record.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerTagVerificationStillWorks()
    {
        PlcIntegrationSettings settings = Settings();
        settings.VerifyReadbackAfterAllWrites = false;
        (XyRestrictedPlcService plc, FakeXyPlcConnection fake) = await Connect(settings);
        using var _ = plc;

        PlcTransmissionRecord record = await plc.SendPayloadAsync(Payload());

        Assert.True(record.Success, record.Message);
        Assert.Equal(3, fake.Writes.Count);
    }
}
