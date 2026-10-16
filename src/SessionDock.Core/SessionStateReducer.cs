namespace SessionDock.Core;

public enum SessionSignal { BeginConnect, TransportConnected, LoginComplete, ConnectionLost, ReconnectStarted, Disconnected, UserDisconnect, AuthenticationRejected, CertificateDecisionRequired, Failure }
public sealed record SessionStateSnapshot(SessionState State = SessionState.Disconnected, bool HasAuthenticated = false, bool DisconnectRequested = false, bool ReconnectAllowed = false);

/// <summary>Only a control's login-complete event can establish authenticated Connected status.</summary>
public static class SessionStateReducer
{
    public static SessionStateSnapshot Apply(SessionStateSnapshot current, SessionSignal signal, bool automaticReconnect = true) => signal switch
    {
        SessionSignal.BeginConnect when current.State is SessionState.Disconnected or SessionState.Failed
            => new(SessionState.Connecting, ReconnectAllowed: automaticReconnect),
        SessionSignal.UserDisconnect when current.State is not SessionState.Disconnected
            => current with { State = SessionState.Disconnecting, DisconnectRequested = true, ReconnectAllowed = false },
        SessionSignal.TransportConnected when !current.DisconnectRequested && current.State is SessionState.Connecting or SessionState.Reconnecting
            => current with { State = SessionState.Authenticating },
        SessionSignal.LoginComplete when !current.DisconnectRequested && current.State is SessionState.Connecting or SessionState.Authenticating or SessionState.Reconnecting
            => current with { State = SessionState.Connected, HasAuthenticated = true },
        SessionSignal.ConnectionLost when current.State is SessionState.Connected && !current.DisconnectRequested && current.HasAuthenticated && current.ReconnectAllowed && automaticReconnect
            => current with { State = SessionState.Reconnecting },
        SessionSignal.ConnectionLost when current.State is SessionState.Connected or SessionState.Connecting or SessionState.Authenticating
            => current with { State = SessionState.Failed, ReconnectAllowed = false },
        SessionSignal.ReconnectStarted when current.State is SessionState.Reconnecting && !current.DisconnectRequested && current.ReconnectAllowed && automaticReconnect
            => current,
        SessionSignal.Disconnected => current with { State = SessionState.Disconnected, ReconnectAllowed = false },
        SessionSignal.AuthenticationRejected or SessionSignal.CertificateDecisionRequired or SessionSignal.Failure
            => current with { State = SessionState.Failed, ReconnectAllowed = false },
        _ => current
    };
}
