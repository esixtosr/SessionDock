# Third-party notices

The original SessionDock source and geometric application artwork were created for this project. No Microsoft product logo, reference wallpaper or screenshot is distributed. A separate open-source license has not yet been selected for the original application; publishing the repository does not change the licenses of its dependencies.

Redistributed dependencies retain their upstream licenses and copyright notices. Full available texts are included in `licenses/` and with runtime package files. NuGet lock files record exact transitive versions and integrity hashes.

| Component | Version | Upstream license / source |
|---|---|---|
| .NET / Windows Desktop runtime | 10.0.13 in the self-contained app | MIT plus runtime third-party notices; [dotnet/runtime](https://github.com/dotnet/runtime), [dotnet/wpf](https://github.com/dotnet/wpf), [dotnet/winforms](https://github.com/dotnet/winforms) |
| CommunityToolkit.Mvvm | 8.4.2 | MIT; included package License.md/ThirdPartyNotices; [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet) |
| Microsoft.Data.Sqlite / Core | 10.0.13 | MIT; [dotnet/efcore](https://github.com/dotnet/efcore) |
| SQLitePCLRaw core, provider, bundle and native library package | 2.1.13 | Apache-2.0 package license; [SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw) |
| SQLite engine | Native library from SQLitePCLRaw.lib.e_sqlite3 | SQLite is public domain; [SQLite copyright](https://www.sqlite.org/copyright.html) |
| Windows SDK .NET projections (Windows.SDK.NET / WinRT.Runtime) | SDK pack 10.0.19041.57 | Microsoft Windows SDK terms; [SDK license](https://aka.ms/WinSDKLicenseURL), [C#/WinRT source](https://github.com/microsoft/CsWinRT) |
| Microsoft RDP ActiveX control | Installed Windows version | Windows component, governed by Microsoft's Windows terms; `MsTscAx.dll` is **not redistributed** |

Windows interop wrappers are generated locally from the installed system type library. They are not downloaded from third-party binaries. The macOS-produced preview has no generated interop DLL; the Windows build creates and validates it. Do not include the system MsTscAx.dll in a release bundle.

Test-only packages (not shipped with the app): xUnit 2.9.3 and related runner/analyzer packages (Apache-2.0), xunit.runner.visualstudio 3.1.5 (MIT), Microsoft.NET.Test.Sdk/test platform/code coverage 17.14.1 (MIT with included third-party notices), and Newtonsoft.Json 13.0.3 (MIT). Exact transitive versions are in `tests/SessionDock.Tests/packages.lock.json`.

A successful dependency audit reports known advisories at the time of the check; it does not replace a release security review or guarantee safety.
