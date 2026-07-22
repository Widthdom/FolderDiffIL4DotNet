window.BENCHMARK_DATA = {
  "lastUpdate": 1784710626215,
  "repoUrl": "https://github.com/Widthdom/FolderDiffIL4DotNet",
  "entries": {
    "FolderDiffIL4DotNet Performance": [
      {
        "commit": {
          "author": {
            "email": "widthdom@gmail.com",
            "name": "Widthdom",
            "username": "Widthdom"
          },
          "committer": {
            "email": "widthdom@gmail.com",
            "name": "Widthdom",
            "username": "Widthdom"
          },
          "distinct": true,
          "id": "acada79b3b3b474b563330f00f3a99685e4f2573",
          "message": "Bump version to 1.21.0",
          "timestamp": "2026-07-22T17:51:39+09:00",
          "tree_id": "fa1b43f27d1c49bec6d8e027e72d2f6734e7c0f8",
          "url": "https://github.com/Widthdom/FolderDiffIL4DotNet/commit/acada79b3b3b474b563330f00f3a99685e4f2573"
        },
        "date": 1784710626169,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_100",
            "value": 62300.1653489333,
            "unit": "ns",
            "range": "± 283.63287562317026"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_1000",
            "value": 586139.1196664664,
            "unit": "ns",
            "range": "± 2891.0467292324656"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_10000",
            "value": 5861635.277901785,
            "unit": "ns",
            "range": "± 32354.58067817836"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.HashCompare_SmallFile",
            "value": 77961.04541015625,
            "unit": "ns",
            "range": "± 258.05964532648295"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_ShortPath",
            "value": 30.648519039154053,
            "unit": "ns",
            "range": "± 0.34857387184758715"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_LongPath",
            "value": 62.087791689804625,
            "unit": "ns",
            "range": "± 0.6966533939488118"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.Sanitize_UnicodePath",
            "value": 30.907505361239114,
            "unit": "ns",
            "range": "± 0.3831111888078619"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.TextDiffer_IdenticalLargeFile",
            "value": 5132326.361979167,
            "unit": "ns",
            "range": "± 28521.569661511774"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.ILComparisonBenchmarks.TextDiffer_CompletelyDifferentSmallFiles",
            "value": 127969.86427217371,
            "unit": "ns",
            "range": "± 2594.578845757665"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.SmallFile_5Changes",
            "value": 2740.562447611491,
            "unit": "ns",
            "range": "± 49.16661891668211"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.MediumFile_20Changes",
            "value": 265153.6511579241,
            "unit": "ns",
            "range": "± 1457.219674961724"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.LargeFile_10Changes",
            "value": 28348541.910416666,
            "unit": "ns",
            "range": "± 266193.049105646"
          }
        ]
      }
    ]
  }
}