using System;
using System.Linq;
using System.Net.Http;

namespace VoteFrank
{
    public class Election
    {
        public int Year { get; }
        public int Month { get; }
        public string Kind { get; }
        public string DataId { get; }
        public string CsvDataUrl => $"https://data.kingcounty.gov/resource/{DataId}.csv";
        public string CsvData { get; }
        public ElectionResult[] Results { get; }

        public Election (int year, int month, string kind, string dataId)
        {
            Year = year;
            Month = month;
            Kind = kind;
            DataId = dataId;
            if (!string.IsNullOrEmpty(dataId)) {
                Console.WriteLine(CsvDataUrl);
                CsvData = CsvDataUrl;
            }
            else {
                CsvData = "";
            }
        }

        static readonly HttpClient http = new HttpClient ();

        public static readonly Election[] All = {
            new Election (2018, 11, "General", "ghxg-x8xz"),
            new Election (2017, 11, "General", "xmvr-b3my"),
            new Election (2016, 11, "General", "b27z-cdmk"),
            new Election (2015, 11, "General", "kncv-f6kh"),
            new Election (2014, 11, "General", "44iw-f49v"),
            new Election (2013, 11, "General", "vrn2-xcr7"),
            new Election (2012, 11, "General", "u6ig-5qm8"),
            new Election (2011, 11, "General", "hgu2-qaye"),
            new Election (2010, 11, "General", "jet5-cigp"),
        };

        public static Election Get (int year, int month) => All.First (x => x.Year == year && x.Month == month);
    }
}
