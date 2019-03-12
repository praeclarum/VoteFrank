using System.Collections.Concurrent;
using System.Threading;

namespace VoteFrank
{
    public class Person
    {
        public string Name;

        public int NumGeneralWins;
        public int NumGeneralLosses;
        public int NumPrimaryWins;
        public int NumPrimaryLosses;

        public ConcurrentBag<Race> Races = new ConcurrentBag<Race> ();

        private Person(string name)
        {
            Name = name;
        }
        public Person()
        {
            Name = "";
        }

        public override string ToString() => Name;

        public void AddRaceResult(Race race, int rank)
        {
            switch (race.Election.Kind)
            {
                case "General":
                    if (rank == 1) {
                        Interlocked.Increment(ref NumGeneralWins);
                    }
                    else {
                        Interlocked.Increment(ref NumGeneralLosses);
                    }
                    break;
                case "Primary":
                    if (rank == 1 || rank == 2) {
                        Interlocked.Increment(ref NumPrimaryWins);
                    }
                    else {
                        Interlocked.Increment(ref NumPrimaryLosses);
                    }
                    break;
            }
        }

        static readonly ConcurrentDictionary<string, Person> all = new ConcurrentDictionary<string, Person> ();

        public static Person Get(string name)
        {
            if (all.TryGetValue (name, out var p))
                return p;
            p = new Person (name);
            if (!all.TryAdd (name, p)) {
                p = all[name];
            }
            return p;
        }
    }
}
