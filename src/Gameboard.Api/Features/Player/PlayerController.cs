// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Gameboard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Gameboard.Api.Validators;

namespace Gameboard.Api.Controllers
{
    [Authorize]
    public class PlayerController : _Controller
    {
        PlayerService PlayerService { get; }

        public PlayerController(
            ILogger<PlayerController> logger,
            IDistributedCache cache,
            PlayerService playerService,
            PlayerValidator validator
        ): base(logger, cache, validator)
        {
            PlayerService = playerService;
        }

        /// <summary>
        /// Create new player
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("api/player")]
        [Authorize]
        public async Task<Player> Register([FromBody] NewPlayer model)
        {
            AuthorizeAny(
                () => Actor.IsTester || Actor.IsRegistrar,
                () => model.UserId == Actor.Id
            );

            await Validate(model);

            return await PlayerService.Register(model, Actor.IsTester || Actor.IsRegistrar);
        }

        /// <summary>
        /// Retrieve player
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/player/{id}")]
        [Authorize]
        public async Task<Player> Retrieve([FromRoute]string id)
        {
            // TODO: consider appropriate authorization
            // Note: this is essentially a scoreboard entry
            AuthorizeAll();

            await Validate(new Entity { Id = id });

            return await PlayerService.Retrieve(id);
        }

        /// <summary>
        /// Change player
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("api/player")]
        [Authorize]
        public async Task Update([FromBody] ChangedPlayer model)
        {
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsRegistrar,
                () => PlayerService.MapId(model.Id).Result == Actor.Id
            );

            await PlayerService.Update(model, Actor.IsRegistrar);
        }

        /// <summary>
        /// Start player/team session
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("api/player/start")]
        [Authorize]
        public async Task<Player> Start([FromBody] SessionStartRequest model)
        {
            AuthorizeAny(
                () => Actor.IsTester || Actor.IsRegistrar,
                () => PlayerService.MapId(model.Id).Result == Actor.Id
            );

            await Validate(model);

            return await PlayerService.Start(model, Actor.IsTester || Actor.IsRegistrar);
        }

        /// <summary>
        /// Delete a player enrollment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/player/{id}")]
        [Authorize]
        public async Task Delete([FromRoute]string id)
        {
            AuthorizeAny(
                () => Actor.IsRegistrar,
                () => Actor.IsTester && IsSelf(id).Result,
                () => IsSelf(id).Result
            );

            await Validate(new Entity { Id = id });

            await PlayerService.Delete(id, Actor.IsRegistrar || Actor.IsTester);
        }

        /// <summary>
        /// Find players
        /// </summary>
        /// <remarks>
        /// Filter with query params `gid, tid, uid, org` (group, team, user, sponsor ids)
        /// Filter withh query param `filter=collapse` to pull just one player record per team.
        /// </remarks>
        /// <param name="model">PlayerDataFilter</param>
        /// <returns></returns>
        [HttpGet("/api/players")]
        [AllowAnonymous]
        public async Task<Player[]> List([FromQuery] PlayerDataFilter model)
        {
            return await PlayerService.List(model, Actor.IsTester || Actor.IsRegistrar);
        }

        /// <summary>
        /// Show scoreboard
        /// </summary>
        /// <remarks>Include querystring value `gid` for game id</remarks>
        /// <param name="model">PlayerDataFilter</param>
        /// <returns>Standings</returns>
        [HttpGet("/api/scores")]
        [AllowAnonymous]
        public async Task<Standing[]> Scores([FromQuery] PlayerDataFilter model)
        {
            return await PlayerService.Standings(model);
        }

        /// <summary>
        /// Get Player Team
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Team</returns>
        [HttpGet("/api/team/{id}")]
        [Authorize]
        public async Task<Team> GetTeam([FromRoute] string id)
        {
            return await PlayerService.LoadTeam(id, Actor.IsObserver);
        }

        /// <summary>
        /// Get Player Team
        /// </summary>
        /// <param name="id">player id</param>
        /// <returns>Team</returns>
        [HttpGet("/api/board/{id}")]
        [Authorize]
        public async Task<BoardPlayer> GetBoard([FromRoute] string id)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => IsSelf(id).Result
            );

            return await PlayerService.LoadBoard(id);
        }

        /// <summary>
        /// Advance an enrollment to a different game
        /// </summary>
        /// <param name="model">TeamAdvancement</param>
        /// <returns></returns>
        [HttpPost("/api/team/advance")]
        [Authorize(AppConstants.DesignerPolicy)]
        public async Task AdvanceTeam([FromBody]TeamAdvancement model)
        {
            await Validate(model);

            await PlayerService.AdvanceTeam(model);
        }

        [HttpPost("/api/player/{id}/invite")]
        [Authorize]
        public async Task<TeamInvitation> Invite([FromRoute]string id)
        {
            AuthorizeAny(
                () => Actor.IsRegistrar,
                () => IsSelf(id).Result
            );

            await Validate(new Entity { Id = id });

            return await PlayerService.GenerateInvitation(id);
        }

        /// <summary>
        /// Enlists the user into a player team
        /// </summary>
        /// <param name="model">EnlistingPlayer</param>
        /// <returns></returns>
        [HttpPost("/api/player/enlist")]
        [Authorize]
        public async Task<Player> Enlist([FromBody]PlayerEnlistment model)
        {
            AuthorizeAny(
                () => Actor.IsRegistrar,
                () => model.UserId == Actor.Id,
                () => PlayerService.MapId(model.PlayerId).Result == Actor.Id
            );

            await Validate(model);

            return await PlayerService.Enlist(model, Actor.IsRegistrar);
        }
         private async Task<bool> IsSelf(string playerId)
        {
          return await PlayerService.MapId(playerId) == Actor.Id;
        }
    }
}