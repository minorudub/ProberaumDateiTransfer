using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Web;

namespace minoru.SimpleDataTransfer;

public sealed class LocalFileServer
{
    private readonly string _root;
    private readonly int _port;
    private IHost? _host;

    public LocalFileServer(string rootFolder, int port)
    {
        _root = Path.GetFullPath(rootFolder);
        _port = port;
        Directory.CreateDirectory(_root);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_host != null) return;

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseKestrel(o =>
                {
                    o.ListenAnyIP(_port);
                    o.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024; // 2 GB
                });

                web.ConfigureServices(services =>
                {
                    services.Configure<FormOptions>(o =>
                    {
                        o.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024;
                    });
                });

                web.Configure(app =>
                {
                    app.UseRouting();

                    app.UseEndpoints(endpoints =>
                    {
                        // BROWSEN
                        endpoints.MapGet("/", async context =>
                        {
                            var rel = NormalizeRel(context.Request.Query["path"].ToString() ?? "");
                            var currentDir = SafeCombineDir(_root, rel);

                            if (currentDir == null || !Directory.Exists(currentDir))
                            {
                                context.Response.StatusCode = 404;
                                return;
                            }

                            context.Response.ContentType = "text/html; charset=utf-8";

                            var enc = HtmlEncoder.Default;

                            // Parent
                            string parentRel = "";
                            if (!string.IsNullOrEmpty(rel))
                            {
                                var parent = rel.TrimEnd('/').Split('/').SkipLast(1);
                                parentRel = string.Join('/', parent);
                            }

                            static string Q(string s) => HttpUtility.UrlEncode(s);

                            // Breadcrumbs
                            var segments = (rel ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
                            var crumbs = segments
                                .Select((seg, idx) => new { seg, idx })
                                .Aggregate(
                                    seed: new System.Collections.Generic.List<(string label, string path)>(),
                                    func: (list, x) =>
                                    {
                                        var prev = list.LastOrDefault().path;
                                        var p = string.IsNullOrEmpty(prev) ? x.seg : $"{prev}/{x.seg}";
                                        list.Add((x.seg, p));
                                        return list;
                                    });

                            static string HumanBytes(long bytes)
                            {
                                string[] units = { "B", "KB", "MB", "GB", "TB" };
                                double v = bytes;
                                int i = 0;
                                while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
                                return (i == 0) ? $"{v:0} {units[i]}" : $"{v:0.##} {units[i]}";
                            }

                            // Header + start of layout
                            await context.Response.WriteAsync($$"""
<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Proberaum Transfer</title>
  <style>
    :root{
      --bg: #0b1220;
      --panel: rgba(255,255,255,.06);
      --border: rgba(255,255,255,.12);
      --text: rgba(255,255,255,.92);
      --muted: rgba(255,255,255,.68);
      --muted2: rgba(255,255,255,.55);
      --accent: #7c5cff;
      --accent2: #00d4ff;
      --shadow: 0 10px 30px rgba(0,0,0,.35);
      --radius: 16px;
      --font: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial;
    }

    *{ box-sizing:border-box; }
    body{
      margin:0;
      font-family: var(--font);
      color: var(--text);
      background:
        radial-gradient(1200px 600px at 10% 0%, rgba(124,92,255,.35), transparent 50%),
        radial-gradient(900px 600px at 90% 10%, rgba(0,212,255,.25), transparent 55%),
        radial-gradient(900px 700px at 50% 110%, rgba(124,92,255,.18), transparent 60%),
        var(--bg);
      min-height:100vh;
    }

    .wrap{ max-width: 980px; margin: 0 auto; padding: 22px 16px 44px; }
    .topbar{
      position: sticky; top:0; z-index:10;
      padding: 14px 0 10px;
      backdrop-filter: blur(10px);
      background: linear-gradient(to bottom, rgba(11,18,32,.75), rgba(11,18,32,.25));
    }

    .header{
      display:flex; align-items:flex-start; justify-content:space-between; gap:12px;
      padding: 0 0 10px;
    }
    .title{ display:flex; flex-direction:column; gap:6px; }
    .title h1{ margin:0; font-size: 18px; font-weight: 700; letter-spacing: .2px; }
    .subtitle{
      font-size: 13px;
      color: var(--muted);
      display:flex; flex-wrap:wrap; gap:8px; align-items:center;
    }

    .chip{
      display:inline-flex; align-items:center; gap:8px;
      padding: 6px 10px;
      border-radius: 999px;
      border: 1px solid var(--border);
      background: rgba(255,255,255,.04);
      color: var(--muted);
      font-size: 12px;
      white-space: nowrap;
    }

    .grid{
      display:grid;
      grid-template-columns: 1.2fr .8fr;
      gap: 14px;
      align-items: start;
    }
    @media (max-width: 860px){
      .grid{ grid-template-columns: 1fr; }
    }

    .card{
      border: 1px solid var(--border);
      background: linear-gradient(180deg, rgba(255,255,255,.06), rgba(255,255,255,.035));
      border-radius: var(--radius);
      box-shadow: var(--shadow);
      overflow:hidden;
    }
    .card .hd{
      padding: 14px 14px 10px;
      border-bottom: 1px solid rgba(255,255,255,.08);
      display:flex; align-items:center; justify-content:space-between; gap:10px;
    }
    .card .hd .h{
      font-weight: 650;
      font-size: 13px;
      color: var(--text);
      display:flex; align-items:center; gap:10px;
    }
    .card .bd{ padding: 14px; }

    .crumbs{
      display:flex; flex-wrap:wrap; gap:6px; align-items:center;
      margin-top: 2px;
    }
    .crumbs a, .crumbs span{
      font-size: 12px;
      color: var(--muted);
      text-decoration:none;
      padding: 6px 10px;
      border-radius: 999px;
      border: 1px solid rgba(255,255,255,.10);
      background: rgba(255,255,255,.03);
    }
    .crumbs .sep{
      border:none; background:transparent; padding:0; margin:0 2px;
      color: var(--muted2);
    }
    .crumbs a:hover{ color: var(--text); border-color: rgba(255,255,255,.18); }

    .btn{
      display:inline-flex; align-items:center; justify-content:center; gap:8px;
      border: 1px solid rgba(255,255,255,.16);
      background: rgba(255,255,255,.06);
      color: var(--text);
      padding: 10px 12px;
      border-radius: 12px;
      font-weight: 650;
      font-size: 13px;
      text-decoration:none;
      cursor:pointer;
      user-select:none;
    }
    .btn:hover{ background: rgba(255,255,255,.09); border-color: rgba(255,255,255,.22); }
    .btn.primary{
      border: none;
      background: linear-gradient(135deg, var(--accent), var(--accent2));
      color: #07101f;
    }
    .btn.primary:hover{ filter: brightness(1.03); }
    .btn.small{ padding: 8px 10px; border-radius: 10px; font-size: 12px; }

    .search{
      width: 100%;
      padding: 10px 12px;
      border-radius: 12px;
      border: 1px solid rgba(255,255,255,.14);
      background: rgba(0,0,0,.18);
      color: var(--text);
      outline:none;
      font-size: 13px;
    }
    .search::placeholder{ color: rgba(255,255,255,.45); }

    .list{ display:flex; flex-direction:column; gap:8px; }
    .item{
      display:flex; align-items:center; justify-content:space-between; gap:10px;
      padding: 10px 10px;
      border-radius: 14px;
      border: 1px solid rgba(255,255,255,.10);
      background: rgba(255,255,255,.03);
    }
    .item:hover{ background: rgba(255,255,255,.06); border-color: rgba(255,255,255,.16); }
    .meta{ display:flex; flex-direction:column; gap:2px; min-width:0; }
    .name{
      font-weight: 650;
      font-size: 13px;
      white-space:nowrap; overflow:hidden; text-overflow:ellipsis;
      max-width: 520px;
    }
    .sub{
      font-size: 12px; color: var(--muted);
      display:flex; gap:10px; flex-wrap:wrap;
    }
    .tag{ font-size: 11px; color: var(--muted2); }
    .ico{
      width: 34px; height: 34px;
      border-radius: 12px;
      display:flex; align-items:center; justify-content:center;
      background: rgba(255,255,255,.06);
      border: 1px solid rgba(255,255,255,.12);
      flex: 0 0 auto;
    }

    .drop{
      border: 1px dashed rgba(255,255,255,.22);
      background: rgba(255,255,255,.03);
      border-radius: var(--radius);
      padding: 14px;
      display:flex; flex-direction:column; gap:10px;
    }
    .drop.dragover{
      border-color: rgba(124,92,255,.85);
      background: rgba(124,92,255,.12);
    }

    .help{
      font-size: 12px; color: var(--muted);
      line-height: 1.4;
    }

    .empty{
      padding: 14px;
      border-radius: 14px;
      border: 1px solid rgba(255,255,255,.10);
      background: rgba(255,255,255,.03);
      color: var(--muted);
      font-size: 13px;
    }
  </style>
</head>
<body>
  <div class="topbar">
    <div class="wrap">
      <div class="header">
        <div class="title">
          <h1>Proberaum Transfer</h1>
          <div class="subtitle">
            <span class="chip">Ordner: <b style="color:var(--text)">/{{enc.Encode(rel)}}</b></span>
            <span class="chip">Download: tippen • Upload: Datei wählen oder ziehen</span>
          </div>

          <div class="crumbs">
            <a href="/?path=">Home</a>
            {{(crumbs.Count == 0 ? "<span class=\"sep\">/</span><span>Root</span>" : "")}}
            {{string.Join("", crumbs.Select(c => $"<span class=\"sep\">/</span><a href='/?path={Q(c.path)}'>{enc.Encode(c.label)}</a>"))}}
          </div>
        </div>

        <div style="display:flex; gap:8px; flex-wrap:wrap; justify-content:flex-end;">
          <a class="btn small" href="/?path={{Q(parentRel)}}" title="Eine Ebene hoch">⬆️ Hoch</a>
          <a class="btn small" href="/?path={{Q(rel)}}" title="Neu laden">⟳ Refresh</a>
        </div>
      </div>
    </div>
  </div>

  <div class="wrap">
    <div class="grid">

      <div class="card">
        <div class="hd">
          <div class="h">📂 Dateien & Ordner</div>
          <div style="min-width: 240px; max-width: 420px; width: 100%;">
            <input id="q" class="search" placeholder="Suchen (Name enthält …)" autocomplete="off"/>
          </div>
        </div>
        <div class="bd">
          <div class="list" id="list">
            <div class="empty" id="empty" style="display:none;">Keine Treffer.</div>
""");

                            // Ordner
                            foreach (var d in Directory.EnumerateDirectories(currentDir).Select(Path.GetFileName).OrderBy(n => n))
                            {
                                var next = string.IsNullOrEmpty(rel) ? d : $"{rel}/{d}";
                                await context.Response.WriteAsync($$"""
            <div class="item" data-name="{{enc.Encode(d!)}}" data-kind="dir">
              <div style="display:flex; gap:10px; align-items:center; min-width:0;">
                <div class="ico">📁</div>
                <div class="meta">
                  <div class="name">{{enc.Encode(d!)}}</div>
                  <div class="sub"><span class="tag">Ordner</span></div>
                </div>
              </div>
              <a class="btn small" href="/?path={{Q(next)}}">Öffnen →</a>
            </div>
""");
                            }

                            // Dateien
                            foreach (var f in Directory.EnumerateFiles(currentDir).Select(Path.GetFileName).OrderBy(n => n))
                            {
                                var fileRel = string.IsNullOrEmpty(rel) ? f : $"{rel}/{f}";
                                var full = Path.Combine(currentDir, f!);
                                var fi = new FileInfo(full);
                                var size = fi.Exists ? fi.Length : 0;
                                var modified = fi.Exists ? fi.LastWriteTime : DateTime.MinValue;

                                await context.Response.WriteAsync($$"""
            <div class="item" data-name="{{enc.Encode(f!)}}" data-kind="file">
              <div style="display:flex; gap:10px; align-items:center; min-width:0;">
                <div class="ico">📄</div>
                <div class="meta">
                  <div class="name">{{enc.Encode(f!)}}</div>
                  <div class="sub">
                    <span class="tag">{{enc.Encode(HumanBytes(size))}}</span>
                    <span class="tag">•</span>
                    <span class="tag">{{enc.Encode(modified.ToString("yyyy-MM-dd HH:mm"))}}</span>
                  </div>
                </div>
              </div>
              <a class="btn small primary" href="/download?path={{Q(fileRel)}}">Download</a>
            </div>
""");
                            }

                            await context.Response.WriteAsync($$"""
          </div>
        </div>
      </div>

      <div class="card">
        <div class="hd">
          <div class="h">⬆️ Upload</div>
        </div>
        <div class="bd">
          <div class="drop" id="drop">
            <div class="help">
              <b>Smartphone → PC</b><br/>
              Datei auswählen oder per Drag&Drop hier ablegen. Upload landet im aktuellen Ordner.
            </div>

            <form id="form" method="post" enctype="multipart/form-data" action="/upload?path={{Q(rel)}}">
              <input id="file" type="file" name="file"
                     style="width:100%; padding:10px; border-radius:12px; border:1px solid rgba(255,255,255,.14); background:rgba(0,0,0,.18); color:var(--text);" />
              <div style="display:flex; gap:10px; margin-top:10px; flex-wrap:wrap;">
                <button class="btn primary" type="submit">Hochladen</button>
                <a class="btn" href="/?path={{Q(rel)}}">Abbrechen</a>
              </div>
            </form>

            <div class="help" id="hint" style="display:none;">Upload läuft …</div>
          </div>
        </div>
      </div>

    </div>
  </div>

  <script>
    (function(){
      const q = document.getElementById('q');
      const list = document.getElementById('list');
      const empty = document.getElementById('empty');
      const items = Array.from(list.querySelectorAll('.item'));

      function applyFilter(){
        const term = (q.value || '').trim().toLowerCase();
        let shown = 0;
        for(const it of items){
          const name = (it.getAttribute('data-name') || '').toLowerCase();
          const ok = !term || name.includes(term);
          it.style.display = ok ? '' : 'none';
          if(ok) shown++;
        }
        empty.style.display = shown ? 'none' : '';
      }

      q.addEventListener('input', applyFilter);
      applyFilter();

      // Drag&drop upload
      const drop = document.getElementById('drop');
      const file = document.getElementById('file');
      const form = document.getElementById('form');
      const hint = document.getElementById('hint');

      function prevent(e){ e.preventDefault(); e.stopPropagation(); }

      ['dragenter','dragover','dragleave','drop'].forEach(ev => {
        drop.addEventListener(ev, prevent, false);
      });

      ['dragenter','dragover'].forEach(ev => {
        drop.addEventListener(ev, () => drop.classList.add('dragover'), false);
      });
      ['dragleave','drop'].forEach(ev => {
        drop.addEventListener(ev, () => drop.classList.remove('dragover'), false);
      });

      drop.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        if(dt && dt.files && dt.files.length){
          file.files = dt.files;
          // optional: auto-submit
          // form.requestSubmit();
        }
      });

      form.addEventListener('submit', () => {
        hint.style.display = '';
        hint.textContent = 'Upload läuft … Bitte warten.';
      });
    })();
  </script>
</body>
</html>
""");
                        });

                        // DOWNLOAD
                        endpoints.MapGet("/download", async context =>
                        {
                            var rel = NormalizeRel(context.Request.Query["path"].ToString() ?? "");
                            var fullPath = SafeCombineFile(_root, rel);

                            if (fullPath == null || !File.Exists(fullPath))
                            {
                                context.Response.StatusCode = 404;
                                return;
                            }

                            var fileName = Path.GetFileName(fullPath);
                            context.Response.ContentType = MediaTypeNames.Application.Octet;
                            context.Response.Headers.ContentDisposition =
                                $"attachment; filename=\"{fileName}\"";

                            await context.Response.SendFileAsync(fullPath);
                        });

                        // UPLOAD
                        endpoints.MapPost("/upload", async context =>
                        {
                            var rel = NormalizeRel(context.Request.Query["path"].ToString() ?? "");
                            var targetDir = SafeCombineDir(_root, rel);

                            if (targetDir == null)
                            {
                                context.Response.StatusCode = 400;
                                return;
                            }

                            if (!context.Request.HasFormContentType)
                            {
                                context.Response.StatusCode = 400;
                                return;
                            }

                            var form = await context.Request.ReadFormAsync();
                            var file = form.Files.GetFile("file");

                            if (file == null || file.Length == 0)
                            {
                                context.Response.StatusCode = 400;
                                return;
                            }

                            Directory.CreateDirectory(targetDir);

                            var safeName = Path.GetFileName(file.FileName);
                            var dest = Path.Combine(targetDir, safeName);

                            await using var fs = File.Create(dest);
                            await file.CopyToAsync(fs);

                            context.Response.Redirect("/?path=" + HttpUtility.UrlEncode(rel));
                        });
                    });
                });
            })
            .Build();

        await _host.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_host == null) return;
        await _host.StopAsync(ct);
        _host.Dispose();
        _host = null;
    }

    private static string NormalizeRel(string rel)
    {
        rel = (rel ?? "").Replace('\\', '/').Trim();
        rel = rel.TrimStart('/');
        while (rel.Contains("//")) rel = rel.Replace("//", "/");
        return rel;
    }

    private static string? SafeCombineDir(string root, string rel)
    {
        var fullRoot = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(fullRoot, rel));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return null;
        return full;
    }

    private static string? SafeCombineFile(string root, string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return null;
        return SafeCombineDir(root, rel);
    }
}