using SessionDock.Rdp.Windows;

namespace SessionDock.Tests;

public sealed class RdpFailureDescriptionsTests
{
    [Fact] public void AccountLockoutDoesNotEncourageCredentialRetries()
    {
        var message = RdpFailureDescriptions.Describe(3335, 0, false);
        Assert.Contains("locked out", message); Assert.Contains("retries are stopped", message);
        Assert.Contains("administrator", message);
    }
    [Fact] public void NameResolutionFailureIsDistinguishedFromBadCredentials()
    {
        var message = RdpFailureDescriptions.Describe(260, 0, false);
        Assert.Contains("DNS", message); Assert.DoesNotContain("password", message);
    }
    [Fact] public void SpecificExtendedFreshCredentialsReasonTakesPriorityOverGenericNetworkCode()
    {
        var message = RdpFailureDescriptions.Describe(516, 10, true);
        Assert.Contains("freshly entered credentials", message);
        Assert.Contains("Ask when required", message); Assert.DoesNotContain("could not be reached", message);
    }
    [Fact] public void UnknownFailureDoesNotInventAReportedCause()
    {
        var message = RdpFailureDescriptions.Describe(123456, 654321, true);
        Assert.Contains("displayed codes", message); Assert.Contains("event logs", message);
        Assert.DoesNotContain("password", message); Assert.DoesNotContain("certificate", message); Assert.DoesNotContain("DNS", message);
    }
}
