# Security Review (2026-04-07)

## Scope
- Static review of the `FolderDiffIL4DotNet` codebase with focus on path handling, HTML rendering, plugin loading, and configuration surfaces.
- Target files were selected by scanning for process execution, dynamic loading, path composition, and HTML/JS DOM sinks.

## Findings

### 1) High: Untrusted plugin DLL loading can lead to arbitrary code execution
- `PluginLoader` recursively scans configured plugin directories, loads `*.dll` via `LoadFromAssemblyPath`, and instantiates discovered `IPlugin` types.
- There is no built-in integrity/authenticity control (signature/allowlist/hash pinning) before loading.
- Risk: if a writable plugin search path is compromised, attacker-controlled code runs in-process.

**Evidence:** `Runner/PluginLoader.cs`.

**Recommendations:**
1. Add optional strict mode requiring plugin allowlist (plugin ID + SHA-256 hash).
2. Verify strong-name/public key token or Authenticode signature before load.
3. Restrict default plugin directory permissions and document trust boundary clearly.

### 2) Medium: External advisory URLs are emitted as clickable links without scheme allowlist
- Vulnerability badges render `AdvisoryUrl` directly to `<a href="...">` after HTML encoding.
- HTML encoding prevents markup injection, but it does not block dangerous URI schemes (e.g., `javascript:`).
- Risk: clicking a malicious advisory link could execute browser-side script in report context.

**Evidence:** `Services/HtmlReport/HtmlReportGenerateService.DetailRows.cs`.

**Recommendations:**
1. Enforce URI scheme allowlist (`https` and optionally `http`).
2. Render non-allowlisted URLs as plain text (non-clickable).
3. Add unit tests for `javascript:`, `data:`, and malformed URLs.

### 3) Medium: `--output-directory` can direct report writes to arbitrary filesystem locations
- `GetReportsFolderAbsolutePath` uses `Path.GetFullPath(outputDirectory)` when provided and then creates the directory.
- This is valid functionality, but it expands write scope beyond app base path.
- Risk: if the tool is invoked in privileged automation with untrusted args, attacker may redirect output into sensitive paths.

**Evidence:** `Runner/RunPreflightValidator.cs`.

**Recommendations:**
1. For CI/automation mode, add an optional "base output root" restriction.
2. Add denylist checks for system directories when running as admin/root.
3. Log normalized output path at warning level when it escapes app base directory.

## Positive controls observed
- Report label is validated with path traversal defenses (`/`, `\\`, `.`/`..`, control chars, reserved names).
- HTML report output consistently uses HTML encoding helpers for rendered data.

## Risk summary
- Immediate hardening priority: **plugin trust model** (Finding #1).
- Next priority: **URL scheme validation** in HTML report rendering (Finding #2).
- Contextual/operational hardening: output directory guardrails (Finding #3).
