using SessionDock.Core;

namespace SessionDock.Infrastructure;

/// <summary>Structured local events only. No free-form exceptions, endpoints, usernames or secrets.</summary>
public sealed class LocalDiagnostics(string directory)
{
    private readonly object gate = new();
    public void Session(Guid profileId, SessionState state, int? code = null)
    {
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "session-events.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                File.Move(path, path + ".previous", overwrite: true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O}\t{profileId:D}\t{state}\t{code?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}{Environment.NewLine}");
        }
    }
}
