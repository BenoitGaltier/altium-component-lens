using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ComponentLens;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

if (args.Contains("--help"))
{
    Console.WriteLine("Component Lens 1.0.1\nUsage: ComponentLens.exe [--headless] [--port 0-65535]\nDefault: random loopback port, opens your default browser. Close this window or use Quit in the app to stop.");
    return;
}
var port = 0;
var portIndex = Array.IndexOf(args, "--port");
if (portIndex >= 0 && (portIndex + 1 >= args.Length || !int.TryParse(args[portIndex + 1], out port) || port is < 0 or > 65535))
    throw new ArgumentException("Invalid --port value");
var builder = WebApplication.CreateSlimBuilder([]);
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options => { options.Listen(IPAddress.Loopback, port); options.Limits.MaxRequestBodySize = 32 * 1024; });
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) });
builder.Services.AddSingleton<AltiumClient>();
builder.Services.AddSingleton<LensSession>();
var app = builder.Build();
var sessionKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

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
app.MapPost("/api/workspaces/{slot}/connect", async (string slot, ConnectRequest input, LensSession session, CancellationToken ct) => await session.Connect(slot, input, ct));
app.MapPost("/api/scans", async (ScanRequest input, LensSession session, CancellationToken ct) => await session.Start(input, ct));
app.MapGet("/api/scans/{id}", (string id, LensSession session) => session.GetJob(id).Snapshot());
app.MapGet("/api/scans/{id}/results", (string id, LensSession session) => session.GetJob(id).Result);
app.MapPost("/api/scans/{id}/cancel", (string id, LensSession session) => { session.GetJob(id).Cancellation.Cancel(); return Results.Ok(); });
app.MapGet("/api/demo", () => DemoData.Create());
app.MapPost("/api/quit", (IHostApplicationLifetime lifetime) =>
{
    _ = Task.Run(async () => { await Task.Delay(300); lifetime.StopApplication(); });
    return Results.Ok();
});

var assembly = Assembly.GetExecutingAssembly();
var assets = new Dictionary<string, (string Resource, string Mime)>
{
    ["/"] = ("index.html", "text/html; charset=utf-8"),
    ["/app.mjs"] = ("app.mjs", "text/javascript; charset=utf-8"),
    ["/core.mjs"] = ("core.mjs", "text/javascript; charset=utf-8"),
    ["/i18n.mjs"] = ("i18n.mjs", "text/javascript; charset=utf-8"),
    ["/style.css"] = ("style.css", "text/css; charset=utf-8"),
    ["/favicon.svg"] = ("favicon.svg", "image/svg+xml")
};
foreach (var (path, asset) in assets)
    app.MapGet(path, () => Results.Stream(assembly.GetManifestResourceStream("ComponentLens.Web." + asset.Resource)!, asset.Mime));

await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var launchUrl = address + "/#session=" + sessionKey;
Console.WriteLine("\n  COMPONENT LENS  1.0.1\n  Portable Altium 365 duplicate finder\n");
Console.WriteLine("  Open: " + launchUrl);
Console.WriteLine("\n  Keep this window open. Close it, press Ctrl+C, or choose Quit in the app to stop.\n  Tokens remain in memory for this session.\n");
if (!args.Contains("--headless"))
{
    try { Process.Start(new ProcessStartInfo(launchUrl) { UseShellExecute = true }); }
    catch (Exception) { Console.WriteLine("  Browser could not be opened automatically. Copy the URL above into your browser."); }
}
await app.WaitForShutdownAsync();
