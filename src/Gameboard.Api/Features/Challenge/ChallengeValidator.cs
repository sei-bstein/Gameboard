// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;
using Gameboard.Api.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Validators
{
    public class ChallengeValidator: IModelValidator
    {
        private readonly IChallengeStore _store;

        public ChallengeValidator(
            IChallengeStore store
        )
        {
            _store = store;
        }

        public Task Validate(object model)
        {
            if (model is Entity)
                return _validate(model as Entity);

            if (model is NewChallenge)
                return _validate(model as NewChallenge);

            throw new System.NotImplementedException();
        }

        private async Task _validate(PlayerDataFilter model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(Entity model)
        {
            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }


        private async Task _validate(NewChallenge model)
        {
            if ((await PlayerExists(model.PlayerId)).Equals(false))
                throw new ResourceNotFound();

            if ((await SpecExists(model.SpecId)).Equals(false))
                throw new ResourceNotFound();

            var player = await _store.DbContext.Players.FindAsync(model.PlayerId);

            if (player.IsLive.Equals(false))
              throw new SessionNotActive();

            var spec = await _store.DbContext.ChallengeSpecs.FindAsync(model.SpecId);

            if (spec.GameId != player.GameId)
              throw new ActionForbidden();

            // Note: not checking "already exists" since this is used idempotently

            await Task.CompletedTask;
        }

        private async Task<bool> Exists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.Retrieve(id)) is Data.Challenge
            ;
        }

        private async Task<bool> GameExists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.DbContext.Games.FindAsync(id)) is Data.Game
            ;
        }

        private async Task<bool> SpecExists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.DbContext.ChallengeSpecs.FindAsync(id)) is Data.ChallengeSpec
            ;
        }

        private async Task<bool> PlayerExists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.DbContext.Players.FindAsync(id)) is Data.Player
            ;
        }
    }
}
