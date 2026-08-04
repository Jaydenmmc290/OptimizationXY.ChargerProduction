# Using the XYRestricted library from a release DLL

This guide shows how to consume the compiled `XYRestricted` library in a **standalone project** — one
that references the release DLL directly, without the library's source in your solution. It covers
referencing the DLL and its dependencies, configuring logging, and using the API: the `XyTag` enum,
reading and writing tags, and hooking into the change event. The examples mirror the bundled `DemoApp`.

The library's public surface is deliberately free of ASComm types, so your code only ever touches
`XYRestricted` types.

## Prerequisites

- **.NET 10** SDK to build, runtime to run.
- **Windows.** `XYRestricted` targets `net10.0`, but it reaches the PLC through the ASComm SDK, which is
  Windows-only; a WinForms consumer targets `net10.0-windows`.
- **ASComm IoT 1.5** available on the machine that runs your app. The library carries an embedded
  Developer Edition license, but Developer Edition licensing is intended for development — confirm the
  deployment/runtime licensing terms with Automated Solutions before shipping to production stations.
- **Network access to `172.16.0.0/16`**, where the XY PLC lives (device path `172.16.60.2`). Without it
  the library logs a warning and runs disconnected rather than failing (see *Create and dispose*).

## What's in the release

Build `XYRestricted` in Release; its output folder (`bin\Release\net10.0\`) contains the assemblies you
ship with your app:

| File | Role | Needed at |
|------|------|-----------|
| `XYRestricted.dll` | the library | compile + run |
| `NLog.dll` | logging dependency | run |
| `AutomatedSolutions.ASCommStd.dll` | the ASComm SDK assembly | run |

Copy those (the whole folder is fine) into your consuming project — for example, a `lib\XYRestricted\`
folder.

## Add the reference

Because you're referencing a **DLL directly** — not a project or NuGet package — MSBuild doesn't know
about the library's dependencies and won't copy them for you. That's the most common mistake here:
reference only `XYRestricted.dll` and your app builds fine, then throws `FileNotFoundException` for
`NLog` (or the ASComm assembly) the moment you create an `XY`. So reference all three, which also
ensures they're copied next to your executable:

```xml
<ItemGroup>
  <Reference Include="XYRestricted">
    <HintPath>lib\XYRestricted\XYRestricted.dll</HintPath>
  </Reference>
  <Reference Include="NLog">
    <HintPath>lib\XYRestricted\NLog.dll</HintPath>
  </Reference>
  <Reference Include="AutomatedSolutions.ASCommStd">
    <HintPath>lib\XYRestricted\AutomatedSolutions.ASCommStd.dll</HintPath>
  </Reference>
</ItemGroup>
```

You only write code against `XYRestricted.dll`; the other two just have to be present at runtime, and
referencing them is the simplest way to get them copied to your output folder.

A minimal console project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="XYRestricted"><HintPath>lib\XYRestricted\XYRestricted.dll</HintPath></Reference>
    <Reference Include="NLog"><HintPath>lib\XYRestricted\NLog.dll</HintPath></Reference>
    <Reference Include="AutomatedSolutions.ASCommStd"><HintPath>lib\XYRestricted\AutomatedSolutions.ASCommStd.dll</HintPath></Reference>
  </ItemGroup>
</Project>
```

(`net10.0-windows` because the ASComm SDK is Windows-only.)

## Configure logging (optional but recommended)

`XYRestricted` logs through NLog but doesn't ship a configuration — the host app owns that. Without a
config you simply get no log output; the library still works. To capture its logs, add an `NLog.config`
to your project and copy it to the output folder:

```xml
<ItemGroup>
  <None Update="NLog.config">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

A simple `NLog.config`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target xsi:type="Console" name="console"
            layout="${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}" />
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="console" />
  </rules>
</nlog>
```

NLog loads this from your executable's folder automatically.

## Using the library

### Create and dispose

`XY` opens the PLC connection in its constructor and implements `IDisposable`:

```csharp
using Arbor.Common.PLC.XYRestricted;

using var xy = new XY();   // connects now; disposes (closing the channel) at end of scope
```

The XY PLC is on the `172.16.0.0/16` network. If that network isn't reachable from the host, `new XY()`
does **not** throw — it logs a warning and returns a disconnected instance (`xy.IsConnected == false`),
and reads and writes are skipped, so your application keeps running. If the network *is* present but the
connection still can't initialize (license failure, and so on), the constructor throws
`InvalidOperationException`. Check `xy.IsConnected` to branch on connection state.

### The tag enum

Tags are exposed as the `XyTag` enum, so you get IntelliSense and compile-time checking instead of
hand-typed strings:

| `XyTag` | PLC tag | Read | Write |
|---------|---------|------|-------|
| `ScanStart` | `TruLog_R_ScanStart` | yes | no |
| `ScanComplete` | `TruLog_R_ScanComplete` | yes | no |
| `ScannedBlockData` | `TruLog_W_ScannedBlockData` | yes | yes |
| `CarriageRetract` | `TruLog_W_CarriageRetract` | yes | yes |
| `RoundupComplete` | `TruLog_W_RoundupComplete` | yes | yes |

`XyTag.ScanComplete.ToTagName()` gives the underlying string if you ever need it. At runtime you can
also enumerate `xy.ReadableTags`, `xy.WritableTags`, or `Enum.GetValues<XyTag>()`.

### Reading tags

`ReadTag` requests a read; the value comes back through the `TagDataChanged` event (below) rather than
as a return value:

```csharp
xy.ReadTag(XyTag.ScanComplete);
```

Reading a tag that isn't part of the read surface throws `KeyNotFoundException`.

### Writing tags

```csharp
xy.WriteTag(XyTag.ScannedBlockData, blockData);   // allowed
```

Only the three `_W_` tags are writable. Writing a read-only tag throws `UnauthorizedTagWriteException`
(which carries `AttemptedTag` and the `WritableTags` set) — the restriction is enforced by the library,
not just by convention:

```csharp
xy.WriteTag(XyTag.ScanComplete, true);   // throws UnauthorizedTagWriteException
```

### Hooking into the change event

`TagDataChanged` fires whenever a tag's value changes — from the automatic polling of the scan tags, or
from a `ReadTag` you request. `TagDataChangedEventArgs` gives you the tag name, the value(s), a
`FirstValue` convenience accessor, and an `IsGood` quality flag, with no ASComm types involved:

```csharp
xy.TagDataChanged += (sender, e) =>
{
    Console.WriteLine($"{e.TagName} = {e.FirstValue} (good={e.IsGood})");
};
```

To react to just one tag, compare against its resolved name:

```csharp
string scanComplete = XyTag.ScanComplete.ToTagName();

xy.TagDataChanged += (sender, e) =>
{
    if (e.TagName != scanComplete) return;
    // ScanComplete is a BOOL; e.FirstValue is its state
    Console.WriteLine($"Scan complete: {e.FirstValue}");
};
```

**Threading:** the event can arrive on a background polling thread. In a UI app you must marshal back to
the UI thread before touching controls (the WinForms example below shows this). In a console app it
doesn't matter.

## Complete example — console

```csharp
using Arbor.Common.PLC.XYRestricted;

using var xy = new XY();

string scanComplete = XyTag.ScanComplete.ToTagName();

xy.TagDataChanged += (sender, e) =>
{
    if (e.TagName == scanComplete)
        Console.WriteLine($"Scan complete: {e.FirstValue} (good={e.IsGood})");
};

// Trigger a read; the handler above prints the result.
xy.ReadTag(XyTag.ScanComplete);

// Write a result back (allowed - ScannedBlockData is a writable tag).
xy.WriteTag(XyTag.ScannedBlockData, new float[] { 1.0f, 2.0f, 3.0f });

Console.WriteLine("Listening. Press Enter to exit.");
Console.ReadLine();
```

## Complete example — WinForms (mirrors the demo)

```csharp
using Arbor.Common.PLC.XYRestricted;

namespace MyApp
{
    public partial class Form1 : Form
    {
        private static readonly string ScanCompleteTag = XyTag.ScanComplete.ToTagName();

        private readonly XY _xy;

        public Form1()
        {
            InitializeComponent();

            _xy = new XY();
            _xy.TagDataChanged += OnScanCompleteChanged;
        }

        private void OnScanCompleteChanged(object? sender, TagDataChangedEventArgs e)
        {
            if (e.TagName != ScanCompleteTag) return;

            // The event may arrive on a background thread; marshal to the UI thread.
            if (InvokeRequired)
            {
                BeginInvoke(() => OnScanCompleteChanged(sender, e));
                return;
            }

            scanCompleteLabel.Text = e.IsGood
                ? $"Scan complete: {e.FirstValue}"
                : "Scan complete: (bad quality)";
        }

        private void readButton_Click(object sender, EventArgs e)
            => _xy.ReadTag(XyTag.ScanComplete);

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _xy.TagDataChanged -= OnScanCompleteChanged;
            _xy.Dispose();
        }
    }
}
```

## Exceptions to handle

| Exception | When |
|-----------|------|
| `InvalidOperationException` | `new XY()` couldn't initialize while the `172.16.0.0/16` network was present (e.g. license failure). If that network is absent, `XY` runs disconnected instead of throwing. |
| `UnauthorizedTagWriteException` | `WriteTag` on a read-only tag. Carries `AttemptedTag` and `WritableTags`. |
| `KeyNotFoundException` | `ReadTag`/`WriteTag` with a string tag that isn't part of the surface. |

## Things to keep in mind

- **Ship the dependencies.** `XYRestricted.dll`, `NLog.dll`, and `AutomatedSolutions.ASCommStd.dll` must
  all sit next to your executable. Referencing them (above) handles this for a normal build and
  `dotnet publish`.
- **Licensing.** The embedded license is ASComm Developer Edition; confirm runtime/deployment licensing
  with Automated Solutions before deploying to machines other than the development box.
- **Fixed target.** The device path and tag set are compiled into the library; this release talks to the
  XY PLC at `172.16.60.2` and its five tags.
- **Network requirement.** The library needs the `172.16.0.0/16` network. Without it, `XY` logs a
  warning and runs disconnected (`IsConnected == false`) instead of failing; reads and writes are
  skipped. `PlcNetwork.IsRequiredNetworkAvailable()` runs the same check if you want to test it yourself.
- **One instance owns the connection.** Create one `XY`, keep it for the life of your app, and dispose
  it on shutdown.
