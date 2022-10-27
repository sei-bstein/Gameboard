using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Data.Abstractions;
using Gameboard.Api.Hubs.PlayerPresence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Gameboard.Api.Hubs.PlayerPresence;

internal class PlayerPresenceHub : Hub<IPlayerPresenceEvent>
{
    private readonly ILogger<PlayerPresenceHub> _logger;
    private readonly IMapper _mapper;
    private PlayerPresenceHubState _state = new PlayerPresenceHubState();
    private readonly IPlayerStore _playerStore;

    public PlayerPresenceHub(
        ILogger<PlayerPresenceHub> logger,
        IMapper mapper,
        IPlayerStore playerStore)
    {
        _logger = logger;
        _mapper = mapper;
        _playerStore = playerStore;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug($"Session Connected: {Context.User.FindFirstValue("name")} {Context.UserIdentifier} {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception ex)
    {
        _logger.LogDebug($"Session Disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(ex);
        await ExitHub();
    }

    // Called by the logged-in player when they enter the hub
    [HubMethodName("enterHub")]
    public async Task<PlayerPresenceHubState> EnterHub(string teamId)
    {
        // validate that this team aligns with the current user/player
        var actor = Context.User.ToActor();
        var team = actor.Enrollments.FirstOrDefault(e => e.TeamId == teamId);

        if (team == null)
        {
            throw new EntityNotFound();
        }

        // build state
        this._state = new PlayerPresenceHubState();
        this._state.OtherPlayers = await GetTeammates(teamId);
        this._state.LoggedInPlayer = new PlayerPresencePlayer
        {
            UserId = actor.Id,
            OrignalName = actor.Name,
            ApprovedName = actor.ApprovedName,
            TeamId = team.Id,
            SponsorLogo = actor.Sponsor
        };

        // store active player in context
        this.Context.Items[PlayerPresenceHubContextItems.LoggedInPlayer] = this._state.LoggedInPlayer;

        // notify everyone else we're here
        await this.Clients.OthersInGroup(teamId).PlayerJoined(this._state.LoggedInPlayer);

        return this._state;
    }

    [HubMethodName("exitHub")]
    public Task ExitHub()
    {
        Task[] tasks;

        var player = Context.Items[PlayerPresenceHubContextItems.LoggedInPlayer] as PlayerPresencePlayer;

        if (player is null)
        {
            tasks = new Task[]
            {
                Groups.RemoveFromGroupAsync(Context.ConnectionId, Context.UserIdentifier),
                Groups.RemoveFromGroupAsync(Context.ConnectionId, AppConstants.InternalSupportChannel)
            };
        }
        else
        {
            this._logger.LogDebug($"Leave {player.TeamId} {Context.User?.Identity.Name} {Context.ConnectionId}");

            tasks = new Task[]
            {
                Groups.RemoveFromGroupAsync(Context.ConnectionId, player.TeamId),
                Groups.RemoveFromGroupAsync(Context.ConnectionId, AppConstants.InternalSupportChannel),
                Clients.OthersInGroup(player.TeamId).PlayerLeft(player)
            };

            Context.Items.Remove(PlayerPresenceHubContextItems.LoggedInPlayer);
        }

        return Task.WhenAll(tasks);
    }

    private async Task<IList<PlayerPresencePlayer>> GetTeammates(string teamId)
    {
        var players = await this._playerStore.ListTeam(teamId);

        return _mapper.Map<IList<Data.Player>, IList<PlayerPresencePlayer>>
        (
            players
                .Where(p => p.UserId != Context.User.ToActor().Id)
                .ToList()
        );
    }
}