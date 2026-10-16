# MSIX packaging

This Windows Application Packaging Project is source only until a Windows Visual Studio packaging build succeeds. It is kept outside the portable `.slnx` so non-Windows builds do not pretend to run DesktopBridge.

Run `scripts/Package-Msix.ps1` on Windows with Visual Studio, .NET desktop development, Windows Application Packaging Project support and Windows SDK 10.0.26100.0. The default package is **unsigned**. `CN=SessionDock-Development` is a development publisher identity, not a trusted production certificate.

For a distributable package, configure the real publisher and an external signing certificate in your Windows release environment. Never commit a private signing key. No script here imports a certificate into trust stores. Test-signed and production-signed packages must be described accurately.

Validate: installed launch, RDP ActiveX activation, app-owned Credential Manager write/read/delete, real RDP logon, settings retention after upgrade, expected app-data behavior and uninstall. An unpackaged cross-build does not establish installed-package behavior.

The package's `MaxVersionTested` is a manifest configuration value, **not evidence of a performed test**; record the actual Windows build in the acceptance report.
