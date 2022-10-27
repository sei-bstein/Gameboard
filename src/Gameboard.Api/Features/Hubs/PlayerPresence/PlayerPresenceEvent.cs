namespace Gameboard.Api.Hubs.PlayerPresence;

internal class PlayerJoined
{
    PlayerPresencePlayer Player { get; set; }
}

internal class PlayerLeft
{
    PlayerPresencePlayer Player { get; set; }
}