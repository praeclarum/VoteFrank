using System;
using System.Collections.Concurrent;
using System.Linq;

namespace VoteFrank
{
    public class Race
    {
        public Election Election;
        public Position Position;
        public readonly ConcurrentDictionary<string, Votes> Votes = new ConcurrentDictionary<string, Votes> ();
        public Person Winner;
        public DateTime Date => Election.Date;

        public override string ToString() => $"{Position} > {Winner} ({Votes.Count})";

        public void AddVotes(Person candidate, int votes, Precinct precinct)
        {
            if (!Votes.TryGetValue (candidate.Name, out var vs)) {
                vs = new Votes {
                    Race = this,
                    Candidate = candidate,
                };
                candidate.Races.Add(this);
                if (!Votes.TryAdd (candidate.Name, vs)) {
                    vs = Votes[candidate.Name];
                }
            }
            vs.Add (precinct, votes);
        }

        public void DeclareWinner()
        {
            var list = Votes.OrderByDescending (x => x.Value.TotalCount).ToList ();
            Winner = list.Count == 0 ? null : list.First().Value.Candidate;
            for (int i = 0; i < list.Count; i++) {
                var v = list[i].Value;
                v.Candidate.AddRaceResult (this, i + 1);
            }
        }
    }
}
