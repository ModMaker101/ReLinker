using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ReLinker;

class Program
{
    static void Main(string[] args)
    {
        var officialRecords = LoadOfficialCsv("csvs/official.csv");
        var ncRecords = LoadNcCsv("csvs/nc.csv");

        // Build IDF dictionary from all names
        var allNames = officialRecords.Select(r => r.Fields["LEA_NAME"])
            .Concat(ncRecords.Select(r => r.Fields["Official School Name"])).ToList();
        var idf = BuildIdfDictionary(allNames);

        var outputLines = new List<string>();
        int total = ncRecords.Count;
        int count = 0;
        int correct = 0;

        foreach (var nc in ncRecords)
        {
            string ncName = nc.Fields["Official School Name"];
            string ncFedSchoolNum = GetFieldIgnoreCase(nc.Fields, "Federal School Number");
            string ncAddress = GetFieldIgnoreCase(nc.Fields, "Address Line1");
            string ncZip = GetFieldIgnoreCase(nc.Fields, "Zip");
            string ncSchoolNum = GetFieldIgnoreCase(nc.Fields, "School Number");

            double bestScore = double.MinValue;
            Record bestMatchRecord = null;
            string bestMatchAddress = "";
            string bestMatchZip = "";

            foreach (var official in officialRecords)
            {
                string officialName = official.Fields["SCH_NAME"];
                string officialAddress = GetFieldIgnoreCase(official.Fields, "MSTREET1");
                string officialZip = GetFieldIgnoreCase(official.Fields, "MZIP");

                double score = Similarity.JaroSimilarity(ncName, officialName, idf);

                // Optionally, boost score if addresses match
                if (!string.IsNullOrWhiteSpace(ncAddress) && !string.IsNullOrWhiteSpace(officialAddress) &&
                    string.Equals(ncAddress, officialAddress, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.1; // Boost for address match
                }

                // Optionally, boost score if ZIPs match
                if (!string.IsNullOrWhiteSpace(ncZip) && !string.IsNullOrWhiteSpace(officialZip) &&
                    ncZip == officialZip)
                {
                    score += 0.05; // Boost for ZIP match
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatchRecord = official;
                    bestMatchAddress = officialAddress;
                    bestMatchZip = officialZip;
                }
            }

            string bestMatch = bestMatchRecord?.Fields["SCH_NAME"];
            string bestNcessch = GetFieldIgnoreCase(bestMatchRecord?.Fields, "NCESSCH");

            // Only consider RIGHT if both IDs are present and equal
            bool isCorrect = !string.IsNullOrWhiteSpace(ncFedSchoolNum)
                 && !string.IsNullOrWhiteSpace(bestNcessch)
                 && ncFedSchoolNum == bestNcessch;

            if (isCorrect) correct++;

            string resultLine =
                $"NC: {ncName} | Best Official Match: {bestMatch} | Similarity: {bestScore:F3} | " +
                $"Federal School Number: {ncFedSchoolNum} | NCESSCH: {bestNcessch} | " +
                $"NC Address: {ncAddress} | Official Address: {bestMatchAddress} | " +
                $"NC Zip: {ncZip} | Official Zip: {bestMatchZip} | " +
                (isCorrect ? "RIGHT" : "WRONG");

            outputLines.Add(resultLine);

            // Progress reporting
            count++;
            Console.Write($"\rProcessed {count} of {total} records ({(count * 100 / total)}%) | Last school: {ncName} | Last FSN: {ncFedSchoolNum}");
        }

        // Write all results to a single file at the end
        double percent = total > 0 ? (correct * 100.0 / total) : 0;
        outputLines.Add($"\nSuccess: {correct} / {total} ({percent:F2}%)");

        Console.WriteLine($"\nDone. Success: {correct} / {total} ({percent:F2}%). Writing results to output.txt...");
        File.WriteAllLines("output.txt", outputLines);

    }


    // Case-insensitive field lookup, safe for null dicts
    static string GetFieldIgnoreCase(Dictionary<string, string> dict, string key)
    {
        if (dict == null) return "";
        foreach (var kv in dict)
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return "";
    }

    static List<Record> LoadOfficialCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var headers = lines[0].Split(',');
        var records = new List<Record>();
        for (int i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',');
            var dict = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length && j < fields.Length; j++)
                dict[headers[j]] = fields[j];
            records.Add(new Record(dict.GetValueOrDefault("LEAID", i.ToString()), dict));

        }
        return records;
    }


    static List<Record> LoadNcCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var headers = lines[0].Trim('"').Split("\",\"");
        var records = new List<Record>();
        for (int i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Trim('"').Split("\",\"");
            var dict = new Dictionary<string, string>();
            for (int j = 0; j < headers.Length && j < fields.Length; j++)
                dict[headers[j]] = fields[j];
            records.Add(new Record(dict.GetValueOrDefault("School Number", i.ToString()), dict));

        }
        return records;
    }

    static Dictionary<string, double> BuildIdfDictionary(List<string> names)
    {
        var termCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int total = names.Count;
        foreach (var name in names)
        {
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLowerInvariant()).Distinct();
            foreach (var token in tokens)
                termCounts[token] = termCounts.GetValueOrDefault(token, 0) + 1;
        }
        var idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in termCounts)
            idf[kv.Key] = Math.Log((double)total / kv.Value);
        return idf;
    }
}
