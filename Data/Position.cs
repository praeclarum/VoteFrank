using System.Collections.Concurrent;
using System.Linq;

namespace VoteFrank
{
    public class Position
    {
        public string Title = "";

        public readonly ConcurrentBag<Race> Races = new ConcurrentBag<Race> ();

        public override string ToString() => Title;

        static readonly ConcurrentDictionary<string, Position> all = new ConcurrentDictionary<string, Position> ();

        public static Position[] All => all.Values.ToArray ();

        public static Position Get(string title)
        {
            if (all.TryGetValue (title, out var p))
                return p;
            p = new Position {
                Title = title,
            };
            if (!all.TryAdd (title, p)) {
                p = all[title];
            }
            return p;
        }
    }
}
