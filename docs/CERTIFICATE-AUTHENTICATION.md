# Server certificate authentication

Configure PC > General > Server certificate authentication offers Strict (recommended default) and optional Interactive per saved PC. Older profiles default to Strict. Both keep CredSSP enabled; level 0 is unsupported. No automatic downgrade or warning approval occurs.

Strict uses AuthenticationLevel=1: Windows must authenticate the server or reject the connection. Interactive uses level 2: Windows can ask whether to cancel or proceed without verified server identity. Enterprise certificate, NLA and credential-delegation policies may still block access. This setting does not override policy.

Homelab Windows PCs commonly use self-signed RDP certificates whose issuer Windows cannot validate. Expiry, hostname mismatch and certificate replacement can also cause warnings. Investigate changes and independently verify identity with the administrator. Proceeding with unverified identity risks exposing credentials and session data to an impersonator; a familiar hostname alone is insufficient verification.

## Configure a trusted certificate

Obtain a server-authentication certificate from your organization's PKI or another CA trusted by the client, including the DNS connection name in its subject alternative names. Install it with its private key on the remote machine and bind it to the RDP listener using supported Windows administration. Grant Remote Desktop Services the required private-key access and manage certificate chain, renewal and revocation. Have an administrator deploy a verified issuing CA through normal managed trust deployment. Never install an arbitrary remote leaf certificate into Trusted Root. SessionDock installs no certificates and changes no machine-wide policy.

Follow [Microsoft's RDP listener certificate configuration guidance](https://learn.microsoft.com/en-us/troubleshoot/windows-server/remote/remote-desktop-listener-certificate-configurations) for your Windows deployment.

## Native warnings and limitations

Windows owns certificate details and warning decisions. SessionDock's Connection details shows endpoint and selected policy. Warning display stops automatic reconnect for the rest of that attempt. Dismissal conveys no decision result: only native login completion can report initial Connected. After cancellation/disconnection, correct the cause before manually retrying. Certificate warnings are not classified as invalid credentials.

The optional dialog parent uses the generated IMsRdpClientNonScriptable2.UIParentWindowHandle wrapper with verified IID and pointer-sized HWND checks. Generation validates installed type-library metadata. If the wrapper is unavailable or the setter fails, Connection details reports the limitation. Actual dialog visibility requires interactive testing. No custom trust database, certificate pinning, separate TLS authentication probe, or root certificate installation is implemented.

## Test W11PC4-Public

1. Build the feature branch and use an authorized test account. Independently verify the machine and certificate with its administrator.
2. Configure W11PC4-Public with Strict; save and restart SessionDock to confirm persistence. An untrusted certificate must not lead to successful login.
3. Close/disconnect the attempt. Select Interactive, read its security warning, save and restart to confirm persistence. Connect and inspect the native warning if Windows policy permits.
4. Cancel first. Confirm no Connected status, login timestamp or repeated automatic dialogs. Manually reconnect and proceed only after independently verifying identity. Connected must await native login completion; policy may still deny access.
5. Return to Strict, configure a trusted matching certificate through an administrator and repeat. Test hostname mismatch, changed certificate, invalid credentials, unreachable host and deliberate cancellation in both modes. Record actual results and Windows policies.

## API references

- [AuthenticationLevel](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings4-authenticationlevel)
- [UIParentWindowHandle](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientnonscriptable2-uiparentwindowhandle)
