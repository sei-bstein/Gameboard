using System;
using System.Collections.Generic;

namespace Gameboard.Api.Data;

public class Team : IEntity
{
    public string Id { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public string InviteCode { get; set; }

    public string InivitationHostId { get; set; }
    public Player InvitationHost { get; set; }
    public ICollection<Player> Players { get; set; }
}