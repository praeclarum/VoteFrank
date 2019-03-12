using System.Collections.Concurrent;

namespace VoteFrank
{
    public class Person
    {
        public string Name;

        public readonly ConcurrentBag<Race> Races = new ConcurrentBag<Race> ();

        public override string ToString() => Name;

        static readonly ConcurrentDictionary<string, Person> all = new ConcurrentDictionary<string, Person> ();

        public static Person Get(string name)
        {
            if (all.TryGetValue (name, out var p))
                return p;
            p = new Person {
                Name = name,
            };
            if (!all.TryAdd (name, p)) {
                p = all[name];
            }
            return p;
        }
    }
}
