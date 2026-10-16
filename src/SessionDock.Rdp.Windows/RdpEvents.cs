using System.Runtime.InteropServices;

namespace SessionDock.Rdp.Windows;

// DISPIDs and signatures: Microsoft's IMsTscAxEvents interface documentation. Windows build
// tooling validates these against the installed type library; this authored sink has no vtable guesses.
[ComVisible(true), Guid("336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6"), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IRdpControlEvents
{
    [DispId(1)] void OnConnecting();
    [DispId(2)] void OnConnected();
    [DispId(3)] void OnLoginComplete();
    [DispId(4)] void OnDisconnected(int reason);
    [DispId(5)] void OnEnterFullScreenMode();
    [DispId(6)] void OnLeaveFullScreenMode();
    [DispId(8)] void OnRequestGoFullScreen();
    [DispId(9)] void OnRequestLeaveFullScreen();
    [DispId(10)] void OnFatalError(int code);
    [DispId(11)] void OnWarning(int code);
    [DispId(12)] void OnRemoteDesktopSizeChange(int width, int height);
    [DispId(13)] void OnIdleTimeoutNotification();
    [DispId(14)] void OnRequestContainerMinimize();
    [DispId(15)] void OnConfirmClose([In, Out, MarshalAs(UnmanagedType.VariantBool)] ref bool allowClose);
    [DispId(16)] void OnReceivedTSPublicKey([MarshalAs(UnmanagedType.BStr)] string publicKey, [In, Out, MarshalAs(UnmanagedType.VariantBool)] ref bool continueLogon);
    [DispId(17)] void OnAutoReconnecting(int reason, int attemptCount, out int continueState);
    [DispId(18)] void OnAuthenticationWarningDisplayed();
    [DispId(19)] void OnAuthenticationWarningDismissed();
    [DispId(22)] void OnLogonError(int code);
    [DispId(23)] void OnFocusReleased(int direction);
    [DispId(33)] void OnAutoReconnected();
    [DispId(34)] void OnAutoReconnecting2(int reason, [MarshalAs(UnmanagedType.VariantBool)] bool networkAvailable, int attemptCount, int maxAttemptCount);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RdpEventSink(RdpSessionControl owner) : IRdpControlEvents
{
    public void OnConnecting() => owner.HandleConnecting();
    public void OnConnected() => owner.HandleAuthenticating();
    public void OnLoginComplete() => owner.HandleLoginComplete();
    public void OnDisconnected(int reason) => owner.HandleDisconnected(reason);
    public void OnEnterFullScreenMode() => owner.HandleFullScreen(true);
    public void OnLeaveFullScreenMode() => owner.HandleFullScreen(false);
    public void OnRequestGoFullScreen() => owner.HandleFullScreen(true);
    public void OnRequestLeaveFullScreen() => owner.HandleFullScreen(false);
    public void OnFatalError(int code) => owner.HandleFatalError(code);
    public void OnWarning(int code) => owner.HandleWarning(code);
    public void OnRemoteDesktopSizeChange(int width, int height) { }
    public void OnIdleTimeoutNotification() => owner.HandleWarning(0);
    public void OnRequestContainerMinimize() => owner.HandleMinimize();
    public void OnConfirmClose(ref bool allowClose) => allowClose = true;
    // This callback is never enabled. If a control nevertheless asks, do not bypass authentication.
    public void OnReceivedTSPublicKey(string publicKey, ref bool continueLogon) => continueLogon = false;
    public void OnAutoReconnecting(int reason, int attemptCount, out int continueState) => continueState = owner.HandleAutoReconnect(attemptCount) ? 0 : 1;
    public void OnAuthenticationWarningDisplayed() => owner.HandleAuthenticationWarning();
    public void OnAuthenticationWarningDismissed() => owner.HandleAuthenticationWarningDismissed();
    public void OnLogonError(int code) => owner.HandleLogonError(code);
    public void OnFocusReleased(int direction) => owner.HandleFocusReleased();
    public void OnAutoReconnected() => owner.HandleAutoReconnected();
    public void OnAutoReconnecting2(int reason, bool networkAvailable, int attemptCount, int maxAttemptCount) => owner.HandleAutoReconnect(attemptCount);
}
