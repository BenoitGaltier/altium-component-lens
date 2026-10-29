using System.Net;
using System.Text;
using System.Text.Json;
using ComponentLens;

if (args.Contains("--fixture-server")) { await FixtureServer.Run(args); return 0; }

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> run) => tests.Add((name, run));
void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
async Task Code(string code, Func<Task> action)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (LensException ex) { Assert(ex.Code == code, $"Expected {code}, got {ex.Code}"); }
}
Category res = new("res", "Resistors", "Passives/Resistors");
Component C(string id, string manufacturer = "Yageo", string mpn = "RC-100", IReadOnlyList<Parameter>? parameters = null, IReadOnlyList<Parameter>? items = null, string? error = null) =>
    new(id, id, "01", "Test", res, "Components", parameters ?? [], items ?? [], manufacturer.Length > 0 ? [new(manufacturer, mpn)] : [], error);
WorkspaceInfo W(string slot) => new(slot, slot.ToUpperInvariant(), $"https://{slot}.365.altium.com/api/graphql", 0, [res]);
WorkspaceConnection WC(string slot) => new(W(slot), "test-token-never-export");

Test("Cross-workspace and internal groups retain all components", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1"), C("a2")], W("b"), [C("b1")], true);
    Assert(r.Groups.Count == 1 && r.Groups[0].A.Count == 2 && r.Groups[0].B.Count == 1);
    Assert(r.Summary.CrossGroups == 1 && r.Summary.InternalGroupsA == 1 && r.Summary.InternalGroupsB == 0);
});
Test("Manufacturer AND MPN are required; punctuation is preserved", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1")], W("b"), [C("b1", "Other"), C("b2", mpn: "RC100")], true);
    Assert(r.Groups.Count == 0);
});
Test("Normalization supports Unicode, external spaces and case", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1", "  Texas  Instruments ", "ＡＢＣ-123 ")], W("b"), [C("b1", "texas instruments", "abc-123")], true);
    Assert(r.Groups.Count == 1);
});
Test("Internal MPN whitespace and manufacturer aliases are not merged", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1", "TI", "ABC 123")], W("b"), [C("b1", "Texas Instruments", "ABC 123"), C("b2", "TI", "ABC123")], true);
    Assert(r.Groups.Count == 0);
});
Test("Strict mode preserves exact case and whitespace", () => {
    Assert(DuplicateDetector.Compare(W("a"), [C("a1", "Yageo")], W("b"), [C("b1", "YAGEO")], false).Groups.Count == 0);
    Assert(DuplicateDetector.Compare(W("a"), [C("a1", " Yageo")], W("b"), [C("b1", "Yageo")], false).Groups.Count == 0);
});
Test("Parameters, item inheritance and indexed pairs are supported", () => {
    var c = C("a1", "", parameters: [new("Manufacturer", "Murata"), new("MPN", "CAP-1"), new("Manufacturer 2", "Yageo"), new("Manufacturer Part Number 2", "RC-2")],
        items: [new("Manufacturer", "Old"), new("Manufacturer Part Number", "CAP-1")]);
    var identities = DuplicateDetector.Identities(c, true);
    Assert(identities.Count == 2 && identities.Any(i => i.Manufacturer == "Murata") && identities.Any(i => i.Mpn == "RC-2"));
});
Test("Unmatched indexed fields do not synthesize a manufacturer/MPN pair", () => {
    Assert(DuplicateDetector.Identities(C("a", "", parameters: [new("Manufacturer 1", "Yageo"), new("MPN 2", "RC-1")]), true).Count == 0);
});
Test("Multiple part choices and parameter evidence are deduplicated per component", () => {
    var a = C("a1", parameters: [new("Manufacturer", "Yageo"), new("MPN", "RC-100")]) with { PartChoices = [new("Yageo", "RC-100"), new("YAGEO", "RC-100"), new("Vishay", "R-20")] };
    var r = DuplicateDetector.Compare(W("a"), [a], W("b"), [C("b1"), C("b2", "Vishay", "R-20")], true);
    Assert(r.Groups.Count == 2 && r.Groups.All(g => g.A.Count == 1));
    Assert(r.Groups.Single(g => g.Mpn == "RC-100").A[0].Evidence.Count == 2);
});
Test("Missing identities and detail failures are explicit, never silent success", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1", "", error: "apiHttp")], W("b"), [C("b1", "N/A", "-")], true);
    Assert(r.Groups.Count == 0 && r.Summary.MissingIdentity == 2 && r.Summary.DetailFailures == 1 && r.Completeness == "partial");
});
Test("Workspace-only duplicates are included as separate scopes", () => {
    var r = DuplicateDetector.Compare(W("a"), [C("a1"), C("a2")], W("b"), [], true);
    Assert(r.Groups.Count == 1 && r.Groups[0].Scope == "a" && r.Summary.CrossGroups == 0);
});
Test("Repeated component IDs do not count as independent duplicates", () => {
    Assert(DuplicateDetector.Compare(W("a"), [C("a1"), C("a1")], W("b"), [], true).Groups.Count == 0);
});
Test("Endpoint validation prevents credential redirects and arbitrary hosts", () => {
    Assert(AltiumClient.Endpoint("test.365.altium.com").AbsoluteUri == "https://test.365.altium.com/api/graphql");
    foreach (var input in new[] { "http://a.365.altium.com", "https://a.365.altium.com.evil.test", "https://a.365.altium.com@evil.test", "https://a.365.altium.com:444", "https://127.0.0.1", "https://a.365.altium.com/api/graphql?token=x", "https://a.365.altium.com/other" })
    {
        try { AltiumClient.Endpoint(input); throw new Exception("Accepted " + input); } catch (LensException ex) { Assert(ex.Code == "invalidEndpoint"); }
    }
});
Test("Demo is explicitly flagged and exports no API tokens", () => {
    var demo = DemoData.Create(); var json = JsonSerializer.Serialize(demo);
    Assert(demo.Demo && demo.Summary.CrossGroups == 4 && demo.Summary.MissingIdentity == 2);
    Assert(!json.Contains("Token", StringComparison.OrdinalIgnoreCase));
});

const string categoryPage1 = """{"data":{"desLibrary":{"name":"Library A","components":{"totalCount":2},"componentTypes":{"nodes":[{"componentTypeId":"res","name":"Resistors","path":"Passives/Resistors"}],"pageInfo":{"hasNextPage":true,"endCursor":"cat-1"}}}}}""";
const string categoryPage2 = """{"data":{"desLibrary":{"name":"Library A","components":{"totalCount":2},"componentTypes":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}""";
const string componentsPage1 = """{"data":{"desLibrary":{"components":{"totalCount":2,"nodes":[{"id":"c1","name":"CMP-1","revisionId":"01","description":"Test","folder":{"path":"Components"},"componentType":{"componentTypeId":"res","name":"Resistors","path":"Passives/Resistors"},"manufacturerParts":[{"companyName":"Yageo","partNumber":"RC-100"}]}],"pageInfo":{"hasNextPage":true,"endCursor":"page-1"}}}}}""";
const string componentsPage2 = """{"data":{"desLibrary":{"components":{"totalCount":2,"nodes":[{"id":"c2","name":"CMP-2","revisionId":"02","description":"Test 2","folder":null,"componentType":null,"manufacturerParts":[]}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}""";
const string details = """{"data":{"desComponentById":{"id":"c1","details":{"parameters":[{"name":"Manufacturer","value":"Yageo"},{"name":"MPN","value":"RC-100"}],"itemParameters":[]}}}}""";
JsonDocument Request(HttpRequestMessage request) => JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
AltiumClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => new(new HttpClient(new MockHandler(handler)));

AsyncTest("Connect paginates real component categories and sends bearer token safely", async () => {
    int requests = 0;
    var client = Client((req, ct) => {
        using var body = Request(req); requests++;
        Assert(req.Method == HttpMethod.Post && req.Headers.Authorization?.Scheme == "Bearer" && req.Headers.Authorization.Parameter == "test-token");
        Assert(body.RootElement.GetProperty("variables").GetProperty("workspace").GetString() == "https://a.365.altium.com");
        Assert(!body.RootElement.GetProperty("query").GetString()!.Contains("mutation"));
        return Task.FromResult(Json(requests == 1 ? categoryPage1 : categoryPage2));
    });
    var connection = await client.Connect("a", new("", "a.365.altium.com", "Bearer test-token"), default);
    Assert(requests == 2 && connection.Info.Name == "Library A" && connection.Info.Categories.Count == 1 && connection.Info.ComponentCount == 2);
});
AsyncTest("All component pages and all per-component parameters are read", async () => {
    var pages = 0; var reads = 0;
    var client = Client((req, ct) => {
        using var body = Request(req); var query = body.RootElement.GetProperty("query").GetString()!;
        if (query.Contains("LensComponents(")) { Interlocked.Increment(ref pages); return Task.FromResult(Json(pages == 1 ? componentsPage1 : componentsPage2)); }
        Interlocked.Increment(ref reads); return Task.FromResult(Json(details.Replace("\"id\":\"c1\"", "\"id\":\"" + body.RootElement.GetProperty("variables").GetProperty("id").GetString() + "\"")));
    });
    var list = await client.Load(WC("a"), _ => {}, default);
    Assert(pages == 2 && reads == 2 && list.Count == 2 && list[0].Parameters.Count == 2 && list[1].Category is null);
});
AsyncTest("Repeated pagination cursor is rejected", async () => {
    var client = Client((req, ct) => Task.FromResult(Json(componentsPage1)));
    await Code("apiCursorRepeated", () => client.Load(WC("a"), _ => {}, default));
});
AsyncTest("An early API end produces an explicit partial report with retrieved components", async () => {
    InventoryDiagnostics? inventory = null;
    var client = Client((req, ct) => {
        using var body = Request(req);
        return Task.FromResult(Json(body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(") ? componentsPage2 : details.Replace("\"id\":\"c1\"", "\"id\":\"c2\"")));
    });
    var loaded = await client.Load(WC("a"), _ => {}, default, value => inventory = value);
    Assert(loaded.Count == 1 && inventory!.Warnings.Contains("reportedCountMismatch"));
    var result = DuplicateDetector.Compare(W("a"), loaded, W("b"), [], true, inventories: [inventory!]);
    Assert(result.Completeness == "partial" && result.Inventories![0].DistinctComponents == 1);
});
AsyncTest("A changing total does not stop cursor traversal and marks the report partial", async () => {
    var pages = 0;
    InventoryDiagnostics? inventory = null;
    var client = Client((req, ct) => {
        using var body = Request(req);
        if (body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents("))
            return Task.FromResult(Json(++pages == 1 ? componentsPage1 : componentsPage2.Replace("\"totalCount\":2", "\"totalCount\":3")));
        return Task.FromResult(Json(details.Replace("\"id\":\"c1\"", "\"id\":\"" + body.RootElement.GetProperty("variables").GetProperty("id").GetString() + "\"")));
    });
    var loaded = await client.Load(WC("a"), _ => {}, default, value => inventory = value);
    Assert(pages == 2 && loaded.Count == 2 && inventory!.ReportedTotals.SequenceEqual(new[] { 2, 3 }));
    Assert(inventory!.Warnings.Contains("reportedTotalChanged"));
    Assert(DuplicateDetector.Compare(W("a"), loaded, W("b"), [], true, inventories: [inventory]).Completeness == "partial");
});
AsyncTest("A next-page flag without a cursor reports a specific error", async () => {
    var client = Client((req, ct) => Task.FromResult(Json(componentsPage1.Replace("\"endCursor\":\"page-1\"", "\"endCursor\":null"))));
    await Code("apiCursorMissing", () => client.Load(WC("a"), _ => {}, default));
});
AsyncTest("An intermediate page without new components cannot loop forever", async () => {
    var pages = 0;
    var client = Client((req, ct) => Task.FromResult(Json(componentsPage1.Replace("page-1", "page-" + ++pages))));
    await Code("apiPaginationStalled", () => client.Load(WC("a"), _ => {}, default));
});
AsyncTest("A repeated component with changed part choices is retained and reported as partial", async () => {
    var pages = 0;
    InventoryDiagnostics? inventory = null;
    var client = Client((req, ct) => {
        using var body = Request(req);
        if (!body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(")) return Task.FromResult(Json(details));
        return Task.FromResult(Json(++pages == 1 ? componentsPage1.Replace("\"totalCount\":2", "\"totalCount\":1") :
            componentsPage1.Replace("\"totalCount\":2", "\"totalCount\":1").Replace("\"hasNextPage\":true", "\"hasNextPage\":false").Replace("RC-100", "RC-200")));
    });
    var loaded = await client.Load(WC("a"), _ => {}, default, value => inventory = value);
    Assert(loaded.Count == 1 && loaded[0].PartChoices[0].Mpn == "RC-200" && inventory!.Warnings.Contains("componentChanged"));
});
foreach (var overlappingRows in new[] { true, false })
AsyncTest(overlappingRows ? "Regression: 1323 rows for 1321 components deduplicate without false duplicates" : "Regression: stable 1323 distinct components with API total 1321 are independently verified", async () => {
    const int announced = 1321;
    var actual = overlappingRows ? 1321 : 1323;
    var detailReads = 0;
    InventoryDiagnostics? inventory = null;
    var client = Client((req, ct) => {
        using var body = Request(req); var variables = body.RootElement.GetProperty("variables");
        if (!body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(")) {
            Interlocked.Increment(ref detailReads);
            return Task.FromResult(Json(details.Replace("\"id\":\"c1\"", "\"id\":\"" + variables.GetProperty("id").GetString() + "\"")));
        }
        var after = variables.GetProperty("after");
        var offset = after.ValueKind == JsonValueKind.Null ? 0 : int.Parse(after.GetString()!);
        var next = Math.Min(offset + AltiumClient.PageSize, actual);
        var indices = Enumerable.Range(offset, next - offset).ToList();
        if (overlappingRows && next == actual) indices.AddRange([0, 1]);
        var nodes = indices.Select(i => new { id = "c" + i, name = "CMP-" + i, revisionId = "01", description = "", folder = (object?)null,
            componentType = (object?)null, manufacturerParts = new[] { new { companyName = "Yageo", partNumber = "RC-" + i } } }).ToArray();
        return Task.FromResult(Json(JsonSerializer.Serialize(new { data = new { desLibrary = new { components = new {
            totalCount = announced, nodes, pageInfo = new { hasNextPage = next < actual, endCursor = next.ToString() }
        } } } })));
    });
    var loaded = await client.Load(WC("a"), _ => {}, default, value => inventory = value);
    Assert(loaded.Count == actual && detailReads == actual);
    Assert(inventory!.RowsRead == 1323 && inventory.Pages == 14 && inventory.RepeatedRows == (overlappingRows ? 2 : 0));
    var result = DuplicateDetector.Compare(W("a"), loaded, W("b"), [], true, inventories: [inventory]);
    Assert(result.Completeness == "complete");
    if (!overlappingRows) Assert(inventory.Verification?.Matches == true && inventory.Notices!.Contains("apiCountVerified"));
    // Part choices remain distinct; only the mocked identical parameter identity forms a group.
    Assert(result.Groups.All(g => g.A.Select(o => o.Id).Distinct().Count() == g.A.Count));
});
foreach (var failure in new[] { "differentIds", "invalidResponse", "authentication" })
AsyncTest("Inventory verification preserves real uncertainty: " + failure, async () => {
    var pages = 0; InventoryDiagnostics? inventory = null;
    var client = Client((req, ct) => {
        using var body = Request(req);
        if (!body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents("))
            return Task.FromResult(Json(details.Replace("\"id\":\"c1\"", "\"id\":\"" + body.RootElement.GetProperty("variables").GetProperty("id").GetString() + "\"")));
        pages++;
        if (pages == 3 && failure == "invalidResponse") return Task.FromResult(Json("{}"));
        if (pages == 3 && failure == "authentication") return Task.FromResult(Json("{}", HttpStatusCode.Unauthorized));
        var page = pages % 2 == 1 ? componentsPage1 : componentsPage2;
        if (pages > 2) page = page.Replace("\"id\":\"c2\"", "\"id\":\"changed-id\"");
        return Task.FromResult(Json(page.Replace("\"totalCount\":2", "\"totalCount\":1")));
    });
    if (failure == "authentication") { await Code("apiAuth", () => client.Load(WC("a"), _ => {}, default)); return; }
    var loaded = await client.Load(WC("a"), _ => {}, default, value => inventory = value);
    Assert(loaded.Count == 2 && inventory!.Warnings.Contains("reportedCountMismatch") && inventory.Verification?.Matches == false);
    Assert(inventory!.Warnings.Contains(failure == "differentIds" ? "inventoryVerificationMismatch" : "inventoryVerificationFailed"));
    Assert(DuplicateDetector.Compare(W("a"), loaded, W("b"), [], true, inventories: [inventory]).Completeness == "partial");
});
AsyncTest("Token expiry during detail reads stops the whole scan", async () => {
    var client = Client((req, ct) => {
        using var body = Request(req);
        var listing = body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(");
        return Task.FromResult(listing ? Json(componentsPage1.Replace("\"totalCount\":2", "\"totalCount\":1").Replace("\"hasNextPage\":true", "\"hasNextPage\":false")) : Json("{}", HttpStatusCode.Unauthorized));
    });
    await Code("apiAuth", () => client.Load(WC("a"), _ => {}, default));
});
AsyncTest("A mismatched detail identifier is flagged rather than attributed to another component", async () => {
    var client = Client((req, ct) => {
        using var body = Request(req);
        return Task.FromResult(Json(body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(")
            ? componentsPage1.Replace("\"totalCount\":2", "\"totalCount\":1").Replace("\"hasNextPage\":true", "\"hasNextPage\":false")
            : details.Replace("\"id\":\"c1\"", "\"id\":\"unrelated\"")));
    });
    var list = await client.Load(WC("a"), _ => {}, default);
    Assert(list[0].DetailError == "apiFormat" && list[0].Parameters.Count == 0);
});
AsyncTest("GraphQL partial responses are treated as errors", async () => {
    var client = Client((req, ct) => Task.FromResult(Json("""{"data":{"desLibrary":null},"errors":[{"message":"private server info"}]}""")));
    await Code("apiGraphql", () => client.Connect("a", new("A", W("a").Endpoint, "test-token"), default));
});
AsyncTest("Authorization and redirect responses never masquerade as empty libraries", async () => {
    foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect }) {
        var client = Client((req, ct) => Task.FromResult(Json("{}", status)));
        await Code(status == HttpStatusCode.Redirect ? "apiHttp" : "apiAuth", () => client.Connect("a", new("A", W("a").Endpoint, "test-token"), default));
    }
});
AsyncTest("Temporary throttling is retried and then succeeds", async () => {
    int requests = 0;
    var client = Client((req, ct) => {
        requests++; if (requests == 1) { var retry = Json("{}", HttpStatusCode.TooManyRequests); retry.Headers.RetryAfter = new(TimeSpan.FromSeconds(1)); return Task.FromResult(retry); }
        return Task.FromResult(Json(categoryPage2));
    });
    await client.Connect("a", new("A", W("a").Endpoint, "test-token"), default); Assert(requests == 2);
});
AsyncTest("Detail errors retain known Part Choices and mark the report partial", async () => {
    var client = Client((req, ct) => {
        using var body = Request(req);
        return Task.FromResult(Json(body.RootElement.GetProperty("query").GetString()!.Contains("LensComponents(")
            ? componentsPage1.Replace("\"totalCount\":2", "\"totalCount\":1").Replace("\"hasNextPage\":true", "\"hasNextPage\":false")
            : """{"errors":[{"message":"Not allowed"}],"data":null}"""));
    });
    var list = await client.Load(WC("a"), _ => {}, default);
    Assert(list[0].DetailError == "apiGraphql" && list[0].PartChoices.Count == 1);
    Assert(DuplicateDetector.Compare(W("a"), list, W("b"), [C("b1")], true).Completeness == "partial");
});
AsyncTest("Cancellation aborts outstanding API reads", async () => {
    var client = Client(async (req, ct) => { await Task.Delay(10000, ct); return Json("{}"); });
    using var cts = new CancellationTokenSource(20);
    try { await client.Load(WC("a"), _ => {}, cts.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { Assert(cts.IsCancellationRequested); }
});
AsyncTest("Session resumes completed results without returning tokens and invalidates them on reconnect", async () => {
    var pages = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
    var client = Client((req, ct) => {
        using var body = Request(req); var query = body.RootElement.GetProperty("query").GetString()!;
        if (query.Contains("LensCategories(")) return Task.FromResult(Json(categoryPage2));
        if (query.Contains("LensComponents(")) return Task.FromResult(Json(pages.AddOrUpdate(req.RequestUri!.Host, 1, (_, value) => value + 1) == 1 ? componentsPage1 : componentsPage2));
        return Task.FromResult(Json(details.Replace("\"id\":\"c1\"", "\"id\":\"" + body.RootElement.GetProperty("variables").GetProperty("id").GetString() + "\"")));
    });
    var session = new LensSession(client, new TestLifetime());
    await session.Connect("a", new("A", W("a").Endpoint, "test-token"), default);
    await session.Connect("b", new("B", W("b").Endpoint, "test-token"), default);
    var snapshot = JsonSerializer.SerializeToElement(await session.Start(new(true), default));
    var id = snapshot.GetProperty("Id").GetString()!;
    for (var i = 0; session.GetJob(id).Running && i < 100; i++) await Task.Delay(5);
    var result = session.GetJob(id).Result;
    Assert(result.Summary.CrossGroups == 1 && result.Completeness == "complete");
    var current = JsonSerializer.Serialize(session.Current());
    Assert(current.Contains(id) && current.Contains("completed") && !current.Contains("test-token") && !current.Contains("Token"));
    await session.Connect("a", new("Changed A", W("a").Endpoint, "test-token"), default);
    await Code("jobNotFound", () => { session.GetJob(id); return Task.CompletedTask; });
});

PersistenceTests.Register(AsyncTest);

var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
Console.WriteLine($"\n{tests.Count - failures}/{tests.Count} tests passed");
return failures == 0 ? 0 : 1;

sealed class MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request, ct);
}
sealed class TestLifetime : Microsoft.Extensions.Hosting.IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() { }
}
