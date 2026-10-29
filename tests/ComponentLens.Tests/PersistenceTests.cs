using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ComponentLens;

public static class PersistenceTests
{
    private static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static string DirectoryForTest() => Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "store-tests", Guid.NewGuid().ToString("N"));
    private static WorkspaceConnection Connection(string slot) => new(new(slot, "TEST " + slot, $"https://qa-{slot}.365.altium.com/api/graphql", 0, []), "fixture-secret-" + slot);
    private static async Task Code(string code, Func<Task> run)
    {
        try { await run(); throw new Exception("Expected " + code); } catch (LensException ex) { Assert(ex.Code == code, "Expected " + code + ", got " + ex.Code); }
    }
    private static async Task<ScanResult> Scan(LensSession session)
    {
        var id = JsonSerializer.SerializeToElement(await session.Start(new(true), default)).GetProperty("Id").GetString()!;
        for (var i = 0; i < 1000 && session.GetJob(id).Running; i++) await Task.Delay(5);
        return session.GetJob(id).Result;
    }
    private static async Task Connect(LensSession session)
    {
        foreach (var slot in new[] { "a", "b" }) await session.Connect(slot, new("TEST " + slot, Connection(slot).Info.Endpoint, Connection(slot).Token), default);
    }
    public static void Register(Action<string, Func<Task>> test)
    {
        test("DPAPI encrypted storage round-trips tokens, URLs and report without plaintext on disk", () => {
            using var store = new LocalStore(DirectoryForTest());
            var state = new StoredState(1, Connection("a"), Connection("b"), null, "es"); store.Save(state);
            var bytes = File.ReadAllBytes(store.FilePath); var text = Encoding.UTF8.GetString(bytes);
            Assert(!text.Contains("fixture-secret") && !text.Contains("qa-a.365.altium.com"));
            var restored = store.Load()!; Assert(restored.A!.Token == state.A!.Token && restored.B!.Info.Endpoint == state.B!.Info.Endpoint && restored.Language == "es");
            return Task.CompletedTask;
        });
        test("Atomic save keeps the previous encrypted file when encryption fails", async () => {
            var protector = new FailingProtector(); using var store = new LocalStore(DirectoryForTest(), protector);
            store.Save(new(1, Connection("a"), null, null)); var before = SHA256.HashData(File.ReadAllBytes(store.FilePath));
            protector.Fail = true;
            await Code("storageCrypto", () => { store.Save(new(1, null, null, null)); return Task.CompletedTask; });
            Assert(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(store.FilePath))) && !Directory.EnumerateFiles(store.DirectoryPath, "*.tmp").Any());
        });
        test("Changed ciphertext is rejected and preserved, without plaintext fallback", async () => {
            using var store = new LocalStore(DirectoryForTest()); store.Save(new(1, Connection("a"), null, null));
            var bytes = File.ReadAllBytes(store.FilePath); bytes[^1] ^= 0x55; File.WriteAllBytes(store.FilePath, bytes);
            await Code("storageCrypto", () => { store.Load(); return Task.CompletedTask; });
            Assert(File.ReadAllBytes(store.FilePath).SequenceEqual(bytes));
        });
        test("A second app cannot overwrite the same local state directory", async () => {
            var directory = DirectoryForTest(); using var store = new LocalStore(directory);
            await Code("storageInUse", () => { using var other = new LocalStore(directory); return Task.CompletedTask; });
        });
        test("Unsupported saved format is reported without replacing its file", async () => {
            using var store = new LocalStore(DirectoryForTest()); store.Save(new(99, null, null, null));
            await Code("storageVersion", () => { store.Load(); return Task.CompletedTask; });
            Assert(File.Exists(store.FilePath));
        });
        test("A restart restores both workspaces, language and full comparison with zero API calls", async () => {
            var directory = DirectoryForTest(); ScanResult expected;
            using (var store = new LocalStore(directory)) {
                var handler = new FixtureHandler(); var session = new LensSession(new(new HttpClient(handler)), new TestLifetime(), store);
                await Connect(session); session.SetLanguage("es"); expected = await Scan(session);
                Assert(expected.Completeness == "complete" && expected.Inventories![0].Verification!.Matches);
            }
            using (var store = new LocalStore(directory)) {
                var handler = new FixtureHandler(); var session = new LensSession(new(new HttpClient(handler)), new TestLifetime(), store);
                var snapshot = JsonSerializer.SerializeToElement(session.Current());
                Assert(snapshot.GetProperty("A").GetProperty("Restored").GetBoolean() && snapshot.GetProperty("ComparisonRestored").GetBoolean());
                Assert(snapshot.GetProperty("Language").GetString() == "es" && session.Comparison.CreatedAt == expected.CreatedAt && session.Comparison.Groups.Count == expected.Groups.Count);
                Assert(handler.Requests == 0 && !snapshot.GetRawText().Contains("fixture-secret"));
            }
        });
        test("Saved token can be reused for its own endpoint but is never sent to a changed workspace", async () => {
            using var store = new LocalStore(DirectoryForTest()); var handler = new FixtureHandler();
            var session = new LensSession(new(new HttpClient(handler)), new TestLifetime(), store); await Connect(session);
            await session.Connect("a", new("Renamed", Connection("a").Info.Endpoint, ""), default);
            var requests = handler.Requests;
            await Code("savedTokenMismatch", () => session.Connect("a", new("Changed", "https://other.365.altium.com", ""), default));
            Assert(handler.Requests == requests && store.Load()!.A!.Token == Connection("a").Token);
        });
        test("Refreshing a token preserves the previous comparison; changing the workspace invalidates it", async () => {
            using var store = new LocalStore(DirectoryForTest()); var session = new LensSession(new(new HttpClient(new FixtureHandler())), new TestLifetime(), store);
            await Connect(session); var comparison = await Scan(session);
            await session.Connect("a", new("TEST a", Connection("a").Info.Endpoint, "new-fixture-secret"), default);
            Assert(session.Comparison.CreatedAt == comparison.CreatedAt && store.Load()!.A!.Token == "new-fixture-secret");
            await session.Connect("a", new("Other", "https://other.365.altium.com", "other-fixture-secret"), default);
            Assert(store.Load()!.Comparison is null);
            await Code("resultsUnavailable", () => { _ = session.Comparison; return Task.CompletedTask; });
        });
        test("Failed refresh with expired saved credentials preserves the dated cached comparison", async () => {
            var directory = DirectoryForTest(); DateTimeOffset date;
            using (var store = new LocalStore(directory)) { var session = new LensSession(new(new HttpClient(new FixtureHandler())), new TestLifetime(), store); await Connect(session); date = (await Scan(session)).CreatedAt; }
            using (var store = new LocalStore(directory)) {
                var handler = new FixtureHandler { RejectAuthorization = true }; var session = new LensSession(new(new HttpClient(handler)), new TestLifetime(), store);
                var job = JsonSerializer.SerializeToElement(await session.Start(new(true), default)); var id = job.GetProperty("Id").GetString()!;
                for (var i = 0; i < 1000 && session.GetJob(id).Running; i++) await Task.Delay(5);
                Assert(JsonSerializer.SerializeToElement(session.GetJob(id).Snapshot()).GetProperty("ErrorCode").GetString() == "apiAuth");
                Assert(session.Comparison.CreatedAt == date && store.Load()!.Comparison!.CreatedAt == date);
            }
        });
        test("Partial comparisons remain partial after an encrypted save and restart", async () => {
            var directory = DirectoryForTest();
            using (var store = new LocalStore(directory)) { var session = new LensSession(new(new HttpClient(new FixtureHandler(true))), new TestLifetime(), store); await Connect(session); Assert((await Scan(session)).Completeness == "partial"); }
            using (var store = new LocalStore(directory)) { var handler = new FixtureHandler(); var session = new LensSession(new(new HttpClient(handler)), new TestLifetime(), store); Assert(session.Comparison.Completeness == "partial" && handler.Requests == 0); }
        });
        test("Forget removes the encrypted state and backup, and clears connections and result", async () => {
            using var store = new LocalStore(DirectoryForTest()); var session = new LensSession(new(new HttpClient(new FixtureHandler())), new TestLifetime(), store);
            await Connect(session); await Scan(session); Assert(File.Exists(store.FilePath + ".bak"));
            await session.Forget(default); var current = JsonSerializer.SerializeToElement(session.Current());
            Assert(!File.Exists(store.FilePath) && !File.Exists(store.FilePath + ".bak") && current.GetProperty("A").ValueKind == JsonValueKind.Null && !current.GetProperty("HasComparison").GetBoolean());
        });
        test("A damaged saved file starts with an explicit storage error, without erasing it", () => {
            using var store = new LocalStore(DirectoryForTest()); File.WriteAllText(store.FilePath, "broken saved file");
            var session = new LensSession(new(new HttpClient(new FixtureHandler())), new TestLifetime(), store);
            Assert(JsonSerializer.SerializeToElement(session.Current()).GetProperty("StorageError").GetString() == "storageRead");
            Assert(File.ReadAllText(store.FilePath) == "broken saved file"); return Task.CompletedTask;
        });
    }
}

sealed class FailingProtector : IDataProtector
{
    private readonly WindowsDataProtector actual = new();
    public bool Fail;
    public byte[] Protect(byte[] data) => Fail ? throw new LensException("storageCrypto") : actual.Protect(data);
    public byte[] Unprotect(byte[] data) => actual.Unprotect(data);
}
