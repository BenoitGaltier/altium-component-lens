using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace ComponentLens;

public static class LensHost
{
    public const string Version = "1.1.0";
    public static WebApplication Build(string[] args, Action<IServiceCollection>? configure = null)
    {
        var port = 0;
        var portIndex = Array.IndexOf(args, "--port");
        if (portIndex >= 0 && (portIndex + 1 >= args.Length || !int.TryParse(args[portIndex + 1], out port) || port is < 0 or > 65535))
            throw new ArgumentException("Invalid --port value");
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ComponentLens");
        var directoryIndex = Array.IndexOf(args, "--data-dir");
        if (directoryIndex >= 0)
        {
            if (directoryIndex + 1 >= args.Length || args[directoryIndex + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Invalid --data-dir value");
            directory = Path.GetFullPath(args[directoryIndex + 1]);
        }
        var builder = WebApplication.CreateSlimBuilder([]);
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => { options.Listen(IPAddress.Loopback, port); options.Limits.MaxRequestBodySize = 32 * 1024; });
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) });
        builder.Services.AddSingleton<AltiumClient>();
        if (!args.Contains("--no-persistence")) builder.Services.AddSingleton(_ => new LocalStore(directory));
        builder.Services.AddSingleton<LensSession>();
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        var sessionKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        ((IApplicationBuilder)app).Properties["LensSessionKey"] = sessionKey;
        _ = app.Services.GetRequiredService<LensSession>();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; font-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            if (context.Request.Host.Host != "127.0.0.1") { context.Response.StatusCode = 403; return; }
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var provided = context.Request.Headers["X-Lens-Session"].ToString();
                var origin = context.Request.Headers.Origin.ToString();
                if (provided.Length != sessionKey.Length || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(sessionKey)) ||
                    (origin.Length > 0 && origin != $"http://{context.Request.Host}"))
                { context.Response.StatusCode = 403; return; }
            }
            try { await next(); }
            catch (LensException error)
            {
                context.Response.StatusCode = error.Status;
                await context.Response.WriteAsJsonAsync(new { error = error.Code, detail = error.Detail });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (BadHttpRequestException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = "invalidRequest" }); }
            catch (Exception) { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = "unexpectedError" }); }
        });
        app.MapGet("/api/session", (LensSession session) => session.Current());
        app.MapGet("/api/comparison", (LensSession session) => session.Comparison);
        app.MapPost("/api/preferences", (LanguageRequest input, LensSession session) => { session.SetLanguage(input.Language); return Results.Ok(); });
        app.MapDelete("/api/local-data", async (LensSession session, CancellationToken ct) => { await session.Forget(ct); return Results.Ok(); });
        app.MapPost("/api/workspaces/{slot}/connect", async (string slot, ConnectRequest input, LensSession session, CancellationToken ct) => await session.Connect(slot, input, ct));
        app.MapPost("/api/scans", async (ScanRequest input, LensSession session, CancellationToken ct) => await session.Start(input, ct));
        app.MapGet("/api/scans/{id}", (string id, LensSession session) => session.GetJob(id).Snapshot());
        app.MapGet("/api/scans/{id}/results", (string id, LensSession session) => session.GetJob(id).Result);
        app.MapPost("/api/scans/{id}/cancel", (string id, LensSession session) => { session.GetJob(id).Cancellation.Cancel(); return Results.Ok(); });
        app.MapGet("/api/demo", () => DemoData.Create());
        app.MapPost("/api/quit", (IHostApplicationLifetime lifetime) =>
        {
            _ = Task.Run(async () => { await Task.Delay(300); lifetime.StopApplication(); }); return Results.Ok();
        });
        var assets = new Dictionary<string, (string Resource, string Mime)>
        {
            ["/"] = ("index.html", "text/html; charset=utf-8"), ["/app.mjs"] = ("app.mjs", "text/javascript; charset=utf-8"),
            ["/core.mjs"] = ("core.mjs", "text/javascript; charset=utf-8"), ["/i18n.mjs"] = ("i18n.mjs", "text/javascript; charset=utf-8"),
            ["/style.css"] = ("style.css", "text/css; charset=utf-8"), ["/favicon.svg"] = ("favicon.svg", "image/svg+xml")
        };
        foreach (var (path, asset) in assets)
            app.MapGet(path, () => Results.Stream(typeof(LensHost).Assembly.GetManifestResourceStream("ComponentLens.Web." + asset.Resource)!, asset.Mime));
        return app;
    }
    public static string SessionKey(WebApplication app) => (string)((IApplicationBuilder)app).Properties["LensSessionKey"]!;
}

public sealed record LanguageRequest(string Language);
