using System;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Linq;
using NGraphics;

namespace VoteFrank
{
    public class Precinct
    {
        public string Title;

        public override string ToString() => Title;

        static readonly ConcurrentDictionary<string, Precinct> all = new ConcurrentDictionary<string, Precinct> ();

        public static Precinct[] All => all.Values.ToArray ();

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

    public class PrecinctShape
    {
        public readonly string Name;
        public readonly Point[] Coordinates;
        public PrecinctShape (string name, Point[] coords)
        {
            Name = name;
            Coordinates = coords;
        }
        public static PrecinctShape GetShape (string precinct)
        {
            if (shapes.TryGetValue (precinct, out var p))
                return p;
            return null;
        }

        static readonly ConcurrentDictionary<string, PrecinctShape> shapes =
            new ConcurrentDictionary<string, PrecinctShape> ();
        static PrecinctShape ()
        {
            var asm = typeof(PrecinctShape).Assembly;
            var zipPath = "VoteFrank.Data.PrecinctShapes.zip";
            using (var s = asm.GetManifestResourceStream (zipPath)) {
                using (var a = new ZipArchive (s, ZipArchiveMode.Read)) {
                    var e = a.Entries.First (x => x.Name == "PrecinctShapes.txt");
                    using (var es = e.Open ())
                    using (var sr = new System.IO.StreamReader (es)) {
                        for (var line = sr.ReadLine (); line != null; line = sr.ReadLine ()) {
                            if (string.IsNullOrWhiteSpace (line))
                                continue;
                            var cs = line.Split (':');
                            var n = cs[0].Trim();
                            var coords =
                                cs[1].Split (' ')
                                .Select (c => {
                                    var xs = c.Split(',');
                                    var lon = double.Parse(xs[0]);
                                    var lat = double.Parse(xs[1]);
                                    var lat_rad = lat * Math.PI / 180.0;
                                    var x = (lon + 180.0) / 360.0;
                                    var y = (1.0 - Math.Log(Math.Tan(lat_rad) + (1 / Math.Cos(lat_rad))) / Math.PI) / 2.0;
                                    return new Point (x, y);
                                })
                                .ToArray ();
                            var shape = new PrecinctShape (n, coords);
                            shapes[n] = shape;
                            // Console.WriteLine ($"RRRR {n}: {shape.Coordinates.Length}");
                        }
                    }
                }
            }
        }
    }
}
