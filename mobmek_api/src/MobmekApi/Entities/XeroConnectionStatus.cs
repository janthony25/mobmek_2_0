namespace MobmekApi.Entities;

/// <summary>State of the single <see cref="XeroConnection"/> row.</summary>
public enum XeroConnectionStatus
{
    /// <summary>No successful OAuth exchange has ever completed (or it was explicitly disconnected).</summary>
    Disconnected,

    /// <summary>Tokens are present and the last refresh attempt (if any) succeeded.</summary>
    Connected,

    /// <summary>A refresh or API call failed (e.g. revoked consent) — <see cref="XeroConnection.LastError"/>
    /// holds why. The admin needs to reconnect via "Connect to Xero" again.</summary>
    Error,
}
