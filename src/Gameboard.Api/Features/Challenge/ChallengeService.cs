// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Gameboard.Api.Data.Abstractions;
using TopoMojo.Api.Client;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Gameboard.Api.Services
{
    public class ChallengeService : _Service
    {
        IChallengeStore Store { get; }
        ITopoMojoApiClient Mojo { get; }

        private IMemoryCache _localcache;

        public ChallengeService(
            ILogger<ChallengeService> logger,
            IMapper mapper,
            CoreOptions options,
            IChallengeStore store,
            ITopoMojoApiClient mojo,
            IMemoryCache localcache
        ) : base(logger, mapper, options)
        {
            Store = store;
            Mojo = mojo;
            _localcache = localcache;
        }

        public async Task<Challenge> GetOrAdd(NewChallenge model)
        {
            var entity = await Store.Load(model);

            if (entity is not null)
                return Mapper.Map<Challenge>(entity);

            var player = await Store.DbContext.Players.FindAsync(model.PlayerId);
            var game = await Store.DbContext.Games.FindAsync(player.GameId);
            var spec = await Store.DbContext.ChallengeSpecs.FindAsync(model.SpecId);

            entity = Mapper.Map<Data.Challenge>(model);

            Mapper.Map(spec, entity);

            entity.Player = player;

            entity.TeamId = player.TeamId;

            var state = await Mojo.RegisterGamespaceAsync(new GamespaceRegistration
            {
                Players = new RegistrationPlayer[] {
                    new RegistrationPlayer {
                        SubjectId = player.TeamId,
                        SubjectName = player.Name
                    }
                },
                ResourceId = entity.ExternalId,
                Variant = model.Variant,
                Points = spec.Points,
                MaxAttempts = game.MaxAttempts,
                StartGamespace = true,
                ExpirationTime = entity.Player.SessionEnd
            });

            Transform(state);

            Mapper.Map(state, entity);

            await Store.Create(entity);

            await Store.UpdateEtd(entity.SpecId);

            return Mapper.Map<Challenge>(entity);
        }

        public async Task<Challenge> Retrieve(string id)
        {
            var result = Mapper.Map<Challenge>(
                await Store.Retrieve(id)
            );

            return result;
        }

        public async Task Update(ChangedChallenge model)
        {
            var entity = await Store.Retrieve(model.Id);

            Mapper.Map(model, entity);

            await Store.Update(entity);
        }

        public async Task Delete(string id)
        {
            await Store.Delete(id);
            await Mojo.DeleteGamespaceAsync(id);
        }

        public async Task<bool> UserIsTeamPlayer(string id, string subjectId)
        {
            var entity = await Store.Retrieve(id);

            return await Store.DbContext.Users.AnyAsync(u =>
                u.Id == subjectId &&
                u.Enrollments.Any(e => e.TeamId == entity.TeamId)
            );
        }

        public async Task<Challenge[]> List(SearchFilter model)
        {
            var q = Store.List(model.Term);

            q = q.OrderBy(p => p.Tag);

            q = q.Skip(model.Skip);

            if (model.Take > 0)
                q = q.Take(model.Take);

            return await Mapper.ProjectTo<Challenge>(q).ToArrayAsync();
        }

        public async Task<Challenge> Preview(NewChallenge model)
        {
            var entity = await Store.Load(model);

            if (entity is Data.Challenge)
                return Mapper.Map<Challenge>(entity);

            var spec = await Store.DbContext.ChallengeSpecs.FindAsync(model.SpecId);

            //check preview cache, else mojo
            var cachestate = _localcache.Get<string>(spec.ExternalId);

            if (cachestate == null)
            {
                var state = await Mojo.PreviewGamespaceAsync(spec.ExternalId);

                Transform(state);

                cachestate = JsonSerializer.Serialize(state);

                if (cachestate != null)
                    _localcache.Set(spec.ExternalId, cachestate, new TimeSpan(0, 60, 0));

            }

            var challenge = Mapper.Map<Data.Challenge>(spec);

            challenge.State = cachestate;

            return Mapper.Map<Challenge>(challenge);
        }

        private async Task<Data.Challenge> Sync(Data.Challenge entity, Task<GameState> task = null)
        {
            if (task is null)
                task = Mojo.LoadGamespaceAsync(entity.Id);

            var state = await task;

            Mapper.Map(state, entity);

            await Store.Update(entity);

            return entity;
        }

        private async Task<Data.Challenge> Sync(string id, Task<GameState> task = null)
        {
            var entity = await Store.Retrieve(id);

            return await Sync(entity, task);
        }

        public async Task<Challenge> StartGamespace(string id)
        {
            var entity = await Sync(
                id,
                Mojo.StartGamespaceAsync(id)
            );

            return Mapper.Map<Challenge>(entity);
        }

        public async Task<Challenge> StopGamespace(string id)
        {
            var entity = await Sync(
                id,
                Mojo.StopGamespaceAsync(id)
            );

            return Mapper.Map<Challenge>(entity);
        }

        public async Task<Challenge> Grade(SectionSubmission model)
        {
            var entity = await Sync(
                model.Id,
                Mojo.GradeChallengeAsync(model)
            );

            await Store.UpdateTeam(entity.TeamId);

            return Mapper.Map<Challenge>(entity);
        }

        public async Task<ConsoleSummary> GetConsole(ConsoleRequest model, bool observer)
        {
            switch (model.Action)
            {
                case ConsoleAction.Ticket:

                    return Mapper.Map<ConsoleSummary>(
                        await Mojo.GetVmTicketAsync(model.Id)
                    );

                case ConsoleAction.Reset:

                    var vm = await Mojo.ChangeVmAsync(
                        new VmOperation
                        {
                            Id = model.Id,
                            Type = VmOperationType.Reset
                        }
                    );

                    return new ConsoleSummary
                    {
                        Id = vm.Id,
                        Name = vm.Name,
                        SessionId = model.SessionId,
                        IsRunning = vm.State == VmPowerState.Running
                    };

            }

            throw new InvalidConsoleAction();
        }

        private void Transform(GameState state)
        {
            state.Markdown = state.Markdown.Replace("](/docs", $"]({Options.ChallengeDocUrl}docs");

            if (state.Challenge is not null)
                state.Challenge.Text = state.Challenge.Text.Replace("](/docs", $"]({Options.ChallengeDocUrl}docs");
        }
    }

}