using System;
using System.Collections.Generic;

namespace Gameboard.Api.Hubs.PlayerPresence;

internal class PlayerPresencePlayer
{
    public string ApprovedName { get; set; }
    public string OrignalName { get; set; }
    public string SponsorLogo { get; set; }
    public string TeamId { get; set; }
    public string UserId { get; set; }
}

internal class PlayerPresenceHubState
{
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.Now;
    public string TeamId { get; set; }
    public PlayerPresencePlayer LoggedInPlayer { get; set; }
    public IList<PlayerPresencePlayer> OtherPlayers { get; set; } = new List<PlayerPresencePlayer>();
}