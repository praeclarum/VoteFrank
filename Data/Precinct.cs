using System.Collections.Concurrent;

namespace VoteFrank
{
    public class Precinct
    {
        public string Title;

        public override string ToString() => Title;

        static readonly ConcurrentDictionary<string, Precinct> all = new ConcurrentDictionary<string, Precinct> ();

        public static Precinct Get(string title)
        {
            if (all.TryGetValue (title, out var p))
                return p;
            p = new Precinct {
                Title = title,
            };
            if (!all.TryAdd (title, p)) {
                p = all[title];
            }
            return p;
        }
    }
}
