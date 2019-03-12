using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VoteFrank
{
    public class Election
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Kind { get; set; }
        public string DataId { get; set; }
        public string CsvDataUrl => $"https://data.kingcounty.gov/api/views/{DataId}/rows.csv?accessType=DOWNLOAD&api_foundry=true";
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

        public Election ()
        {
            Kind = "";
            DataId = "";
        }

        bool loaded = false;

        static readonly Lazy<string> dataDirL = new Lazy<string> (() => {
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
            return edataDir;
        });

        void Load ()
        {
            if (loaded)
                return;
            loaded = true;
            LoadCsv ();
        }

        void LoadCsv ()
        {
            // Read CSV
            var csvData = "";
            if (!string.IsNullOrEmpty(DataId)) {
                var edataDir = dataDirL.Value;
                var csvPath = Path.Combine(edataDir, DataId + ".csv");
                var content = "";
                if (File.Exists(csvPath)) {
                    content = File.ReadAllText (csvPath);
                }
                else {
                    try {
                        content = http.GetStringAsync(CsvDataUrl).Result;
                        File.WriteAllText(csvPath, content);
                    }
                    catch (Exception ex) {
                        System.Console.WriteLine(ex);
                    }
                }
                csvData = content;
            }
            else {
                csvData = "";
            }

            //
            // Process CSV
            //
            if (!string.IsNullOrEmpty (csvData)) {
                System.Console.WriteLine($"Loading {Year}/{Month} {Kind} {DataId}...");

                var csvMemory = csvData.AsMemory ();
                var csvCols = new string[100];
                var csvRem = ReadCsvLine (csvMemory, csvCols, out var numHeaderColumns);
                var i_precinct = GetCsvColumn (csvCols, numHeaderColumns, "precinct", "Precinct");
                var i_race = GetCsvColumn (csvCols, numHeaderColumns, "race", "Race");
                var i_countertype = GetCsvColumn (csvCols, numHeaderColumns, "countertype", "CounterType");
                var i_sumofcount = GetCsvColumn (csvCols, numHeaderColumns, "sumofcount", "SumOfCount");

                var numColumns = numHeaderColumns;
                var count = 0;
                var laste = "";
                while (csvRem.Length >= numHeaderColumns) {
                    csvRem = ReadCsvLine (csvRem, csvCols, out numColumns);
                    if (numColumns != numHeaderColumns) {
                        System.Console.WriteLine("BAD LINE: " + string.Join(",", csvCols.Take(numColumns)));
                        continue;
                    }
                    var l = csvCols;
                    count++;
                    try {
                        if (!l[i_precinct].StartsWith("SEA"))
                            continue;
                        if (!InterestedInRace (l[i_race]))
                            continue;
                        // System.Console.WriteLine($"{race} {precinct}");
                        switch (l[i_countertype]) {
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
                            case "YES":
                            case "NO":
                            case "LEVY YES":
                            case "LEVY NO":
                            case "APPROVED":
                            case "REJECTED":
                            case "Against annexation":
                            case "For annexation":
                            case "Proposition 1A":
                            case "Proposition 1B":
                                break;
                            default:
                                {
                                    var race = GetRace(l[i_race]);
                                    var precinct = Precinct.Get(l[i_precinct]);
                                    var name = Normalize(l[i_countertype], nameNorms);
                                    var candidate = Person.Get(name);
                                    race.AddVotes (candidate, int.Parse(l[i_sumofcount]), precinct);
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

        int GetCsvColumn (string[] columns, int numColumns, params string[] names)
        {
            foreach (var n in names) {
                for (int i = 0; i < numColumns; i++) {
                    var c = columns[i];
                    if (n == c) {
                        return i;
                    }
                }
            }
            throw new Exception("Column not found: " + string.Join(", ", names));
        }

        ReadOnlyMemory<char> ReadCsvLine(ReadOnlyMemory<char> textMemory, string[] columns, out int numColumns)
        {
            var text = textMemory.Span;
            var column = 0;
            var state = ReadState.WaitingForColumnStart;
            var i = 0;
            var colStartI = 0;
            var colEndI = 0;
            while (i < text.Length && state != ReadState.Done) {
                var ch = text[i];
                switch (state) {
                    case ReadState.WaitingForColumnStart:
                        if (ch == '\"') {
                            state = ReadState.ReadingEscaped;
                            colStartI = i + 1;
                            i++;
                        }
                        else if (ch == '\r') {
                            i++;
                        }
                        else if (ch == '\n') {
                            state = ReadState.Done;
                            i++;
                        }
                        else if (ch == ',') {
                            columns[column] = string.Empty;
                            column++;
                            i++;
                        }
                        else {
                            state = ReadState.Reading;
                            colStartI = i;
                            i++;
                        }
                        break;
                    case ReadState.ReadingEscaped:
                        if (ch == '\"') {
                            if ((i + 1) < text.Length && text[i+1] == '\"') {
                                i += 2;
                            }
                            else {
                                colEndI = i;
                                columns[column] = String.Intern (new String (text.Slice (colStartI, colEndI - colStartI)));
                                // Console.WriteLine ($"{column} = {columns[column]}");
                                column++;
                                i++;
                                if (i < text.Length && text[i] == ',')
                                    i++;
                                state = ReadState.WaitingForColumnStart;
                            }
                        }
                        else {
                            i++;
                        }
                        break;
                    case ReadState.Reading:
                        if (ch == ',' || ch == '\n' || (ch == '\r' && i + 1 < text.Length && text[i+1] == '\n')) {
                            colEndI = i;
                            // Console.WriteLine ($"textMemory.Slice ({colStartI}, {colEndI} - {colStartI})");
                            columns[column] = String.Intern (new String (text.Slice (colStartI, colEndI - colStartI)));
                            // Console.WriteLine ($"{column} = {columns[column]}");
                            column++;
                            i++;
                            if (ch == '\r')
                                i++;
                            state = ch == ',' ? ReadState.WaitingForColumnStart : ReadState.Done;
                        }
                        else {
                            i++;
                        }
                        break;
                    default:
                        throw new NotImplementedException ($"{state}");
                }
            }
            numColumns = column;
            return textMemory.Slice (i);
        }

        enum ReadState { WaitingForColumnStart, ReadingEscaped, Reading, Done }

        Race GetRace(string rawPosition)
        {
            var ipart = rawPosition.IndexOf("nonpartisan");
            if (ipart > 0) {
                rawPosition = rawPosition.Substring(0, ipart).Trim();
            }
            else {
                ipart = rawPosition.IndexOf("partisan");
                if (ipart > 0) {
                    rawPosition = rawPosition.Substring(0, ipart).Trim();
                }
            }
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

        bool InterestedInRace (string raceTitle)
        {
            return !raceTitle.Contains("Judge")
                   && !raceTitle.Contains("Justice")
                   && !raceTitle.Contains("School")
                   && !raceTitle.Contains("Vashon")
                   && !raceTitle.Contains("Si View")
                   && !raceTitle.Contains("North Highline")
                   && !raceTitle.StartsWith("PCO")
                   && !raceTitle.EndsWith("PCO");
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
            (new Regex(@"Port of Seattle, Commissioner Position No. (\d+)"),                 "Port of Seattle Commissioner Position No. $1"),
            (new Regex(@"King County US Senator"),                                           "US Senator"),
        };

        static readonly (Regex, string)[] nameNorms = new (Regex, string)[] {
            (new Regex(@"Lorena Gonzalez"), "M. Lorena González"),
            (new Regex(@"Goodspaceguy"), "GoodSpaceGuy"),
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

        public static Election[] All {get; private set; } = {
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
            // new Election (2010, 11, "General", "jet5-cigp"),
        };

        const int DataVersion = 3;

        static Election()
        {
            // var jsonPath = Path.Combine (dataDirL.Value, $"all{All.Length}_v{DataVersion}.json");

            // if (File.Exists(jsonPath)) {
            //     var json = File.ReadAllText (jsonPath);
            //     All = JsonConvert.DeserializeObject<Election[]> (json);
            //     System.Console.WriteLine($"LOADED {All.Length}");
            // }
            // else {
                // Parallel.ForEach(All, e => e.Load ());
                foreach (var e in All) e.Load ();

                //
                // Save json
                //
                // var jsonSettings = new JsonSerializerSettings ();
                // jsonSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
                // jsonSettings.Formatting = Formatting.None;
                // var json = JsonConvert.SerializeObject (All, jsonSettings);
                // File.WriteAllText (jsonPath, json);
            // }
        }

        public static Election Get (int year, int month) => All.First (x => x.Year == year && x.Month == month);
    }
}
