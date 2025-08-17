using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Utils;

namespace FolderDiffIL4DotNet.Services.ILOutput
{
    /// <summary>
    /// IL ログ (ILlog.md / ILlog.html) の生成・追記・クローズを担当するサービス。
    /// </summary>
    public sealed class ILlogOutputService
    {
        /// <summary>
        /// IL 出力フォルダの絶対パス
        /// </summary>
        private readonly string _ilOutputFolderAbsolutePath;

        /// <summary>
        /// ロックオブジェクト
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ilOutputFolderAbsolutePath">IL 出力フォルダの絶対パス</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ILlogOutputService(string ilOutputFolderAbsolutePath)
        {
            _ilOutputFolderAbsolutePath = ilOutputFolderAbsolutePath ?? throw new ArgumentNullException(nameof(ilOutputFolderAbsolutePath));
        }

        /// <summary>
        /// ILlog.md 空ファイル作成と ILlog.html ヘッダ初期化を行う。
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Markdown (一時空で出力)
                var illogMdInitPath = Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_MARKDOWN);
                using (var mdInit = new FileStream(illogMdInitPath, FileMode.Create, FileAccess.Write, FileShare.Read)) { }

                // HTML (ヘッダ)
                var illogHtmlInitPath = Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_HTML);
                using var htmlInit = new StreamWriter(new FileStream(illogHtmlInitPath, FileMode.Create, FileAccess.Write, FileShare.Read));
                await htmlInit.WriteLineAsync("<!DOCTYPE html>");
                await htmlInit.WriteLineAsync("<html lang=\"ja\">");
                await htmlInit.WriteLineAsync("<head>");
                await htmlInit.WriteLineAsync("<meta charset=\"utf-8\">");
                await htmlInit.WriteAsync("<meta name=\"viewport\" ");
                await htmlInit.WriteLineAsync("content=\"width=device-width, initial-scale=1\">");
                await htmlInit.WriteAsync("<title>");
                await htmlInit.WriteAsync("IL log");
                await htmlInit.WriteLineAsync("</title>");
                await htmlInit.WriteAsync("<style>");
                await htmlInit.WriteAsync(":root{--bg:#fbfbfd;--fg:#111;--muted:#5c5c61;--card:#fff;--border:#b8b8be;--accent:#0071e3;--ok:#34c759;--ng:#ff3b30} ");
                await htmlInit.WriteAsync("@media (prefers-color-scheme: dark){:root{--bg:#000;--fg:#f5f5f7;--muted:#b9b9be;--card:#111;--border:#3a3a3c;--accent:#0a84ff;--ok:#30d158;--ng:#ff453a}} ");
                await htmlInit.WriteAsync("html,body{height:100%} ");
                await htmlInit.WriteAsync("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,'Noto Sans JP','Noto Sans',sans-serif;line-height:1.55;background:var(--bg);color:var(--fg);margin:0} ");
                await htmlInit.WriteAsync(".container{max-width:1280px;margin:0 auto;padding:24px} h2{margin:0 0 16px;font-size:22px;font-weight:700;letter-spacing:-.01em;display:flex;align-items:center;gap:8px;flex-wrap:wrap} h3{margin:8px 0 6px;font-size:16px;font-weight:600} ");
                await htmlInit.WriteAsync(".meta{color:var(--muted);font-size:12px;display:inline-flex;align-items:center;vertical-align:middle;padding:2px 8px;border:1px solid var(--border);border-radius:9999px;background:transparent} .meta-plain{border:none;border-radius:0;padding:0;background:transparent} ");
                await htmlInit.WriteAsync(".badge{color:#fff;background:var(--accent);border:none;border-radius:9999px;font-size:12px;font-weight:700;padding:3px 10px;display:inline-flex;align-items:center;line-height:1;vertical-align:middle;letter-spacing:.02em} .badge--ok{background:var(--ok)} .badge--ng{background:var(--ng)} ");
                await htmlInit.WriteAsync(".clickable{cursor:pointer;text-decoration:underline} .btn{display:inline-block;margin:6px 0;padding:6px 10px;font-size:12px;border:1px solid var(--border);border-radius:8px;background:var(--card);color:var(--fg);cursor:pointer;transition:background .15s,border-color .15s,box-shadow .15s} ");
                await htmlInit.WriteAsync(".btn:hover{background:rgba(0,0,0,.05);box-shadow:0 1px 3px rgba(0,0,0,.15)} @media (prefers-color-scheme: dark){.btn:hover{background:rgba(255,255,255,.16);border-color:rgba(255,255,255,.28);box-shadow:0 2px 10px rgba(0,0,0,.6),0 0 0 1px rgba(255,255,255,.22)}} ");
                await htmlInit.WriteAsync("pre{background:var(--card);padding:12px;overflow:auto;max-width:100%;box-sizing:border-box;border:1px solid var(--border);border-radius:10px;box-shadow:0 1px 2px rgba(0,0,0,.08)} code{font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,'Liberation Mono','Courier New',monospace;font-size:12px;color:var(--fg)} ");
                await htmlInit.WriteAsync(".ilcode{max-height:36vh;overflow:auto;scrollbar-width:thin;scrollbar-color:rgba(0,0,0,.35) transparent} @media (prefers-color-scheme: dark){.ilcode{scrollbar-color:rgba(255,255,255,.35) transparent}} @media (min-height:800px){.ilcode{max-height:calc(50vh - 120px)}} @media (min-height:1000px){.ilcode{max-height:calc(50vh - 140px)}} ");
                await htmlInit.WriteAsync(".ilcode::-webkit-scrollbar{width:10px;height:10px} .ilcode::-webkit-scrollbar-track{background:transparent} .ilcode::-webkit-scrollbar-thumb{background:rgba(0,0,0,.28);border-radius:8px;border:2px solid transparent;background-clip:padding-box} .ilcode::-webkit-scrollbar-thumb:hover{background:rgba(0,0,0,.4)} @media (prefers-color-scheme: dark){.ilcode::-webkit-scrollbar-thumb{background:rgba(255,255,255,.28)} .ilcode::-webkit-scrollbar-thumb:hover{background:rgba(255,255,255,.4)}} ");
                await htmlInit.WriteAsync(".section{margin-bottom:28px} .cols{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:16px;align-items:start} .col h3{margin:8px 0 6px} @media (max-width: 960px){.cols{grid-template-columns:1fr}} ");
                await htmlInit.WriteAsync(".modal{position:fixed;inset:0;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.45);backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px);z-index:9999} .modal .box{background:var(--card);color:var(--fg);padding:14px 18px;border-radius:12px;box-shadow:0 10px 30px rgba(0,0,0,.25);font-size:14px;border:1px solid var(--border);animation:pop .22s ease-out} @keyframes pop{from{transform:translateY(6px) scale(.98);opacity:0}to{transform:none;opacity:1}} ");
                await htmlInit.WriteLineAsync("</style>");
                await htmlInit.WriteAsync("<script>");
                await htmlInit.WriteAsync("function showModal(msg){try{const m=document.getElementById('modal');const b=document.getElementById('modal_msg');if(!m)return;if(b)b.textContent=msg||'Copied';m.style.display='flex';setTimeout(()=>{m.style.display='none';},500);}catch{}} ");
                await htmlInit.WriteAsync("async function copyFrom(id){try{const el=document.getElementById(id);if(!el)return;const text=el.innerText||el.textContent||'';if(navigator.clipboard&&navigator.clipboard.writeText){await navigator.clipboard.writeText(text);}else{const sel=window.getSelection();const range=document.createRange();range.selectNodeContents(el);sel.removeAllRanges();sel.addRange(range);document.execCommand('copy');sel.removeAllRanges();}showModal('Copied');}catch(e){alert('Copy failed: '+e);}} ");
                await htmlInit.WriteAsync("</script>");
                await htmlInit.WriteLineAsync("</head>");
                await htmlInit.WriteLineAsync("<body>");
                await htmlInit.WriteLineAsync("<div id=\"modal\" class=\"modal\"><div class=\"box\" id=\"modal_msg\">Copied</div></div>");
                await htmlInit.WriteAsync("<div class=\"container\">");
                await htmlInit.WriteAsync("\n");
                await htmlInit.WriteLineAsync("");
            }
            catch (Exception)
            {
                LoggerService.LogMessage("[ERROR] Failed to initialize IL logs.", shouldOutputMessageToConsole: true);
                throw;
            }
        }

        /// <summary>
        /// IL 差分 1 件分を md / html 双方に追記。
        /// </summary>
        public void AppendILDiff(
            string fileRelativePath,
            bool areILsEqual,
            IList<string> il1Lines,
            IList<string> il2Lines,
            string commandString1,
            string commandString2,
            string file1AbsolutePath,
            string file2AbsolutePath)
        {
            try
            {
                lock (_lock)
                {
                    // Markdown
                    using (var logWriter = new StreamWriter(new FileStream(Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_MARKDOWN), FileMode.Append, FileAccess.Write, FileShare.Read)))
                    {
                        logWriter.WriteLine($"## {fileRelativePath} `{(areILsEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch)}`");
                        logWriter.WriteLine($"- {Constants.NOTE_MVID_SKIP}");
                        WriteMarkdownIlBlock(logWriter, file1AbsolutePath, commandString1, il1Lines);
                        WriteMarkdownIlBlock(logWriter, file2AbsolutePath, commandString2, il2Lines);
                    }

                    // HTML
                    using (var htmlWriter = new StreamWriter(new FileStream(Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_HTML), FileMode.Append, FileAccess.Write, FileShare.Read)))
                    {
                        string HtmlEnc(string str) => WebUtility.HtmlEncode(str ?? string.Empty);
                        htmlWriter.Write("<h2>");
                        htmlWriter.Write(HtmlEnc(fileRelativePath));
                        htmlWriter.Write(" <span class=\"");
                        htmlWriter.Write(areILsEqual ? "badge badge--ok" : "badge badge--ng");
                        htmlWriter.Write("\">");
                        htmlWriter.Write(HtmlEnc((areILsEqual ? FileDiffResultLists.DiffDetailResult.ILMatch : FileDiffResultLists.DiffDetailResult.ILMismatch).ToString()));
                        htmlWriter.Write("</span>");
                        htmlWriter.Write("<span class=\"meta meta-plain\">");
                        htmlWriter.Write(HtmlEnc(Constants.NOTE_MVID_SKIP));
                        htmlWriter.Write("</span>");
                        htmlWriter.WriteLine("</h2>");

                        var sectionId = Guid.NewGuid().ToString("N");

                        htmlWriter.WriteLine("<div class=\"section\">");
                        htmlWriter.WriteLine("  <div class=\"cols\">");
                        WriteHtmlIlColumn(htmlWriter, sectionId, "old", file1AbsolutePath, commandString1, il1Lines, HtmlEnc);
                        WriteHtmlIlColumn(htmlWriter, sectionId, "new", file2AbsolutePath, commandString2, il2Lines, HtmlEnc);
                        htmlWriter.WriteLine("  </div>");
                        htmlWriter.WriteLine("</div>");
                    }
                }
            }
            catch (Exception)
            {
                LoggerService.LogMessage("[ERROR] Failed to output IL.", shouldOutputMessageToConsole: true);
                throw;
            }
        }

        /// <summary>
        /// HTML フッタとキャッシュ統計、md への統計追記、md/html の読み取り専用化を行う。
        /// </summary>
        public Task FinalizeAsync(bool enableILCache, int hits, int stores, int evicted, int expired)
        {
            // 1) HTML にフッタと（必要なら）統計を追記
            try
            {
                var illogHtmlPath = Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_HTML);
                if (File.Exists(illogHtmlPath))
                {
                    using var htmlClose = new StreamWriter(new FileStream(illogHtmlPath, FileMode.Append, FileAccess.Write, FileShare.Read));
                    if (enableILCache && (hits > 0 || stores > 0))
                    {
                        htmlClose.WriteLine("<hr><div class=\"meta meta-plain\">IL Cache Stats: hits=" + hits + ", stores=" + stores + ", evicted=" + evicted + ", expired=" + expired + "</div>");
                    }
                    htmlClose.WriteLine("</div>");
                    htmlClose.WriteLine("</body>");
                    htmlClose.WriteLine("</html>");
                }
            }
            catch (Exception ex)
            {
                // HTML のクローズに失敗しても以降（md 追記、属性付与）は続行
                LoggerService.LogMessage($"[WARNING] Failed to finalize {Constants.ILLOG_HTML}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }

            // 2) md に（必要なら）統計を追記
            try
            {
                if (enableILCache && (hits > 0 || stores > 0))
                {
                    using var mdStreamWriter = new StreamWriter(new FileStream(Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_MARKDOWN), FileMode.Append, FileAccess.Write, FileShare.Read));
                    mdStreamWriter.WriteLine();
                    mdStreamWriter.WriteLine("---");
                    mdStreamWriter.WriteLine($"IL Cache Stats: hits={hits}, stores={stores}, evicted={evicted}, expired={expired}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage($"[WARNING] Failed to append cache stats to {Constants.ILLOG_MARKDOWN}: {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }

            // 3) 読み取り専用属性付与
            try
            {
                Utility.TrySetReadOnly(Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_MARKDOWN));
                Utility.TrySetReadOnly(Path.Combine(_ilOutputFolderAbsolutePath, Constants.ILLOG_HTML));
            }
            catch (Exception ex)
            {
                LoggerService.LogMessage($"[WARNING] {ex.Message}", shouldOutputMessageToConsole: true, ex);
            }

            return Task.CompletedTask;
        }

        #region Private Methods
        /// <summary>
        /// IL 差分 1 件分をMarkdownに追記。
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="fileAbsolutePath"></param>
        /// <param name="commandString"></param>
        /// <param name="ilLines"></param>
        private static void WriteMarkdownIlBlock(StreamWriter writer, string fileAbsolutePath, string commandString, IList<string> ilLines)
        {
            writer.WriteLine($"### {fileAbsolutePath}");
            writer.WriteLine($"- updated: {Caching.TimestampCache.GetOrAdd(fileAbsolutePath)}");
            writer.WriteLine($"- {commandString}");
            writer.WriteLine("``` text");
            foreach (var line in ilLines)
            {
                writer.WriteLine(line);
            }
            writer.WriteLine("```");
        }

        /// <summary>
        /// IL 差分 1 件分をHTMLに追記。
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="sectionId"></param>
        /// <param name="kind"></param>
        /// <param name="fileAbsolutePath"></param>
        /// <param name="commandString"></param>
        /// <param name="ilLines"></param>
        /// <param name="htmlEnc"></param>
        private static void WriteHtmlIlColumn(StreamWriter writer, string sectionId, string kind, string fileAbsolutePath, string commandString, IList<string> ilLines, Func<string, string> htmlEnc)
        {
            var idPrefix = kind == "old" ? "old" : "new";
            writer.WriteLine("    <div class=\"col\">");
            writer.Write("      <h3><span id=\"path_" + idPrefix + "_");
            writer.Write(sectionId);
            writer.Write("\" class=\"clickable\" onclick=\"copyFrom('path_" + idPrefix + "_");
            writer.Write(sectionId);
            writer.Write("')\" title=\"Copy path\">");
            writer.Write(htmlEnc(fileAbsolutePath));
            writer.Write("</span><br><span class=\"meta meta-plain\">updated: ");
            writer.Write(htmlEnc(Caching.TimestampCache.GetOrAdd(fileAbsolutePath)));
            writer.Write("</span><br><span class=\"meta meta-plain\">");
            writer.Write(htmlEnc(commandString));
            writer.Write("</span><br><span class=\"meta meta-plain\">");
            writer.Write(htmlEnc(Constants.NOTE_MVID_INCLUDE));
            writer.WriteLine("</span></h3>");
            writer.WriteLine($"      <button class=\"btn\" onclick=\"copyFrom('code_{idPrefix}_{sectionId}')\">Copy（{idPrefix} IL）</button>");
            writer.Write("      <pre class=\"ilcode\"><code id=\"code_" + idPrefix + "_");
            writer.Write(sectionId);
            writer.Write("\">");
            foreach (var line in ilLines)
            {
                writer.WriteLine(htmlEnc(line));
            }
            writer.Write("      </code>");
            writer.WriteLine("</pre>");
            writer.WriteLine("    </div>");
        }
        #endregion
    }
}
