// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Gameboard.Api.Data.Abstractions;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace Gameboard.Api.Services
{
    public class PlayerService
    {
        IPlayerStore Store { get; }
        IGameStore GameStore { get; }
        IMapper Mapper { get; }
        IMemoryCache LocalCache { get; }
        TimeSpan _idmapExpiration = new TimeSpan(0, 30, 0);

        public PlayerService (
            IPlayerStore store,
            IGameStore gameStore,
            IMapper mapper,
            IMemoryCache localCache
        ){
            Store = store;
            GameStore = gameStore;
            Mapper = mapper;
            LocalCache = localCache;
        }

        public async Task<Player> Register(NewPlayer model, bool sudo = false)
        {
            var game = await GameStore.Retrieve(model.GameId);

            if (!sudo && !game.RegistrationActive)
                throw new RegistrationIsClosed();

            var user = await Store.GetUserEnrollments(model.UserId);

            if (user.Enrollments.Any(p => p.GameId == model.GameId))
                throw new AlreadyRegistered();

            var entity = Mapper.Map<Data.Player>(model);

            entity.TeamId = Guid.NewGuid().ToString("n");
            entity.Role = PlayerRole.Manager;
            entity.ApprovedName = user.ApprovedName;
            entity.Name = user.ApprovedName;
            entity.Sponsor = user.Sponsor;
            entity.SessionMinutes = game.SessionMinutes;

            await Store.Create(entity);

            return Mapper.Map<Player>(entity);
        }

        /// <summary>
        /// Maps a PlayerId to it's UserId
        /// </summary>
        /// <remarks>This happens frequently for authorization, so cache the mapping.</remarks>
        /// <param name="playerId"></param>
        /// <returns></returns>
        public async Task<string> MapId(string playerId)
        {
            if (LocalCache.TryGetValue(playerId, out string userId))
                return userId;

            userId = (await Store.Retrieve(playerId))?.UserId;

            LocalCache.Set(playerId, userId, _idmapExpiration);

            return userId;
        }

        public async Task<Player> Retrieve(string id)
        {
            return Mapper.Map<Player>(await Store.Retrieve(id));
        }

        public async Task Update(ChangedPlayer model, bool sudo = false)
        {
            var entity = await Store.Retrieve(model.Id);

            bool pushToTeam =
                model.Name != entity.Name ||
                model.ApprovedName != entity.ApprovedName
            ;

            if (!sudo)
            {
                Mapper.Map(
                    Mapper.Map<SelfChangedPlayer>(model),
                    entity
                );
            }
            else
            {
                Mapper.Map(model, entity);
            }

            await Store.Update(entity);

            // change names for whole team
            if (!pushToTeam)
                return;

            var team = await Store.ListTeamByPlayer(model.Id);

            foreach( var p in team)
            {
                p.Name = model.Name;
                p.ApprovedName = model.ApprovedName;
            }

            await Store.Update(team);

        }

        public async Task Delete(string id, bool sudo = false)
        {
            var player = await Store.List()
                .Include(p => p.Game)
                .FirstOrDefaultAsync(
                    p => p.Id == id
                )
            ;

            if (!sudo && !player.Game.AllowReset)
                throw new ActionForbidden();

            if (!sudo && !player.Game.RegistrationActive)
                throw new RegistrationIsClosed();

            await Store.Delete(id);
        }

        public async Task<Player> Start(SessionStartRequest model, bool sudo)
        {

            var team = await Store.ListTeamByPlayer(model.Id);

            var game = await Store.DbContext.Games.FindAsync(model.GameId);

            if (!sudo && game.SessionLimit > 0)
            {
                int sessionCount = await Store.DbSet
                    .CountAsync(p =>
                        p.GameId == game.Id &&
                        DateTimeOffset.UtcNow.CompareTo(p.SessionEnd) < 0
                    )
                ;

                if (sessionCount >= game.SessionLimit)
                    throw new SessionLimitReached();
            }

            if (!sudo && game.IsLive.Equals(false))
                throw new GameNotActive();

            if (
                !sudo &&
                game.RequireTeam &&
                team.Length < game.MinTeamSize
            )
                throw new InvalidTeamSize();

            var st = DateTimeOffset.UtcNow;
            var et = st.AddMinutes(team.First().SessionMinutes);

            foreach( var p in team)
            {
                p.SessionBegin = st;
                p.SessionEnd = et;
            }

            await Store.Update(team);

            return Mapper.Map<Player>(
                team.First(p => p.Id == model.Id)
            );
        }

        public async Task<Player[]> List(PlayerDataFilter model, bool sudo = false)
        {
            if (!sudo && !model.WantsGame && !model.WantsTeam)
                return new Player[] {};

            var q = _List(model);

            return await Mapper.ProjectTo<Player>(q).ToArrayAsync();
        }

        public async Task<Standing[]> Standings(PlayerDataFilter model)
        {
            if (model.gid.IsEmpty())
                return new Standing[] {};

            model.Filter = model.Filter
                .Append(PlayerDataFilter.FilterCollapseTeams)
                .ToArray()
            ;

            var q = _List(model);

            return await Mapper.ProjectTo<Standing>(q).ToArrayAsync();
        }

        private IQueryable<Data.Player> _List(PlayerDataFilter model)
        {
            var q = Store.List()
                .Include(p => p.User)
                .AsNoTracking();

            if (model.WantsGame)
            {
                q = q.Where(p => p.GameId == model.gid);

                if (model.WantsUser)
                    q = q.Where(p => p.UserId == model.uid);

                if (model.WantsOrg)
                    q = q.Where(p => p.Sponsor == model.org);
            }

            if (model.WantsTeam)
                q = q.Where(p => p.TeamId == model.tid);

            if (model.WantsCollapsed)
                q = q.Where(p => p.Role == PlayerRole.Manager);

            if (model.Term.NotEmpty())
            {
                string term = model.Term.ToLower();

                q = q.Where(p =>
                    p.ApprovedName.ToLower().Contains(term) ||
                    p.Name.ToLower().Contains(term)
                );
            }

            q = q.OrderByDescending(p => p.Score)
                .ThenBy(p => p.Time)
                .ThenByDescending(p => p.CorrectCount)
                .ThenByDescending(p => p.PartialCount)
                .ThenBy(p => p.Rank)
                .ThenBy(p => p.ApprovedName);

            q = q.Skip(model.Skip);

            if (model.Take > 0)
                q = q.Take(model.Take);

            return q;
        }

        public async Task<BoardPlayer> LoadBoard(string id)
        {
            return Mapper.Map<BoardPlayer>(
                await Store.LoadBoard(id)
            );
        }

        public async Task<TeamInvitation> GenerateInvitation(string id)
        {
            var player = await Store.Retrieve(id);

            if (player.Role != PlayerRole.Manager)
                throw new ActionForbidden();

            byte[] buffer = new byte[16];

            new Random().NextBytes(buffer);

            player.InviteCode = Convert.ToBase64String(buffer)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")
            ;

            await Store.Update(player);

            return new TeamInvitation
            {
                Code = player.InviteCode
            };
        }

        public async Task<Player> Enlist(PlayerEnlistment model, bool sudo = false)
        {

            var manager = await Store.List()
                .Include(p => p.Game)
                .FirstOrDefaultAsync(
                    p => p.InviteCode == model.Code
                )
            ;

            if (manager is not Data.Player)
                throw new InvalidInvitationCode();

            var player = model.PlayerId.NotEmpty()
                ? await Store.Retrieve(model.PlayerId)
                : await Store.List().FirstOrDefaultAsync(p =>
                    p.UserId == model.UserId &&
                    p.GameId == manager.GameId
                )
            ;

            if (player is not Data.Player)
            {
                //returns the model, but we want the entity
                var tmp = await Register(new NewPlayer{
                    UserId = model.UserId,
                    GameId = manager.GameId
                });

                player = await Store.Retrieve(tmp.Id);
            }

            if (player.Id == manager.Id)
                return Mapper.Map<Player>(player);

            if (!sudo && !manager.Game.RegistrationActive)
                throw new RegistrationIsClosed();

            if (!sudo && manager.Game.RequireSponsoredTeam && !manager.Sponsor.Equals(player.Sponsor))
                throw new RequiresSameSponsor();

            int count = await Store.List().CountAsync(p => p.TeamId == manager.TeamId);

            if (!sudo && manager.Game.AllowTeam && count >= manager.Game.MaxTeamSize)
                throw new TeamIsFull();

            player.TeamId = manager.TeamId;
            player.Name = manager.Name;
            player.ApprovedName = manager.ApprovedName;
            player.Role = PlayerRole.Member;

            await Store.Update(player);

            return  Mapper.Map<Player>(player);
        }

        public async Task<Team> LoadTeam(string id, bool sudo)
        {
            var players = await Store.ListTeam(id);

            var team = Mapper.Map<Team>(
                players.First(p => p.IsManager)
            );

            team.Members = Mapper.Map<TeamMember[]>(
                players.Select(p => p.User)
            );

            // TODO: consider display of challenge detail after game closed
            // if (sudo || !players.First().Game.IsLive)
            if (sudo)
                team.Challenges = Mapper.Map<TeamChallenge[]>(
                    await Store.ListTeamChallenges(id)
                );

            return team;
        }

        public async Task AdvanceTeam(TeamAdvancement model)
        {
            var game = await GameStore.Retrieve(model.NextGameId);

            var team = await Store.ListTeam(model.TeamId);

            var enrollments = new List<Data.Player>();

            foreach(var player in team)
                enrollments.Add(new Data.Player {
                    TeamId = player.TeamId,
                    UserId = player.UserId,
                    GameId = model.NextGameId,
                    ApprovedName = player.ApprovedName,
                    Name = player.Name,
                    Sponsor = player.Sponsor,
                    Role = player.Role,
                    Rank = player.Rank
                });

            await Store.Create(enrollments);
        }

    }

}