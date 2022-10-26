// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Features.UnityGames;
using Gameboard.Api.Hubs;
using Gameboard.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Gameboard.Api.Controllers;

[Authorize]
public class UnityGameController : _Controller
{
    private readonly ConsoleActorMap _actorMap;
    private readonly ChallengeEventService _challengeEventService;
    private readonly GameService _gameService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<AppHub, IAppHubEvent> _hub;
    private readonly IMapper _mapper;
    private readonly UnityGameService _unityGameService;

    public UnityGameController(
        // required by _Controller
        IDistributedCache cache,
        ILogger<ChallengeEventController> logger,
        UnityGamesValidator validator,
        // other stuff
        ConsoleActorMap actorMap,
        GameService gameService,
        IHttpClientFactory httpClientFactory,
        UnityGameService unityGameService,
        ChallengeEventService challengeEventService,
        IHubContext<AppHub, IAppHubEvent> hub,
        IMapper mapper
    ) : base(logger, cache, validator)
    {
        _actorMap = actorMap;
        _challengeEventService = challengeEventService;
        _gameService = gameService;
        _httpClientFactory = httpClientFactory;
        _hub = hub;
        _mapper = mapper;
        _unityGameService = unityGameService;
    }

    [HttpGet("/api/unity/{gid}/{tid}")]
    [Authorize]
    public async Task<IActionResult> GetGamespace([FromRoute] string gid, [FromRoute] string tid)
    {
        AuthorizeAny(
            () => _gameService.UserIsTeamPlayer(Actor.Id, gid, tid).Result
        );

        var gb = await CreateGamebrain();
        var m = await gb.GetAsync($"admin/deploy/{gid}/{tid}");

        if (m.IsSuccessStatusCode)
        {
            var stringContent = await m.Content.ReadAsStringAsync();

            if (!stringContent.IsEmpty())
            {
                return new JsonResult(stringContent);
            }

            return Ok();
        }

        return BuildError(m, $"Bad response from Gamebrain: {m.Content} : {m.ReasonPhrase}");
    }

    [HttpPost("/api/unity/deploy/{gid}/{tid}")]
    [Authorize]
    public async Task<string> DeployUnitySpace([FromRoute] string gid, [FromRoute] string tid)
    {
        AuthorizeAny(
            () => Actor.IsDirector,
            () => _gameService.UserIsTeamPlayer(Actor.Id, gid, tid).Result
        );

        var gb = await CreateGamebrain();
        var m = await gb.PostAsync($"admin/deploy/{gid}/{tid}", null);
        return await m.Content.ReadAsStringAsync();
    }

    [HttpPost("/api/unity/undeploy/{gid}/{tid}")]
    [Authorize]
    public async Task<string> UndeployUnitySpace([FromQuery] string gid, [FromRoute] string tid)
    {
        AuthorizeAny(
            () => Actor.IsAdmin,
            () => _gameService.UserIsTeamPlayer(Actor.Id, gid, tid).Result
        );

        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var gb = await CreateGamebrain();

        var m = await gb.GetAsync($"admin/undeploy/{tid}");
        return await m.Content.ReadAsStringAsync();
    }

    /// <summary>
    ///     Create challenge data for an existing Unity game's gamespace.
    /// </summary>
    /// <param name="model">NewChallengeEvent</param>
    /// <returns>ChallengeEvent</returns>
    [HttpPost("api/unity/challenges")]
    [Authorize]
    public async Task<IList<Data.Challenge>> CreateChallenge([FromBody] NewUnityChallenge model)
    {
        AuthorizeAny(
            () => Actor.IsAdmin,
            () => Actor.IsDirector
        );

        await Validate(model);
        var result = await _unityGameService.Add(model, Actor);

        foreach (var challenge in result.Select(c => _mapper.Map<Challenge>(c)))
        {
            await _hub.Clients
                .Group(model.TeamId)
                .ChallengeEvent(new HubEvent<Challenge>(challenge, EventAction.Updated));
        }

        return result;
    }

    private ActionResult<T> BuildError<T>(HttpResponse response, string message = null)
    {
        var result = new ObjectResult(message);
        result.StatusCode = response.StatusCode;
        return result;
    }

    private ActionResult BuildError(HttpResponseMessage response, string message)
    {
        var result = new ObjectResult(message);
        result.StatusCode = (int)response.StatusCode;
        return result;
    }

    private async Task<HttpClient> CreateGamebrain()
    {
        var gb = _httpClientFactory.CreateClient("Gamebrain");
        gb.DefaultRequestHeaders.Add("Authorization", $"Bearer {await HttpContext.GetTokenAsync("access_token")}");
        return gb;
    }
}
