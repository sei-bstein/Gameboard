namespace Gameboard.Api.Hubs.PlayerPresence;

internal class PlayerPresenceEventArgs
{
    PlayerPresencePlayer Player { get; set; }
}

internal class PlayerPresenceService
{
    public delegate void OnPlayerChanged(PlayerPresenceEventArgs e);
}