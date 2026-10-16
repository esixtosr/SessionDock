namespace SessionDock.Rdp.Windows;

/// <summary>Messages from numeric documented codes; no raw server or COM strings enter diagnostics.</summary>
public static class RdpFailureDescriptions
{
    public static string Describe(int reason, int extended, bool usesGateway)
    {
        var extendedMessage = extended switch
        {
            2 or 12 => "The remote session was signed out on the host. SessionDock did not send a sign-out request.",
            3 => "The host disconnected this idle session under its timeout policy.",
            4 => "The host ended sign-in because its logon time limit expired.",
            5 => "Another connection replaced this remote session.",
            7 or 8 or 9 => "The host denied access under its permissions or security policy. Ask its administrator to verify RDP access.",
            10 => "The host requires freshly entered credentials. Select Ask when required and enter them for this connection.",
            >= 256 and <= 267 => "Remote Desktop licensing failed. Ask the host administrator to check RDS licensing.",
            768 => "The host rejected the supplied credentials. Automatic retry is stopped; verify the account before trying again.",
            _ => null
        };
        if (extendedMessage is not null) return extendedMessage;
        return reason switch
        {
            260 or 1288 or 1540 or 520 => "Windows could not resolve the PC name. Check its spelling, DNS, and any required VPN connection.",
            264 or 1796 => "The connection timed out. Check that the host is awake, RDP is enabled, and the selected network path is available.",
            516 => usesGateway ? "The connection endpoint could not be reached. Check the RD Gateway server, proxy requirements, and destination access." : "The RDP endpoint could not be reached. Check the host, port, firewall, and any required VPN.",
            772 or 1028 or 2308 => "The RDP network connection was lost. Check connectivity before reconnecting.",
            776 or 2052 => "Windows rejected the destination IP address. Check the saved PC address.",
            1798 or 6919 => "The server certificate is invalid or expired. Verify its identity and correct certificate trust; SessionDock will not bypass validation.",
            1030 or 1286 or 1542 or 2310 or 2566 or 2822 or 3078 => "The secure RDP handshake failed. Check the server certificate, supported security policy, and Windows updates; security was not downgraded.",
            2055 or 2567 => "Sign-in failed. Check the username, domain, and password. Automatic credential retries are stopped.",
            2823 => "The account is disabled. Contact the account administrator before trying again.",
            3335 => "The account is locked out. Automatic retries are stopped; ask the account administrator to resolve the lockout.",
            3591 => "The account has expired. Contact the account administrator.",
            3079 => "An account restriction blocked sign-in. Check the account's permitted hours and host access.",
            3847 or 4615 => "The password has expired or must be changed. Update it through the account's approved password-change process.",
            5639 or 5895 => "Windows credential delegation policy blocked the connection. Ask the administrator to verify the host trust and policy.",
            8455 => "The server requires fresh credentials. Select Ask when required and enter them for this connection.",
            6151 => "The authentication authority could not be reached. Check domain connectivity and the account domain.",
            8711 or 7175 => "Smart-card authentication failed. Check card status and PIN; automatic retry is stopped.",
            2056 or 2312 => "Remote Desktop license negotiation failed. Ask the host administrator to check RDS licensing.",
            _ => usesGateway ? "The RDP connection ended. Check the RD Gateway and destination event logs using the displayed codes; SessionDock has stopped retrying." : "The RDP connection ended. Check the destination and Windows event logs using the displayed codes; SessionDock has stopped retrying."
        };
    }
}
