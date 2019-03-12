using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VoteFrank
{
    public class Election
    {
        public int Year { get; }
        public int Month { get; }
        public string Kind { get; }
        public string DataId { get; }
        public string CsvDataUrl => $"https://data.kingcounty.gov/api/views/{DataId}/rows.csv?accessType=DOWNLOAD&api_foundry=true";
        public string CsvData { get; private set; } = "";
        public DateTime Date => new DateTime (Year, Month, 1);

        readonly Dictionary<string, Race> races = new Dictionary<string, Race> ();
        public Race[] Races { get; private set; } = Array.Empty<Race> ();

        public Election (int year, int month, string kind, string dataId)
        {
            Year = year;
            Month = month;
            Kind = kind;
            DataId = dataId;
        }

        bool loaded = false;

        void Load ()
        {
            if (loaded)
                return;
            loaded = true;
            // Read CSV
            if (!string.IsNullOrEmpty(DataId)) {
                var homeDir = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(homeDir)) {
                    homeDir = Environment.CurrentDirectory;
                }
                var dataDir = Path.Combine(homeDir, "Data");
                if (!Directory.Exists (dataDir))
                    Directory.CreateDirectory (dataDir);
                var edataDir = Path.Combine(dataDir, "ElectionResults");
                if (!Directory.Exists (edataDir))
                    Directory.CreateDirectory (edataDir);
                var path = Path.Combine(edataDir, DataId + ".csv");
                var content = "";
                if (File.Exists(path)) {
                    content = File.ReadAllText (path);
                }
                else {
                    try {
                        content = http.GetStringAsync(CsvDataUrl).Result;
                        File.WriteAllText(path, content);
                    }
                    catch (Exception ex) {
                        System.Console.WriteLine(ex);
                    }
                }
                CsvData = content;
            }
            else {
                CsvData = "";
            }

            // Process CSV
            if (!string.IsNullOrEmpty (CsvData)) {
                System.Console.WriteLine($"Loading {Year}/{Month} {Kind} {DataId}...");
                var lines = Csv.CsvReader.Read (new StringReader(CsvData));
                var count = 0;
                var k_precinct = "precinct";
                var k_race = "race";
                var k_countertype = "countertype";
                var k_sumofcount = "sumofcount";
                var hasHeader = false;
                var laste = "";
                foreach (var l in lines) {
                    count++;
                    try {
                        if (!hasHeader) {
                            hasHeader = true;
                            if (l.Headers.Contains("Precinct")) k_precinct = "Precinct";
                            if (l.Headers.Contains("Race")) k_race = "Race";
                            if (l.Headers.Contains("CounterType")) k_countertype = "CounterType";
                            if (l.Headers.Contains("SumOfCount")) k_sumofcount = "SumOfCount";
                        }
                        if (!l[k_precinct].StartsWith("SEA"))
                            continue;
                        var precinct = Precinct.Get(l[k_precinct]);
                        var race = GetRace(l[k_race]);
                        // System.Console.WriteLine($"{race} {precinct}");
                        switch (l[k_countertype]) {
                            case "Times Blank Voted":
                            case "Times Counted":
                            case "Times Over Voted":
                            case "Times Under Voted":
                            case "Write-In":
                            case "Write-in":
                            case "Registered Voters":
                                break;
                            case "Approved":
                            case "Rejected":
                            case "Yes":
                            case "No":
                            case "Maintained":
                            case "Repealed":
                                break;
                            default:
                                {
                                    var name = Normalize(l[k_countertype], nameNorms);
                                    var candidate = Person.Get(name);
                                    race.AddVotes (candidate, int.Parse(l[k_sumofcount]), precinct);
                                }
                                break;
                        }
                    }
                    catch (Exception ex) {
                        var e = ex.ToString ();
                        if (e != laste) {
                            laste = e;
                            System.Console.WriteLine(e);
                        }
                    }
                }
                System.Console.WriteLine($"{DataId} == {count}");
            }

            Races = races.Values.Where (x => x.Votes.Count > 0).OrderBy (x => x.Position.Title).ToArray ();
            foreach (var r in Races) {
                r.DeclareWinner ();
            }
        }

        Race GetRace(string rawPosition)
        {
            var ipart = rawPosition.IndexOf("partisan office");
            if (ipart > 0)
                rawPosition = rawPosition.Substring(0, ipart).Trim();
            var position = Normalize(rawPosition, positionNorms);
            if (races.TryGetValue (position, out var race))
                return race;
            var pposition = Position.Get (position);
            race = new Race {
                Election = this,
                Position = pposition,
            };
            races[position] = race;
            pposition.Races.Add (race);
            return race;
        }

        static readonly (Regex, string)[] positionNorms = new (Regex, string)[] {
            (new Regex(@"Legislative District (\d+) Representative Position (\d+)"),         "Legislative District No. $1 Representative Position No. $2"),
            (new Regex(@"State Representative Legislative Dist No. (\d+) - Position (\d+)"), "Legislative District No. $1 Representative Position No. $2"),
            (new Regex(@"US Representative Congressional District (\d+)"),                   "Congressional District No. $1 US Representative"),
            (new Regex(@"US Representative Congressional District No. (\d+)"),               "Congressional District No. $1 US Representative"),
            (new Regex(@"United States Representative Congressional District No. (\d+)"),    "Congressional District No. $1 US Representative"),
            (new Regex(@"Congressional District (\d+)"),                                     "Congressional District No. $1 US Representative"),
            (new Regex(@"SEA ([0-9\-]+) SEA ([0-9\-]+) PCO"),                                "PCO SEA $1"),
            (new Regex(@"President and Vice President of the United States"),                "US President & Vice President"),            
        };

        static readonly (Regex, string)[] nameNorms = new (Regex, string)[] {
            (new Regex(@"Lorena Gonzalez"), "M. Lorena González"),
        };

        static string Normalize (string text, (Regex, string)[] norms)
        {
            foreach (var (regex, repl) in norms) {
                var m = regex.Match (text);
                if (m.Success) {
                    return regex.Replace(text, repl);
                }
            }
            return text;
        }

        static readonly HttpClient http = new HttpClient ();

        public static readonly Election[] All = {
            new Election (2018, 11, "General", "ghxg-x8xz"),
            new Election (2018, 8, "Primary", "juuz-29xu"),
            new Election (2017, 11, "General", "xmvr-b3my"),
            new Election (2017, 8, "Primary", "u623-b62i"),
            new Election (2016, 11, "General", "b27z-cdmk"),
            new Election (2016, 8, "Primary", "d9qg-mtfe"),
            new Election (2015, 11, "General", "kncv-f6kh"),
            new Election (2015, 8, "Primary", "pyps-tcwb"),
            new Election (2014, 11, "General", "44iw-f49v"),
            new Election (2013, 11, "General", "vrn2-xcr7"),
            new Election (2012, 11, "General", "u6ig-5qm8"),
            new Election (2011, 11, "General", "hgu2-qaye"),
            new Election (2010, 11, "General", "jet5-cigp"),
        };

        static Election()
        {
            // Parallel.ForEach(All, e => e.Load ());
            foreach (var e in All) e.Load ();
        }

        public static Election Get (int year, int month) => All.First (x => x.Year == year && x.Month == month);
    }
}
