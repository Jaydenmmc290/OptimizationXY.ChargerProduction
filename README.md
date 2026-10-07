# OptimizationXY Charger

Windows Forms software for a veneer-lathe XY charger. Two cameras scan a rotating log, the program builds
its 3D shape, works out the best spindle position for the head and tail, and writes it to the PLC.

## Where things are

| Folder | What is in it |
|---|---|
| `src/OptimizationXY.Charger/` | The application source code. |
| `docs/` | All documentation. Start at [docs/README.md](docs/README.md). |
| `config/` | Settings files to start from: the production template and a ready-made production file. |
| `database/` | The SQL schema for the scan database. |
| `lib/` | Supplied DLLs the build needs: `XYRestricted` (PLC library) and `Sapera8` (camera library for Sapera 8 builds). |
| `scripts/` | PowerShell scripts: host validation, and the older build and ZIP-publish scripts. |

Inside `src/OptimizationXY.Charger/`:

| Folder | What is in it |
|---|---|
| `UI/` | The main form, the other forms (`Forms/`) and custom controls (`Controls/`). |
| `Services/` | The work: camera acquisition, calibration, geometry, optimization, PLC, production checks, saving. |
| `Models/` | The data classes the services pass around, and the settings (`Models/Settings`). |
| `Visualization/` | Builders for the pictures saved with a scan and shown on the forms. |
| `Kinematic/` | The kinematic position form and its calculations. |
| `Infrastructure/` | Where the program keeps its data on disk. |

## Building

Open `OptimizationXY.Charger.slnx` in Visual Studio and build (x64). The `dotnet` command line cannot build
this project (error MSB4803, because of `licenses.licx`).

To test processing without cameras or a PLC, see [Offline replay](docs/development/OFFLINE_REPLAY.md).

## Deploying

Production is deployed with ClickOnce from Visual Studio. See
[Production deployment](docs/deployment/PRODUCTION_DEPLOYMENT.md).
