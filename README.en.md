<p align="center">
  <img src="assets/icon.png" width="128" alt="Akota Startup Manager icon" />
</p>

<h1 align="center">Akota Startup Manager</h1>

<p align="center">
  Reliable, recoverable, and dependency-aware Windows startup management.
</p>

<p align="center">
  <a href="README.md">简体中文</a> ·
  <a href="https://github.com/AkotaP/AkotaStartupManager/releases">Releases</a> ·
  <a href="https://github.com/AkotaP/AkotaStartupManager/issues">Issues</a>
</p>

> [!IMPORTANT]
> This project is currently at the early `0.1.1` stage. Understand the impact and back up important settings before changing machine-wide registry entries, the common Startup folder, or scheduled tasks.

## Why use it?

Native Windows startup settings can only say “start after sign-in.” They cannot express “the database port is listening,” “a local endpoint is healthy,” or “another application's window has appeared.” Akota Startup Manager takes over those entries, continuously evaluates dependency conditions, and launches targets only after the conditions remain stable.

<p align="center">
  <img src="assets/screenshot.png" width="900" alt="Akota Startup Manager main window" />
</p>

## Features

- Manage registry `Run` / `RunOnce` entries for current user and machine, including 32/64-bit views
- Manage per-user and common Startup folders
- Manage Windows scheduled tasks with boot or logon triggers
- Preserve recoverable backups when disabling native startup entries
- Convert a native startup item directly into a managed rule
- Build nested AND / OR condition trees with:
  - **Process** — a process exists
  - **TcpPort** — a local TCP endpoint accepts connections
  - **Http** — a local HTTP/HTTPS endpoint returns a success status
  - **FileExists** — a file has been created
  - **WindowsService** — a Windows service is running
  - **WindowTitle** — a visible window title appears, optionally using regex
- Poll every two seconds and require three consecutive successful checks by default
- Delay after readiness, prevent duplicate launches, verify process survival, and retry failures
- Single-instance behavior, Windows background startup, system tray controls, and local logs
- Keep configuration, backups, and logs beside the portable executable

## Requirements

- Windows 10/11 x64
- Prebuilt **standalone** packages do not require a separately installed .NET runtime
- Smaller **runtime** packages require the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Building from source requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Quick start

1. Open [Releases](https://github.com/AkotaP/AkotaStartupManager/releases) and choose:
   - `AkotaStartupManager-v*-win-x64-standalone.zip` — includes the runtime and is ready to run after extraction.
   - `AkotaStartupManager-v*-win-x64-runtime.zip` — smaller download that requires the .NET 10 Desktop Runtime.
2. Verify the SHA-256 value using the matching `.zip.sha256` file, then extract the ZIP to a user-writable directory.
3. Run `AkotaStartupManager.exe`.
4. Use **Windows Startup** to disable, restore, or take over an entry.
5. Use **Managed Startup** to configure the target, stability policy, and dependencies.

> [!WARNING]
> Do not place the portable build under `Program Files` or another location that normal users cannot write to. Machine-wide operations can trigger UAC. Do not disable system or security tasks unless you understand their purpose.

## Portable data

The application creates runtime data only beside the executable:

```text
Data/
├─ config.json
├─ config.previous.json
└─ Backups/
Logs/
└─ akota-YYYYMMDD.log
```

After moving the entire application directory, toggle **Start with Windows** off and on to update the executable path in the registry. Release archives never contain existing configuration, backups, or logs.

## Evaluation and launch flow

```text
Evaluate condition tree in a loop
  ├─ failure: reset consecutive count and wait
  └─ success: increment consecutive count
       └─ threshold reached
            └─ delay after readiness
                 └─ check whether target is already running
                      ├─ running: skip duplicate launch
                      └─ stopped: launch → survival check → retry if required
```

An empty group is invalid. AND requires every enabled child to succeed; OR requires at least one enabled child to succeed. To reduce SSRF exposure, HTTP conditions accept only localhost or loopback addresses.

## Build from source

```powershell
git clone https://github.com/AkotaP/AkotaStartupManager.git
Set-Location .\AkotaStartupManager

dotnet restore .\AkotaStartupManager.slnx
dotnet build .\AkotaStartupManager.slnx -c Release
dotnet test .\AkotaStartupManager.slnx -c Release --no-build
```

Create both portable GitHub Release packages (standalone and runtime) with their SHA-256 checksum files. The version is read from the repository's root `VERSION` file by default:

```powershell
.\scripts\publish.ps1
```

Use `-Version` for a temporary override without modifying `VERSION`:

```powershell
.\scripts\publish.ps1 -Version 0.1.1-beta.1
```

Artifacts are written to `artifacts\`. See [CONTRIBUTING.md](CONTRIBUTING.md#releasing) for the manual release procedure.

## Repository layout

```text
assets/                              Brand assets and repository screenshots
src/
├─ AkotaStartupManager.Core/         Domain models and interfaces
├─ AkotaStartupManager.Application/  Orchestration and startup business logic
├─ AkotaStartupManager.Infrastructure/ Windows APIs, persistence, and logging
└─ AkotaStartupManager.App/          WPF UI, MVVM, and system tray
scripts/                             Reproducible local release scripts
tests/                               Unit and infrastructure tests
```

Dependencies flow as `Core ← Application ← Infrastructure ← App`. Add a new condition by implementing `StartupCondition` and `IConditionChecker`; add a startup source by implementing `IStartupProvider`.

## Roadmap

- Improve rule editor interactions and on-demand condition testing
- Add broader isolated Windows integration coverage
- Add configuration import, export, and migration UI
- Improve accessibility, localization, and theme support

The roadmap is not a release commitment. Please use Issues to discuss priorities.

## Contributing and security

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting code.
- Follow [SECURITY.md](SECURITY.md) for private vulnerability reporting. Do not post registry exports, full logs, or sensitive local paths in public issues.
- See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

Copyright © 2026 [AkotaP](https://github.com/AkotaP).

This project is released under the [GNU General Public License v3.0](LICENSE). Distribution of modified versions must comply with the GPL-3.0 source and license obligations. The software is provided **as is**, without warranty.
