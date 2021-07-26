// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Gameboard.Api.Data.Abstractions;

namespace Gameboard.Api.Data
{

    public class ChallengeStore: Store<Challenge>, IChallengeStore
    {
        public ChallengeStore(GameboardDbContext dbContext)
        :base(dbContext)
        {

        }

        public async Task<Challenge> Load(NewChallenge model)
        {
            var player = await DbContext.Players.FindAsync(model.PlayerId);

            return await DbSet
                .Include(c => c.Player)
                .FirstOrDefaultAsync(c =>
                    c.SpecId == model.SpecId &&
                    (
                        c.PlayerId == model.PlayerId ||
                        c.TeamId == player.TeamId
                    )
                )
            ;
        }

        public async Task UpdateEtd(string specId)
        {
            var stats = await DbSet.Where(c => c.SpecId == specId)
                .Select(c => new { Created = c.WhenCreated, Started = c.StartTime })
                .OrderByDescending(m => m.Created)
                .Take(20)
                .ToArrayAsync();

            int avg = (int) stats.Average(m =>
                m.Started.Subtract(m.Created).TotalSeconds
            );

            var spec = await DbContext.ChallengeSpecs.FindAsync(specId);

            spec.AverageDeploySeconds = avg;

            await DbContext.SaveChangesAsync();
        }

        public async Task UpdateTeam(string id)
        {
            var challenges = await DbSet.Where(c => c.TeamId == id).ToArrayAsync();

            // TODO: reconsider int vs double
            int score = (int)challenges.Sum(c => c.Score);

            long time = challenges.Sum(c => c.Duration);
            int complete = challenges.Count(c => c.Result == ChallengeResult.Success);
            int partial = challenges.Count(c => c.Result == ChallengeResult.Partial);


            var players = await DbContext.Players.Where(p => p.TeamId == id).ToArrayAsync();

            foreach (var p in players)
            {
                p.Score = score;
                p.Time = time;
                p.CorrectCount = complete;
                p.PartialCount = partial;
            }

            await DbContext.SaveChangesAsync();
        }

        // If entity has searchable fields, use this:
        // public override IQueryable<Challenge> List(string term = null)
        // {
        //     var q = base.List();

        //     if (!string.IsNullOrEmpty(term))
        //     {
        //         term = term.ToLower();

        //         q = q.Where(t =>
        //             t.Name.ToLower().Contains(term)
        //         );
        //     }

        //     return q;
        // }

    }
}