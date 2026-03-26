window.BENCHMARK_DATA = {
  "lastUpdate": 1774500110288,
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
          "id": "83b89f15e4296bf8124307b82367c78fd47cf9a3",
          "message": "Merge pull request #91 from Widthdom/claude/fix-pipeline-failure-r1InY",
          "timestamp": "2026-03-26T13:38:29+09:00",
          "tree_id": "788e2fdc61e0a6be80305597b4fcc74a068d3e78",
          "url": "https://github.com/Widthdom/FolderDiffIL4DotNet/commit/83b89f15e4296bf8124307b82367c78fd47cf9a3"
        },
        "date": 1774500109888,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_100",
            "value": 38285.53485921224,
            "unit": "ns",
            "range": "± 112.93067583909456"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_1000",
            "value": 374614.0434945914,
            "unit": "ns",
            "range": "± 582.2003887247816"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.EnumerateFiles_10000",
            "value": 3925353.675520833,
            "unit": "ns",
            "range": "± 13914.137352730355"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.FolderDiffBenchmarks.HashCompare_SmallFile",
            "value": 49786.16810709635,
            "unit": "ns",
            "range": "± 135.5806506299013"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.SmallFile_5Changes",
            "value": 2970.655214670542,
            "unit": "ns",
            "range": "± 100.45210984329258"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.MediumFile_20Changes",
            "value": 298053.51381835935,
            "unit": "ns",
            "range": "± 1854.5640865786486"
          },
          {
            "name": "FolderDiffIL4DotNet.Benchmarks.TextDifferBenchmarks.LargeFile_10Changes",
            "value": 40403374.43406594,
            "unit": "ns",
            "range": "± 501047.50675205834"
          }
        ]
      }
    ]
  }
}