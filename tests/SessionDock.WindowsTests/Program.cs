using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using SessionDock.Rdp.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows is required. No engine test was run."); return 2; }
        ApplicationConfiguration.Initialize();
        var vaultResult = VaultProbe.Run();
        bool vaultPassed = vaultResult.RoundTripPassed && vaultResult.DeletionVerified;
        Console.WriteLine(vaultPassed ? "PASS: temporary app-owned Credential Manager item round-tripped and was deleted." : $"FAIL: credential round-trip or deletion failed; HRESULT={vaultResult.ErrorCode}, cleanup HRESULT={vaultResult.CleanupErrorCode}.");
        bool previewPassed = PreviewProbe.Run();
        int exitCode = vaultPassed && previewPassed ? 0 : 1;
        var reportPath = args.Length == 2 && args[0] == "--report" ? Path.GetFullPath(args[1]) : null;
        object? report = null;
        using var window = new Form { Text = "SessionDock installed-control activation probe — no connection", Width = 1000, Height = 550 };
        RdpSessionControl? first = null, second = null;
        try
        {
            first = new RdpSessionControl();
            second = new RdpSessionControl();
            ((ISupportInitialize)first).BeginInit();
            ((ISupportInitialize)second).BeginInit();
            var split = new SplitContainer { Dock = DockStyle.Fill, Width = 980, SplitterDistance = 480 };
            split.Panel1.Controls.Add(first);
            split.Panel2.Controls.Add(second);
            window.Controls.Add(split);
            ((ISupportInitialize)first).EndInit();
            ((ISupportInitialize)second).EndInit();
            window.Shown += (_, _) =>
            {
                if (first.ControlVersion == "Not activated" || second.ControlVersion == "Not activated") { exitCode = 1; Console.Error.WriteLine("FAIL: a control was not activated."); }
                else
                {
                    Console.WriteLine($"OS: {Environment.OSVersion.VersionString}");
                    Console.WriteLine($"Control: {first.ControlVersion}; second independent control: {second.ControlVersion}");
                    Console.WriteLine("PASS: two STA-hosted ActiveX controls created. No network, login, redirection, resize, or package test was performed.");
                    var optionProbe = first.ProbeOptionMappings();
                    bool mappingsPassed = optionProbe.All(x => x.Accepted);
                    foreach (var failed in optionProbe.Where(x => !x.Accepted))
                        Console.Error.WriteLine($"FAIL: RDP option {failed.Target}.{failed.Member}; HRESULT 0x{failed.ErrorCode:X8}.");
                    if (!mappingsPassed) exitCode = 1;
                    Console.WriteLine(mappingsPassed ? "PASS: all preconnection option setters accepted. No connection was attempted; remote effects remain unverified." : "FAIL: a preconnection option setter was rejected. No connection was attempted.");
                    report = new { schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, os = Environment.OSVersion.VersionString, architecture = RuntimeInformation.ProcessArchitecture.ToString(), controlVersion = first.ControlVersion, secondControlVersion = second.ControlVersion, activation = "Passed", optionMapping = mappingsPassed ? "Passed" : "Failed", optionProbe, realLogin = "Not tested", networkAccess = false, syntheticPreview = previewPassed ? "Passed" : "Failed", credentialVault = vaultResult, capabilities = first.GetCapabilityReport() };
                }
                window.BeginInvoke(window.Close);
            };
            Application.Run(window);
            // Parent close may release ActiveX before derived Dispose. These must be safe no-ops.
            first.Disconnect();
            second.Disconnect();
            first.Dispose();
            second.Dispose();
            first.Dispose();
            second.Dispose();
            Console.WriteLine("PASS: parent close followed by repeated disconnect and disposal completed.");
        }
        catch (Exception ex) { Console.Error.WriteLine($"FAIL: control activation failed, HRESULT 0x{ex.HResult:X8}. No real-session test was performed."); exitCode = 1; }
        finally { first?.Dispose(); second?.Dispose(); }
        if (reportPath is not null)
        {
            report ??= new { schemaVersion = 1, generatedUtc = DateTimeOffset.UtcNow, os = Environment.OSVersion.VersionString, activation = "Failed", realLogin = "Not tested", networkAccess = false, syntheticPreview = previewPassed ? "Passed" : "Failed", credentialVault = vaultResult };
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            var options = new JsonSerializerOptions { WriteIndented = true }; options.Converters.Add(new JsonStringEnumConverter());
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, options));
            Console.WriteLine("Local capability report written. It contains no destination or account values.");
        }
        return exitCode;
    }
}
