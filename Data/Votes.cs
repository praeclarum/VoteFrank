using System;
using System.Collections.Concurrent;

namespace VoteFrank
{
    public class Votes
    {
        public Race Race;
        public Person Candidate;
        public int TotalCount;
        public readonly ConcurrentDictionary<string, int> PrecinctCounts = new ConcurrentDictionary<string, int> ();

        public void Add(Precinct precinct, int votes)
        {
            TotalCount += votes;
            PrecinctCounts[precinct.Title] = votes;
        }

        public override string ToString() => $"{Candidate} = {TotalCount}";
    }
}
