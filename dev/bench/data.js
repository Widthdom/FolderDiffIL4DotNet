window.BENCHMARK_DATA = {
  "lastUpdate": 1787329742697,
  "repoUrl": "https://github.com/Widthdom/FolderDiffIL4DotNet",
  "entries": {
    "FolderDiffIL4DotNet Performance": [
      {
        "commit": {
          "author": {
            "email": "125688807+Widthdom@users.noreply.github.com",
            "name": "Widthdom",
            "username": "Widthdom"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "cf443f31e441d7f8d7552628f5490d69d71ae820",
          "message": "Enforce possible-null return warnings in tests (#272)",
          "timestamp": "2026-08-22T01:19:25+09:00",
          "tree_id": "9dd512babb4933faafaf6c707483100fcaf8c90b",
          "url": "https://github.com/Widthdom/FolderDiffIL4DotNet/commit/cf443f31e441d7f8d7552628f5490d69d71ae820"
        },
        "date": 1787329742605,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_100",
            "value": 28759.44034295333,
            "unit": "ns",
            "range": "± 979.4399863890293"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_1000",
            "value": 277927.43396809895,
            "unit": "ns",
            "range": "± 2051.2376217437445"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_10000",
            "value": 2911666.9455915177,
            "unit": "ns",
            "range": "± 16704.702744472026"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.HashCompare_SmallFile",
            "value": 34731.67686767578,
            "unit": "ns",
            "range": "± 252.15803447179948"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_ShortPath",
            "value": 24.307144230604173,
            "unit": "ns",
            "range": "± 0.4625682415848111"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_LongPath",
            "value": 65.94389507671197,
            "unit": "ns",
            "range": "± 1.732090967661151"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_UnicodePath",
            "value": 22.934270183245342,
            "unit": "ns",
            "range": "± 0.27646136357303597"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.TextDiffer_IdenticalLargeFile",
            "value": 4178863.759982639,
            "unit": "ns",
            "range": "± 206096.61281892407"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.TextDiffer_CompletelyDifferentSmallFiles",
            "value": 91550.71375325522,
            "unit": "ns",
            "range": "± 1025.0824409376046"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.SmallFile_5Changes",
            "value": 2493.349285826391,
            "unit": "ns",
            "range": "± 145.19902171264596"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.MediumFile_20Changes",
            "value": 382285.44700195314,
            "unit": "ns",
            "range": "± 32124.230335634144"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.LargeFile_10Changes",
            "value": 41908705.61236801,
            "unit": "ns",
            "range": "± 1684330.8583563203"
          }
        ]
      }
    ]
  }
}