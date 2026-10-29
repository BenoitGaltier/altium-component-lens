using System.Net;
using System.Text;
using System.Text.Json;
using ComponentLens;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class FixtureHandler(bool partial = false, bool small = true) : HttpMessageHandler
{
    public int Requests;
    public bool RejectAuthorization;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Requests);
        if (RejectAuthorization) return new(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") };
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var root = body.RootElement; var query = root.GetProperty("query").GetString()!; var variables = root.GetProperty("variables");
        var slot = request.RequestUri!.Host.Contains("qa-a", StringComparison.Ordinal) ? "a" : "b";
        var count = small ? (slot == "a" ? 23 : 27) : (slot == "a" ? 1323 : 1866);
        var reported = slot == "a" ? count - 2 : count;
        var category = new { componentTypeId = "res", name = "Resistors", path = "Passives/Resistors" };
        object data;
        if (query.Contains("LensCategories(", StringComparison.Ordinal))
            data = new { desLibrary = new { name = "TEST " + slot.ToUpperInvariant(), components = new { totalCount = reported },
                componentTypes = new { nodes = new[] { category }, pageInfo = new { hasNextPage = false, endCursor = (string?)null } } } };
        else if (query.Contains("LensComponents(", StringComparison.Ordinal))
        {
            var cursor = variables.GetProperty("after");
            var offset = cursor.ValueKind == JsonValueKind.Null ? 0 : int.Parse(cursor.GetString()!);
            // Two independently traversed inventories differ only in the explicit partial fixture.
            if (offset == 0) traversals.AddOrUpdate(slot, 1, (_, value) => value + 1);
            var changed = partial && slot == "a" && traversals[slot] > 1;
            var next = Math.Min(offset + AltiumClient.PageSize, count);
            var nodes = Enumerable.Range(offset, next - offset).Select(i => new { id = slot + "-" + i, name = "TEST-" + slot.ToUpperInvariant() + "-" + i,
                revisionId = "01", description = "Fictitious regression component", componentType = category, folder = new { path = "Components/Resistors" },
                manufacturerParts = new[] { new { companyName = "Fixture Manufacturer", partNumber = "TEST-MPN-" + (i % 37).ToString("000") +
                    (changed && i == count - 1 ? "-CHANGED" : "") } } }).ToArray();
            data = new { desLibrary = new { components = new { totalCount = reported, nodes, pageInfo = new { hasNextPage = next < count, endCursor = next.ToString() } } } };
        }
        else
        {
            var id = variables.GetProperty("id").GetString()!;
            var i = int.Parse(id[2..]);
            data = new { desComponentById = new { id, details = new { parameters = new[] {
                new { name = "Manufacturer", value = "Fixture Manufacturer" }, new { name = "Manufacturer Part Number", value = "TEST-MPN-" + (i % 37).ToString("000") }
            }, itemParameters = Array.Empty<object>() } } };
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { data }), Encoding.UTF8, "application/json") };
    }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> traversals = new();
}

public static class FixtureServer
{
    public static async Task Run(string[] args)
    {
        var handler = new FixtureHandler(args.Contains("--fixture-partial"), false);
        await using var app = LensHost.Build(args, services => services.AddSingleton(new HttpClient(handler)));
        app.MapGet("/test/stats", () => new { handler.Requests });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        Console.WriteLine("Open: " + address + "/#session=" + LensHost.SessionKey(app));
        await app.WaitForShutdownAsync();
    }
}
