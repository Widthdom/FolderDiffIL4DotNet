# Myers Diff Algorithm — A Comprehensive Guide

> **Scope** — This document explains the Myers diff algorithm as implemented in
> [`TextDiffer.cs`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs).
> The first half is written in English; the second half (starting from
> [日本語版](#myers-diff-アルゴリズム詳解)) provides the same content
> in Japanese. Both sections are self-contained.
>
> **Reference paper** — ["E. W. Myers,
> An O(ND) Difference Algorithm and Its Variations,
> _Algorithmica_ **1**(2), 1986."](http://www.xmailserver.org/diff2.pdf)

---

## Table of Contents

- [1. What Problem Does Diff Solve?](#1-what-problem-does-diff-solve)
- [2. Why Not the Classic LCS Approach?](#2-why-not-the-classic-lcs-approach)
- [3. The Edit Graph — A Map of All Possible Edits](#3-the-edit-graph--a-map-of-all-possible-edits)
- [4. Diagonals and D-Paths](#4-diagonals-and-d-paths)
- [5. The Forward Pass — Finding the Shortest Edit Distance D](#5-the-forward-pass--finding-the-shortest-edit-distance-d)
- [6. Backtracking — Reconstructing the Edit Script](#6-backtracking--reconstructing-the-edit-script)
- [7. Worked Example](#7-worked-example)
- [8. Complexity Analysis](#8-complexity-analysis)
- [9. Implementation in This Project](#9-implementation-in-this-project)
- [10. Trade-offs and Practical Limits](#10-trade-offs-and-practical-limits)
- [11. Further Reading](#11-further-reading)
- [Myers Diff アルゴリズム詳解](#myers-diff-アルゴリズム詳解)

---

## 1. What Problem Does Diff Solve?

Imagine you have two versions of a text file — an **old** version and a **new**
version. A _diff_ tool answers the question:

> **"What is the smallest set of insertions and deletions that transforms the
> old file into the new file?"**

For example:

```
Old (3 lines)        New (3 lines)
-------------        -------------
A                    A
B                    C
C                    B
```

One valid edit script is: _delete B at line 2, then insert C before the
remaining B_. Another (longer) script might delete every line and re-insert
everything. Among all valid scripts, we want the **shortest** — the one with
the fewest insertions and deletions. That number of edits is called the **edit
distance** D.

### Analogy — GPS Navigation

Think of it like a GPS finding the shortest route between two cities. There
are many ways to get from City A to City B, but you want the route with the
fewest turns (edits). The Myers algorithm is the GPS — it efficiently finds
the shortest route through a special "map" called the **edit graph**.

---

## 2. Why Not the Classic LCS Approach?

The textbook way to find a shortest diff is through the **Longest Common
Subsequence (LCS)** using dynamic programming. You build an N × M table (where
N = lines in old, M = lines in new) and fill every cell.

| Metric | Classic LCS | Myers Diff    |
| ------ | ----------- | ------------- |
| Time   | O(N × M)    | O(D² + N + M) |
| Space  | O(N × M)    | O(D²)         |

For two 1,000,000-line IL files that differ in only 20 lines:

- **LCS**: 1,000,000 × 1,000,000 = **1 trillion** cells — completely
  infeasible.
- **Myers**: D = 20 -> roughly 400 diagonal iterations + 2 million snake
  comparisons -> completes in **< 0.1 seconds**.

The key insight is that **D is usually small** relative to N and M. Most real
files differ by a handful of lines, not by millions. Myers diff exploits this
by making its cost proportional to the _number of changes_, not to the _file
size_.

---

## 3. The Edit Graph — A Map of All Possible Edits

The edit graph is the conceptual foundation of Myers diff. It transforms the
problem of "find the shortest diff" into "find the shortest path in a graph."

### Axes

- **x-axis** (horizontal): positions 0 to N in the old file.
- **y-axis** (vertical): positions 0 to M in the new file.

Every point (x, y) represents a state: "we have consumed x lines from old and
y lines from new."

### Three Types of Moves

```
               old[0]         old[1]         old[2]
                 A              B              C
          +--------------+--------------+--------------+
          |              |              |              |
          |              |              |              |
          |              |              |              |
 new[0] A |    (0,0)-----+--->(1,0)-----+--->(2,0)-----+--->(3,0)
          |      |\      |      |       |      |       |
          |      | \     |      |       |      |       |
          |      |  \    |      |       |      |       |
          |      |   \   |      |       |      |       |
          |      |    \  |      |       |      |       |
          |      |     \ |      |       |      |       |
          |      v      \|      v       |      v       |
 new[1] C |    (0,1)-----+--->(1,1)-----+--->(2,1)-----+--->(3,1)
          |      |       |      |       |      |\      |
          |      |       |      |       |      | \     |
          |      |       |      |       |      |  \    |
          |      |       |      |       |      |   \   |
          |      |       |      |       |      |    \  |
          |      |       |      |       |      |     \ |
          |      v       |      v       |      v      \|
 new[2] B |    (0,2)-----+--->(1,2)-----+--->(2,2)-----+--->(3,2)
          |      |       |      |\      |      |       |
          |      |       |      | \     |      |       |
          |      |       |      |  \    |      |       |
          |      |       |      |   \   |      |       |
          |      |       |      |    \  |      |       |
          |      |       |      |     \ |      |       |
          |      v       |      v      \|      v       |
          |    (0,3)-----+--->(1,3)-----+--->(2,3)-----+--->(3,3)
          |              |              |              |
          |              |              |              |
          |              |              |              |
          +--------------+--------------+--------------+

Legend:
  --->  Horizontal move (x+1, y)   = DELETE old[x]
  v     Vertical move   (x, y+1)   = INSERT new[y]
  \     Diagonal move   (x+1, y+1) = MATCH  (old[x] == new[y])  -- FREE!
```

| Move       | Direction            | Meaning                      | Cost          |
| ---------- | -------------------- | ---------------------------- | ------------- |
| Horizontal | (x, y) -> (x+1, y)   | Delete `old[x]`              | 1 edit        |
| Vertical   | (x, y) -> (x, y+1)   | Insert `new[y]`              | 1 edit        |
| Diagonal   | (x, y) -> (x+1, y+1) | Match — `old[x]` == `new[y]` | **0** (free!) |

**Goal**: find a path from **(0, 0)** to **(N, M)** that uses the **fewest**
horizontal + vertical moves. Diagonal moves are free — they represent lines
that are already the same in both files.

### Snakes

A **snake** is a maximal sequence of consecutive diagonal moves. When the
algorithm lands on a point where `old[x]` == `new[y]`, it "slides" diagonally as
far as possible — consuming all matching lines in one go. Snakes are free, so
the longer the better.

```
A snake starting at (1,1):

  (1,1) -\- (2,2) -\- (3,3) -\- (4,4)
          ^          ^          ^
        match      match      match

  Three matching lines consumed for free!
```

---

## 4. Diagonals and D-Paths

### Diagonal Number k

Every point (x, y) in the edit graph lies on a **diagonal** identified by:

```
k = x − y
```

- k = 0 is the main diagonal (from top-left toward bottom-right).
- k > 0 means we have consumed more old lines than new (net deletions).
- k < 0 means we have consumed more new lines than old (net insertions).

```
           x = 0    x = 1    x = 2    x = 3
         +--------+--------+--------+--------+
 y = 0   | k = 0  | k = 1  | k = 2  | k = 3  |
         +--------+--------+--------+--------+
 y = 1   | k = -1 | k = 0  | k = 1  | k = 2  |
         +--------+--------+--------+--------+
 y = 2   | k = -2 | k = -1 | k = 0  | k = 1  |
         +--------+--------+--------+--------+
 y = 3   | k = -3 | k = -2 | k = -1 | k = 0  |
         +--------+--------+--------+--------+
```

### D-Paths

A **D-path** is a path from (0,0) that uses exactly D non-diagonal moves
(edits). The crucial observation:

> **With exactly D edits, only diagonals k ∈ {−D, −D+2, …, D−2, D} are
> reachable.**

Why? Each edit changes k by exactly ±1 (horizontal: k+1, vertical: k−1).
Starting at k = 0, after D moves the parity of k always equals the parity of
D. So for D = 0, only k = 0 is reachable; for D = 1, k ∈ {−1, +1}; for D = 2,
k ∈ {−2, 0, +2}; and so on.

```
D = 0:                                   k = 0
                                          / \
                                         /   \
                                        /     \
                                       /       \
                                      /         \
                                     /           \
                                    /             \
                                   /               \
                                  /                 \
D = 1:                        k = -1              k = +1
                                / \                 / \
                               /   \               /   \
                              /     \             /     \
                             /       \           /       \
                            /         \         /         \
                           /           \       /           \
                          /             \     /             \
                         /               \   /               \
                        /                 \ /                 \
D = 2:              k = -2               k = 0              k = +2
                      / \                 / \                 / \
                     /   \               /   \               /   \
                    /     \             /     \             /     \
                   /       \           /       \           /       \
                  /         \         /         \         /         \
                 /           \       /           \       /           \
                /             \     /             \     /             \
               /               \   /               \   /               \
              /                 \ /                 \ /                 \
D = 3:    k = -3              k = -1              k = +1              k = +3

  Reachable diagonals fan out by 2 at each step.
```

---

## 5. The Forward Pass — Finding the Shortest Edit Distance D

The forward pass is the heart of the algorithm. It answers: "What is the
minimum D?"

### The V Array

The algorithm maintains an array `V` indexed by diagonal k:

> **V[k]** = the furthest x-coordinate reached on diagonal k.

Since y = x − k, knowing x is enough to know the exact position.

### Algorithm Step-by-Step

```
for d = 0, 1, 2, …:
    save a snapshot of V             <- needed for backtracking later
    for k = −d, −d+2, …, d−2, d:    <- only same-parity diagonals
        ① Decide direction:
           • If coming from k+1 (vertical / insert): x = V[k+1]
           • If coming from k−1 (horizontal / delete): x = V[k−1] + 1
           • Pick whichever gives the LARGER x (= more progress)
        ② Extend the snake:
           y = x − k
           while old[x] == new[y]:
               x++; y++
        ③ Record: V[k] = x
        ④ Check: if x == N and y == M -> DONE! D = d
```

### Why Pick the Larger x?

A larger x means we have consumed more of the old file. Combined with the
diagonal constraint (y = x − k), a larger x also means a larger y — we've made
more overall progress toward (N, M). The algorithm always takes the **greedy**
choice at each step, which Myers proved leads to an optimal solution.

### Visualization of One Step (d = 1)

Suppose old = `[A, B, C]`, new = `[A, C, B]`.

After d = 0, V[0] = 1 (matched A, slid to x = 1, y = 1).

For d = 1:

- k = −1: can only come from k = 0 (down/insert). x = V[0] = 1, y = 1−(−1) = 2.
  Check: old[1] = B, new[2] = B -> match! Slide to x = 2, y = 3.
  V[−1] = 2.
- k = +1: can only come from k = 0 (right/delete). x = V[0] + 1 = 2, y = 2−1 = 1.
  Check: old[2] = C, new[1] = C -> match! Slide to x = 3, y = 2.
  V[+1] = 3.

Neither (3, 3) is reached yet, so continue to d = 2…

---

## 6. Backtracking — Reconstructing the Edit Script

Once we know D, we need to recover the actual sequence of edits. This is where
the **snapshots** come in.

### Why Snapshots?

During the forward pass, each step d overwrites V. To reconstruct the path, we
need to know what V looked like _before_ each step. The snapshot `trace[d]`
stores the V values at the beginning of step d.

### Backtracking Procedure

Starting from the final position (N, M):

```
for d = D, D−1, …, 1:
    k = x − y
    Look up trace[d] to find V values from step d's start:
        Was the move vertical (insert)?   -> came from (V[k+1], V[k+1]−(k+1))
        Was the move horizontal (delete)?  -> came from (V[k−1]+1, V[k−1]+1−(k−1))
    Output the snake (context lines) from (xStart, yStart) to (x, y).
    Output the single edit (insert or delete).
    Move current position to the previous point.

Any remaining prefix (d = 0 snake) is all context lines.
```

### How to Determine Direction

The same rule as the forward pass: if `k` == `−d` or `V[k−1]` < `V[k+1]`, the move
was vertical (insert); otherwise, it was horizontal (delete).

### Assembling the Result

The backtracking produces edits in **reverse** order (from end to start), so the
final step reverses the list. Each edit is tagged:

| Tag           | Meaning                             |
| ------------- | ----------------------------------- |
| `' '` (space) | Context — line exists in both files |
| `'-'`         | Removed — line exists only in old   |
| `'+'`         | Added — line exists only in new     |

---

## 7. Worked Example

Let's trace through the complete algorithm with a small example.

### Input

```
old = ["A", "B", "C", "D"]    (N = 4)
new = ["A", "C", "D", "E"]    (M = 4)
```

Expected diff:

```
  A        (context)
- B        (delete)
  C        (context)
  D        (context)
+ E        (insert)
```

Edit distance D = 2 (one deletion + one insertion).

### Edit Graph

```
              old[0]=A       old[1]=B       old[2]=C       old[3]=D
        (0,0)--------->(1,0)--------->(2,0)--------->(3,0)--------->(4,0)
          | \            |              |              |              |
          |  \           |              |              |              |
          |   \          |              |              |              |
          |    \         |              |              |              |
          |     \        |              |              |              |
          |      \       |              |              |              |
new[0]=A  |       \      |              |              |              |
          |        \     |              |              |              |
          |         \    |              |              |              |
          |          \   |              |              |              |
          |           \  |              |              |              |
          |            \ |              |              |              |
          v             \v              v              v              v
        (0,1)--------->(1,1)--------->(2,1)--------->(3,1)--------->(4,1)
          |              |              | \            |              |
          |              |              |  \           |              |
          |              |              |   \          |              |
          |              |              |    \         |              |
          |              |              |     \        |              |
          |              |              |      \       |              |
new[1]=C  |              |              |       \      |              |
          |              |              |        \     |              |
          |              |              |         \    |              |
          |              |              |          \   |              |
          |              |              |           \  |              |
          |              |              |            \ |              |
          v              v              v             \v              v
        (0,2)--------->(1,2)--------->(2,2)--------->(3,2)--------->(4,2)
          |              |              |              | \            |
          |              |              |              |  \           |
          |              |              |              |   \          |
          |              |              |              |    \         |
          |              |              |              |     \        |
          |              |              |              |      \       |
new[2]=D  |              |              |              |       \      |
          |              |              |              |        \     |
          |              |              |              |         \    |
          |              |              |              |          \   |
          |              |              |              |           \  |
          |              |              |              |            \ |
          v              v              v              v             \v
        (0,3)--------->(1,3)--------->(2,3)--------->(3,3)--------->(4,3)
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
new[3]=E  |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          v              v              v              v              v
        (0,4)--------->(1,4)--------->(2,4)--------->(3,4)--------->(4,4)  <- GOAL
```

Diagonal matches (\):

- (0,0)->(1,1): old[0]=A == new[0]=A
- (2,1)->(3,2): old[2]=C == new[1]=C
- (3,2)->(4,3): old[3]=D == new[2]=D

### Forward Pass Trace

**d = 0** — no edits, just slide along the initial snake:

```
  k=0: start at (0,0). old[0]=A == new[0]=A -> slide to (1,1).
  V[0] = 1
  Not at (4,4) yet.
```

**d = 1** — one edit:

```
  k=−1: down from k=0 -> x = V[0] = 1, y = 1−(−1) = 2.
         old[1]=B, new[2]=D -> no match.  V[−1] = 1.

  k=+1: right from k=0 -> x = V[0]+1 = 2, y = 2−1 = 1.
         old[2]=C == new[1]=C -> slide! (3,2).
         old[3]=D == new[2]=D -> slide! (4,3).
         V[+1] = 4.
  Not at (4,4) yet.
```

**d = 2** — two edits:

```
  k=−2: down from k=−1 -> x = V[−1] = 1, y = 1−(−2) = 3.
         old[1]=B, new[3]=E -> no match.  V[−2] = 1.

  k=0:  choose max(down from k=+1, right from k=−1):
         down from k=+1 -> x = V[+1] = 4.  right from k=−1 -> x = V[−1]+1 = 2.
         Pick down (x=4). y = 4−0 = 4.
         x=4 == N and y=4 == M -> FOUND!  D = 2. ✓

  (k=+2 not needed)
```

### Backtracking

Start at (4, 4), d = 2.

**d = 2, k = 0**: Look at trace[2]. Was it down (from k=+1)?
V[+1] = 4, so came from (4, 3) via down (insert).
Snake: (4, 3) -> (4, 4) — no snake (length 0).
Edit: INSERT new[3] = "E".
Move to (4, 3).

**d = 1, k = +1**: Look at trace[1]. Was it right (from k=0)?
V[0] = 1, so came from (2, 1) via right (delete). x = V[0]+1 = 2, y = 1.
Snake: (2, 1) -> (4, 3) — old[2]=C == new[1]=C, old[3]=D == new[2]=D.
Edit: DELETE old[1] = "B".
Move to (1, 1).

**d = 0 prefix**: Snake from (0, 0) to (1, 1) — old[0]=A == new[0]=A.

### Final Result (after reversing)

```
  A          <- context (d=0 snake)
- B          <- delete  (d=1 edit)
  C          <- context (d=1 snake)
  D          <- context (d=1 snake)
+ E          <- insert  (d=2 edit)
```

---

## 8. Complexity Analysis

### Time Complexity: O(D² + N + M)

The cost breaks down into two parts:

1. **Diagonal loop iterations**: At step d, the inner loop runs over 2d + 1
   diagonals. Summing from d = 0 to D:

   ```
   Σ(2d + 1) for d = 0…D  =  (D + 1)²  ≈  D²
   ```

2. **Snake extensions**: Each cell (x, y) in the edit graph is visited _at most
   once_ across all snake extensions. The grid has N × M cells, but snakes can
   only advance forward, so the total number of string comparisons is bounded by
   **N + M**.

Combined: **O(D² + N + M)**.

### Space Complexity: O(D²)

The V array itself is O(D). But we save a snapshot at each of the D + 1 steps.
Snapshot at step d has size 2d + 3. Total:

```
Σ(2d + 3) for d = 0…D  ≈  D² + 3D  ≈  D²  integers
```

### Why This Is Fast for Real-World Diffs

| Scenario                     | D     | Time                    | Trace Space            |
| ---------------------------- | ----- | ----------------------- | ---------------------- |
| 1M-line IL files, 20 changes | 20    | ~40M ops (< 0.1 s)      | ~400 ints (negligible) |
| Large text, 1000 changes     | 1,000 | ~1M iterations + O(N+M) | ~1M ints (~4 MB)       |
| Completely different files   | N + M | Degrades to O((N+M)²)   | Up to N+M snapshots    |

The algorithm is **output-sensitive** — its speed depends on how _different_ the
files are, not how _big_ they are.

---

## 9. Implementation in This Project

The implementation lives in
[`TextDiffer.cs`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs).

### Entry Point

```csharp
public static IReadOnlyList<DiffLine> Compute(
    string[] oldLines,
    string[] newLines,
    int contextLines = 3,       // context lines around each change
    int maxOutputLines = 10000,  // cap on output size
    int maxEditDistance = 4000)  // cap on edit distance D
```

### Internal Structure

```
Compute()
  |
  +- MyersDiff(old, new, maxEditDistance)    <- Forward pass: finds D
  |    |
  |    +- BacktrackMyers(...)                <- Backtracking: reconstructs edits
  |
  +- BuildHunks(old, new, edits, ...)       <- Formats into unified diff hunks
```

### Key Implementation Details

1. **Offset trick**: Since k ranges from −maxD to +maxD, the array index is
   shifted: `V[k + offset]` where `offset = maxD`.

2. **Snapshot optimization**: Only the needed range `[offset − d − 1,
offset + d + 1]` is copied at each step, not the entire V array.

3. **Early termination**: If D exceeds `maxEditDistance`, `MyersDiff` returns
   `null` and `Compute` emits a single `Truncated` line explaining the skip.

4. **Output budget**: `BuildHunks` counts output lines and stops when
   `maxOutputLines` is reached, appending a `Truncated` marker.

### DiffLine Record

Each output line is represented as:

```csharp
public readonly record struct DiffLine(
    char Kind,       // ' ' context, '-' removed, '+' added, '@' hunk header, '~' truncated
    string Text,     // the line content
    int OldLineNo,   // 1-based line number in old (0 if N/A)
    int NewLineNo    // 1-based line number in new (0 if N/A)
);
```

---

## 10. Trade-offs and Practical Limits

### Configuration Parameters

| Parameter                                                  | Default | Purpose                                       |
| ---------------------------------------------------------- | ------- | --------------------------------------------- |
| [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs) | 4000    | Maximum D before aborting the diff            |
| [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs)  | 10000   | Maximum lines in the diff output              |
| [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs)    | 10000   | Maximum total diff lines (post-compute check) |

### When D Exceeds the Limit

For D = 4000, the trace stores approximately:

```
Σ(2d + 3) for d = 0…4000  ≈  16 million integers  ≈  64 MB
```

This is the practical upper bound. Beyond this, the diff is skipped and a
notice is shown to the user.

### Comparison with Other Algorithms

| Algorithm            | Time            | Space    | Best For                 |
| -------------------- | --------------- | -------- | ------------------------ |
| Classic LCS (DP)     | O(N × M)        | O(N × M) | Small files only         |
| Myers (basic)        | O(D² + N + M)   | O(D²)    | Files with few changes ✓ |
| Myers (linear space) | O(D² + N + M)   | O(D)     | Memory-constrained       |
| Patience diff        | O(N log N + D²) | O(N)     | Code with unique lines   |
| Histogram diff       | ~O(N + M + D²)  | O(N + M) | Git's default for code   |

This project uses the **basic Myers** variant because the snapshot trace is
needed for clean backtracking, and D is capped at 4000, keeping memory bounded.

---

## 11. Further Reading

- **Original paper**: [E. W. Myers, "An O(ND) Difference Algorithm and Its Variations,1986."](http://www.xmailserver.org/diff2.pdf)
- **Blog post**: James Coglan, ["The Myers diff algorithm"](https://blog.jcoglan.com/2017/02/12/the-myers-diff-algorithm-part-1/) —
  an excellent series with step-by-step visualizations.
- **Git source**: Git uses a variant of Myers diff in [`xdiff/xdiffi.c`](https://github.com/git/git/blob/master/xdiff/xdiffi.c).

---

---

# Myers Diff アルゴリズム詳解

> **対象読者** — diff の仕組みを初めて学ぶ開発者から、実装の詳細を確認したい
> 上級者まで。前半（[English version](#myers-diff-algorithm--a-comprehensive-guide)）は英語、
> 後半（本セクション以降）は日本語で同じ内容を記述しています。
> 各セクションは独立して読めます。
>
> **実装ファイル** —
> [`TextDiffer.cs`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs)
>
> **参照論文** — ["E. W. Myers,
> An O(ND) Difference Algorithm and Its Variations",
> _Algorithmica_ **1**(2), 1986.](http://www.xmailserver.org/diff2.pdf)

---

## 目次

- [1. diff が解く問題とは](#1-diff-が解く問題とは)
- [2. なぜ古典的 LCS では駄目なのか](#2-なぜ古典的-lcs-では駄目なのか)
- [3. 編集グラフ — すべての編集操作の地図](#3-編集グラフ--すべての編集操作の地図)
- [4. 対角線と D パス](#4-対角線と-d-パス)
- [5. 前向きパス — 最小編集距離 D の発見](#5-前向きパス--最小編集距離-d-の発見)
- [6. バックトラック — 編集スクリプトの復元](#6-バックトラック--編集スクリプトの復元)
- [7. 具体例で追う全手順](#7-具体例で追う全手順)
- [8. 計算量の分析](#8-計算量の分析)
- [9. 本プロジェクトでの実装](#9-本プロジェクトでの実装)
- [10. トレードオフと実用上の制限](#10-トレードオフと実用上の制限)
- [11. 参考資料](#11-参考資料)

---

## 1. diff が解く問題とは

テキストファイルの **旧バージョン (old)** と **新バージョン (new)** があるとします。
diff ツールは次の問いに答えます。

> **「old を new に変換するために必要な、挿入と削除の最小集合は何か？」**

例:

```
Old（3 行）          New（3 行）
-----------          -----------
A                    A
B                    C
C                    B
```

有効な編集スクリプトの一つは「2 行目の B を削除し、残った B の前に C を挿入する」
ですが、すべての行を削除して全行を再挿入する方法も（長くはなるものの）有効です。
すべての候補のうち**最短**のもの — 挿入・削除の回数が最も少ないもの — を求めます。
その編集回数を**編集距離 D** と呼びます。

### たとえ話 — カーナビ

これはカーナビが 2 つの都市間の最短ルートを探すのと似ています。A 市から B 市に
行く方法はたくさんありますが、曲がる回数（＝編集回数）が最も少ないルートが欲しい
のです。Myers アルゴリズムはカーナビ — **編集グラフ**という特殊な「地図」上で
最短ルートを効率よく見つけるエンジンです。

---

## 2. なぜ古典的 LCS では駄目なのか

最短差分を求める教科書的な方法は、**最長共通部分列 (LCS: Longest Common Subsequence)** を動的計画法で解く
ことです。old の行数 N、new の行数 M として N × M のテーブルを作り、全セルを
埋めます。

| 指標 | 古典的 LCS | Myers Diff    |
| ---- | ---------- | ------------- |
| 時間 | O(N × M)   | O(D² + N + M) |
| 空間 | O(N × M)   | O(D²)         |

20 行しか違わない 100 万行の IL ファイル 2 本を比較する場合:

- **LCS**: 1,000,000 × 1,000,000 = **1 兆**セル — 実行不可能。
- **Myers**: D = 20 -> 対角線反復 約 400 回 + スネーク比較 200 万回 ->
  **0.1 秒未満**で完了。

核心的な洞察は、**D は通常 N や M に比べて非常に小さい**ということです。
現実のファイルは数百万行あっても差分はわずか数行。Myers diff はこの性質を活かし、
コストを*ファイルサイズ*ではなく*変更量*に比例させます。

---

## 3. 編集グラフ — すべての編集操作の地図

編集グラフは Myers diff の概念的な基盤です。「最短の diff を見つける」問題を
「グラフ上の最短パスを見つける」問題に変換します。

### 軸

- **x 軸**（水平）: old ファイルの位置 0 ～ N。
- **y 軸**（垂直）: new ファイルの位置 0 ～ M。

各点 (x, y) は「old から x 行、new から y 行を消費した」状態を表します。

### 3 種類の移動

```
               old[0]         old[1]         old[2]
                 A              B              C
          +--------------+--------------+--------------+
          |              |              |              |
          |              |              |              |
          |              |              |              |
 new[0] A |    (0,0)-----+--->(1,0)-----+--->(2,0)-----+--->(3,0)
          |      |\      |      |       |      |       |
          |      | \     |      |       |      |       |
          |      |  \    |      |       |      |       |
          |      |   \   |      |       |      |       |
          |      |    \  |      |       |      |       |
          |      |     \ |      |       |      |       |
          |      v      \|      v       |      v       |
 new[1] C |    (0,1)-----+--->(1,1)-----+--->(2,1)-----+--->(3,1)
          |      |       |      |       |      |\      |
          |      |       |      |       |      | \     |
          |      |       |      |       |      |  \    |
          |      |       |      |       |      |   \   |
          |      |       |      |       |      |    \  |
          |      |       |      |       |      |     \ |
          |      v       |      v       |      v      \|
 new[2] B |    (0,2)-----+--->(1,2)-----+--->(2,2)-----+--->(3,2)
          |      |       |      |\      |      |       |
          |      |       |      | \     |      |       |
          |      |       |      |  \    |      |       |
          |      |       |      |   \   |      |       |
          |      |       |      |    \  |      |       |
          |      |       |      |     \ |      |       |
          |      v       |      v      \|      v       |
          |    (0,3)-----+--->(1,3)-----+--->(2,3)-----+--->(3,3)
          |              |              |              |
          |              |              |              |
          |              |              |              |
          +--------------+--------------+--------------+

凡例:
  --->  横移動 (x+1, y)   = old[x] を削除
  v     縦移動 (x, y+1)   = new[y] を挿入
  \     斜め移動 (x+1, y+1) = 一致 (old[x] == new[y]) -- コスト 0！
```

| 移動     | 方向                 | 意味                        | コスト          |
| -------- | -------------------- | --------------------------- | --------------- |
| 横移動   | (x, y) -> (x+1, y)   | `old[x]` を削除             | 1 回の編集      |
| 縦移動   | (x, y) -> (x, y+1)   | `new[y]` を挿入             | 1 回の編集      |
| 斜め移動 | (x, y) -> (x+1, y+1) | 一致 — `old[x]` == `new[y]` | **0**（無料！） |

**目標**: **(0, 0)** から **(N, M)** へ、横移動 + 縦移動の回数が**最小**の
パスを見つけること。斜め移動はコスト 0 — 両ファイルに共通する行を表します。

### スネーク（Snake）

**スネーク**とは、連続する斜め移動の最長列のことです。`old[x]` == `new[y]` となる
地点に着いたら、一致が続く限り斜めに「滑る」ように進みます。スネークは無料なので、
長ければ長いほど有利です。

```
(1,1) からのスネーク:

  (1,1) -\- (2,2) -\- (3,3) -\- (4,4)
          ^          ^          ^
        一致       一致       一致

  一致する 3 行を無料で消費！
```

---

## 4. 対角線と D パス

### 対角線番号 k

編集グラフ上の各点 (x, y) は**対角線**上にあります:

```
k = x − y
```

- k = 0: 主対角線（左上から右下方向）。
- k > 0: old の方を多く消費（正味の削除が多い）。
- k < 0: new の方を多く消費（正味の挿入が多い）。

```
           x = 0    x = 1    x = 2    x = 3
         +--------+--------+--------+--------+
 y = 0   | k = 0  | k = 1  | k = 2  | k = 3  |
         +--------+--------+--------+--------+
 y = 1   | k = -1 | k = 0  | k = 1  | k = 2  |
         +--------+--------+--------+--------+
 y = 2   | k = -2 | k = -1 | k = 0  | k = 1  |
         +--------+--------+--------+--------+
 y = 3   | k = -3 | k = -2 | k = -1 | k = 0  |
         +--------+--------+--------+--------+
```

### D パス

**D パス**とは、(0,0) から出発し、ちょうど D 回の非斜め移動（編集）を含む
パスです。重要な観察:

> **D 回の編集では、対角線 k ∈ {−D, −D+2, …, D−2, D} にのみ到達可能。**

なぜか？ 各編集は k をちょうど ±1 変化させます（横移動: k+1、縦移動: k−1）。
k = 0 からスタートし、D 回移動後の k のパリティ（偶奇）は常に D のパリティと
一致します。つまり D = 0 なら k = 0 のみ、D = 1 なら k ∈ {−1, +1}、
D = 2 なら k ∈ {−2, 0, +2}…と展開します。

```
D = 0:                                   k = 0
                                          / \
                                         /   \
                                        /     \
                                       /       \
                                      /         \
                                     /           \
                                    /             \
                                   /               \
                                  /                 \
D = 1:                        k = -1              k = +1
                                / \                 / \
                               /   \               /   \
                              /     \             /     \
                             /       \           /       \
                            /         \         /         \
                           /           \       /           \
                          /             \     /             \
                         /               \   /               \
                        /                 \ /                 \
D = 2:              k = -2               k = 0              k = +2
                      / \                 / \                 / \
                     /   \               /   \               /   \
                    /     \             /     \             /     \
                   /       \           /       \           /       \
                  /         \         /         \         /         \
                 /           \       /           \       /           \
                /             \     /             \     /             \
               /               \   /               \   /               \
              /                 \ /                 \ /                 \
D = 3:    k = -3              k = -1              k = +1              k = +3

  到達可能な対角線はステップごとに 2 ずつ広がる。
```

---

## 5. 前向きパス — 最小編集距離 D の発見

前向きパスはアルゴリズムの核心です。「最小の D はいくつか？」に答えます。

### V 配列

アルゴリズムは対角線 k でインデックスされた配列 `V` を保持します:

> **V[k]** = 対角線 k 上で到達できた最大の x 座標。

y = x − k なので、x が分かれば正確な位置が分かります。

### アルゴリズムのステップ

```
d = 0, 1, 2, … について:
    V のスナップショットを保存        <- 後のバックトラック用
    k = −d, −d+2, …, d−2, d について:  <- 同パリティの対角線のみ
        ① 方向を決定:
           • k+1 から来る場合（縦移動 / 挿入）: x = V[k+1]
           • k−1 から来る場合（横移動 / 削除）: x = V[k−1] + 1
           • x が大きくなる方を選ぶ（＝進行度が高い）
        ② スネークを延長:
           y = x − k
           old[x] == new[y] である限り x++; y++
        ③ 記録: V[k] = x
        ④ 判定: x == N かつ y == M -> 完了！ D = d
```

### なぜ x が大きい方を選ぶのか？

x が大きいほど old ファイルをより多く消費しています。対角線の制約 (y = x − k)
により、x が大きければ y も大きい — つまり (N, M) への全体的な進行度が高いのです。
アルゴリズムは各ステップで**貪欲**な選択を行います。Myers はこの貪欲選択が
最適解を導くことを証明しました。

### 1 ステップの可視化（d = 1）

old = `[A, B, C]`、new = `[A, C, B]` の場合。

d = 0 の後、V[0] = 1（A が一致し、x = 1, y = 1 まで滑った）。

d = 1:

- k = −1: k = 0 からの縦移動（挿入）のみ。x = V[0] = 1, y = 1−(−1) = 2。
  判定: old[1] = B, new[2] = B -> 一致！ x = 2, y = 3 まで滑る。V[−1] = 2。
- k = +1: k = 0 からの横移動（削除）のみ。x = V[0] + 1 = 2, y = 2−1 = 1。
  判定: old[2] = C, new[1] = C -> 一致！ x = 3, y = 2 まで滑る。V[+1] = 3。

(3, 3) にはまだ到達していないので d = 2 へ…

---

## 6. バックトラック — 編集スクリプトの復元

D が判明したら、実際の編集列を復元する必要があります。ここで**スナップショット**
が活きてきます。

### なぜスナップショットが必要か？

前向きパスでは各ステップ d が V を上書きします。パスを復元するには、各ステップの
**開始時点**での V が必要です。`trace[d]` がステップ d 開始時の V 値を保存して
います。

### バックトラックの手順

最終位置 (N, M) から出発:

```
d = D, D−1, …, 1 について:
    k = x − y
    trace[d] を参照して、ステップ d 開始時の V 値を取得:
        縦移動（挿入）だったか？ -> (V[k+1], V[k+1]−(k+1)) から来た
        横移動（削除）だったか？ -> (V[k−1]+1, V[k−1]+1−(k−1)) から来た
    スネーク部分（コンテキスト行）を (xStart, yStart) -> (x, y) として出力。
    1 回の編集（挿入または削除）を出力。
    現在位置を前のポイントに移動。

残りのプレフィックス（d = 0 のスネーク）はすべてコンテキスト行。
```

### 方向の判定方法

前向きパスと同じルール: `k` == `−d` または `V[k−1]` < `V[k+1]` なら縦移動（挿入）、
それ以外なら横移動（削除）。

### 結果の組み立て

バックトラックは編集を**逆順**（末尾->先頭）で生成するため、最後にリストを
反転します。各編集にはタグが付きます:

| タグ          | 意味                                  |
| ------------- | ------------------------------------- |
| `' '`（空白） | コンテキスト — 両ファイルに存在する行 |
| `'-'`         | 削除 — old にのみ存在する行           |
| `'+'`         | 追加 — new にのみ存在する行           |

---

## 7. 具体例で追う全手順

小さな例でアルゴリズムの全体を追いましょう。

### 入力

```
old = ["A", "B", "C", "D"]    (N = 4)
new = ["A", "C", "D", "E"]    (M = 4)
```

期待される diff:

```
  A        （コンテキスト）
- B        （削除）
  C        （コンテキスト）
  D        （コンテキスト）
+ E        （挿入）
```

編集距離 D = 2（削除 1 回 + 挿入 1 回）。

### 編集グラフ

```
              old[0]=A       old[1]=B       old[2]=C       old[3]=D
        (0,0)--------->(1,0)--------->(2,0)--------->(3,0)--------->(4,0)
          | \            |              |              |              |
          |  \           |              |              |              |
          |   \          |              |              |              |
          |    \         |              |              |              |
          |     \        |              |              |              |
          |      \       |              |              |              |
new[0]=A  |       \      |              |              |              |
          |        \     |              |              |              |
          |         \    |              |              |              |
          |          \   |              |              |              |
          |           \  |              |              |              |
          |            \ |              |              |              |
          v             \v              v              v              v
        (0,1)--------->(1,1)--------->(2,1)--------->(3,1)--------->(4,1)
          |              |              | \            |              |
          |              |              |  \           |              |
          |              |              |   \          |              |
          |              |              |    \         |              |
          |              |              |     \        |              |
          |              |              |      \       |              |
new[1]=C  |              |              |       \      |              |
          |              |              |        \     |              |
          |              |              |         \    |              |
          |              |              |          \   |              |
          |              |              |           \  |              |
          |              |              |            \ |              |
          v              v              v             \v              v
        (0,2)--------->(1,2)--------->(2,2)--------->(3,2)--------->(4,2)
          |              |              |              | \            |
          |              |              |              |  \           |
          |              |              |              |   \          |
          |              |              |              |    \         |
          |              |              |              |     \        |
          |              |              |              |      \       |
new[2]=D  |              |              |              |       \      |
          |              |              |              |        \     |
          |              |              |              |         \    |
          |              |              |              |          \   |
          |              |              |              |           \  |
          |              |              |              |            \ |
          v              v              v              v             \v
        (0,3)--------->(1,3)--------->(2,3)--------->(3,3)--------->(4,3)
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
new[3]=E  |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          |              |              |              |              |
          v              v              v              v              v
        (0,4)--------->(1,4)--------->(2,4)--------->(3,4)--------->(4,4)  <- GOAL
```

斜め移動（一致）の位置:

- (0,0)->(1,1): old[0]=A == new[0]=A
- (2,1)->(3,2): old[2]=C == new[1]=C
- (3,2)->(4,3): old[3]=D == new[2]=D

### 前向きパスのトレース

**d = 0** — 編集なし、初期スネークのみ:

```
  k=0: (0,0) からスタート。old[0]=A == new[0]=A -> (1,1) まで滑る。
  V[0] = 1
  (4,4) にはまだ到達していない。
```

**d = 1** — 編集 1 回:

```
  k=−1: k=0 から縦移動 -> x = V[0] = 1, y = 1−(−1) = 2。
         old[1]=B, new[2]=D -> 不一致。V[−1] = 1。

  k=+1: k=0 から横移動 -> x = V[0]+1 = 2, y = 2−1 = 1。
         old[2]=C == new[1]=C -> 一致！ (3,2) まで滑る。
         old[3]=D == new[2]=D -> 一致！ (4,3) まで滑る。
         V[+1] = 4。
  (4,4) にはまだ到達していない。
```

**d = 2** — 編集 2 回:

```
  k=−2: k=−1 から縦移動 -> x = V[−1] = 1, y = 1−(−2) = 3。
         old[1]=B, new[3]=E -> 不一致。V[−2] = 1。

  k=0:  max（k=+1 からの縦移動, k=−1 からの横移動）を選択:
         k=+1 から縦移動 -> x = V[+1] = 4。
         k=−1 から横移動 -> x = V[−1]+1 = 2。
         縦移動を選択（x=4 の方が大きい）。y = 4−0 = 4。
         x=4 == N かつ y=4 == M -> 発見！ D = 2。 ✓

  （k=+2 は不要）
```

### バックトラック

(4, 4) から開始、d = 2。

**d = 2, k = 0**: trace[2] を参照。縦移動（k=+1 から）だったか？
V[+1] = 4 なので、(4, 3) から縦移動（挿入）で来た。
スネーク: (4, 3) -> (4, 4) — スネークなし（長さ 0）。
編集: INSERT new[3] = "E"。
(4, 3) に移動。

**d = 1, k = +1**: trace[1] を参照。横移動（k=0 から）だったか？
V[0] = 1 なので、(2, 1) から横移動（削除）で来た。x = V[0]+1 = 2, y = 1。
スネーク: (2, 1) -> (4, 3) — old[2]=C == new[1]=C, old[3]=D == new[2]=D。
編集: DELETE old[1] = "B"。
(1, 1) に移動。

**d = 0 のプレフィックス**: (0, 0) -> (1, 1) のスネーク — old[0]=A == new[0]=A。

### 最終結果（反転後）

```
  A          <- コンテキスト（d=0 のスネーク）
- B          <- 削除（d=1 の編集）
  C          <- コンテキスト（d=1 のスネーク）
  D          <- コンテキスト（d=1 のスネーク）
+ E          <- 挿入（d=2 の編集）
```

---

## 8. 計算量の分析

### 時間計算量: O(D² + N + M)

コストは 2 つの部分に分かれます:

1. **対角線ループの反復回数**: ステップ d では内側ループが 2d + 1 回の対角線を
   走査します。d = 0 から D まで合計すると:

   ```
   Σ(2d + 1)（d = 0…D）=（D + 1）² ≈ D²
   ```

2. **スネーク延長**: 編集グラフの各セル (x, y) は全スネーク延長を通じて**最大
   1 回**しか訪問されません。スネークは前方にしか進めないため、文字列比較の
   総回数は **N + M** 以下です。

合計: **O(D² + N + M)**。

### 空間計算量: O(D²)

V 配列自体は O(D) です。しかし D + 1 回の各ステップでスナップショットを保存
します。ステップ d のスナップショットのサイズは 2d + 3。合計:

```
Σ(2d + 3)（d = 0…D）≈ D² + 3D ≈ D² 個の整数
```

### 実世界の差分ではなぜ高速か

| シナリオ                     | D     | 時間                     | トレース領域             |
| ---------------------------- | ----- | ------------------------ | ------------------------ |
| 100 万行 IL、差分 20 行      | 20    | ~4000 万演算（< 0.1 秒） | ~400 整数（無視できる）  |
| 大きなテキスト、差分 1000 行 | 1,000 | ~100 万反復 + O(N+M)     | ~100 万整数（~4 MB）     |
| 完全に異なるファイル         | N + M | O((N+M)²) に退化         | N+M 個のスナップショット |

アルゴリズムは**出力感応型**です — 速度はファイルの*大きさ*ではなく、ファイルが
どれだけ*異なるか*に依存します。

---

## 9. 本プロジェクトでの実装

実装は
[`TextDiffer.cs`](../FolderDiffIL4DotNet.Core/Text/TextDiffer.cs)
にあります。

### エントリポイント

```csharp
public static IReadOnlyList<DiffLine> Compute(
    string[] oldLines,
    string[] newLines,
    int contextLines = 3,       // 変更箇所の前後に表示するコンテキスト行数
    int maxOutputLines = 10000,  // 出力サイズの上限
    int maxEditDistance = 4000)  // 編集距離 D の上限
```

### 内部構造

```
Compute()
  |
  +- MyersDiff(old, new, maxEditDistance)    <- 前向きパス: D を発見
  |    |
  |    +- BacktrackMyers(...)                <- バックトラック: 編集を復元
  |
  +- BuildHunks(old, new, edits, ...)       <- unified diff ハンクへ整形
```

### 実装上の要点

1. **オフセットのトリック**: k は −maxD ～ +maxD の範囲なので、配列のインデックス
   をシフトします: `V[k + offset]`（`offset = maxD`）。

2. **スナップショットの最適化**: 各ステップで V 全体をコピーするのではなく、必要
   な範囲 `[offset − d − 1, offset + d + 1]` のみをコピーします。

3. **早期打ち切り**: D が `maxEditDistance` を超えた場合、`MyersDiff` は `null`
   を返し、`Compute` はスキップの理由を示す `Truncated` 行を 1 行だけ出力します。

4. **出力バジェット**: `BuildHunks` は出力行数をカウントし、`maxOutputLines` に
   達した時点で `Truncated` マーカーを付加して停止します。

### DiffLine レコード

各出力行は以下のレコードで表現されます:

```csharp
public readonly record struct DiffLine(
    char Kind,       // ' ' コンテキスト, '-' 削除, '+' 追加, '@' ハンクヘッダ, '~' 打ち切り
    string Text,     // 行の内容
    int OldLineNo,   // old 内の 1-based 行番号（該当なしは 0）
    int NewLineNo    // new 内の 1-based 行番号（該当なしは 0）
);
```

---

## 10. トレードオフと実用上の制限

### 設定パラメータ

| パラメータ                                                 | 既定値 | 用途                                |
| ---------------------------------------------------------- | ------ | ----------------------------------- |
| [`InlineDiffMaxEditDistance`](../Models/ConfigSettings.cs) | 4000   | 差分を中断する最大 D                |
| [`InlineDiffMaxOutputLines`](../Models/ConfigSettings.cs)  | 10000  | diff 出力の最大行数                 |
| [`InlineDiffMaxDiffLines`](../Models/ConfigSettings.cs)    | 10000  | diff 行の合計上限（計算後チェック） |

### D が上限を超えた場合

D = 4000 の場合、トレースが格納する整数の数は:

```
Σ(2d + 3)（d = 0…4000）≈ 1600 万整数 ≈ 64 MB
```

これが実用的な上限です。この値を超えると diff はスキップされ、ユーザーに通知が
表示されます。

### 他のアルゴリズムとの比較

| アルゴリズム        | 時間            | 空間     | 最適な用途                 |
| ------------------- | --------------- | -------- | -------------------------- |
| 古典的 LCS (DP)     | O(N × M)        | O(N × M) | 小さなファイルのみ         |
| Myers（基本版）     | O(D² + N + M)   | O(D²)    | 差分が少ないファイル ✓     |
| Myers（線形空間版） | O(D² + N + M)   | O(D)     | メモリ制約がある場合       |
| Patience diff       | O(N log N + D²) | O(N)     | ユニークな行が多いコード   |
| Histogram diff      | ~O(N + M + D²)  | O(N + M) | Git のコード差分デフォルト |

本プロジェクトでは**基本版 Myers** を採用しています。スナップショットによる
トレースがバックトラックの簡潔な実装に必要であり、D は 4000 に制限されている
ためメモリは制約内に収まります。

---

## 11. 参考資料

- **原論文**: [E. W. Myers, "An O(ND) Difference Algorithm and Its Variations,1986."](http://www.xmailserver.org/diff2.pdf)
- **ブログ記事**: James Coglan,
  ["The Myers diff algorithm"](https://blog.jcoglan.com/2017/02/12/the-myers-diff-algorithm-part-1/) —
  ステップバイステップの可視化が優れたシリーズ。
- **Git ソースコード**: Git は [`xdiff/xdiffi.c`](https://github.com/git/git/blob/master/xdiff/xdiffi.c) で Myers diff の変種を使用しています。
