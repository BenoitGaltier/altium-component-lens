namespace ComponentLens;

public sealed class ScanJob(bool ignoreCase)
{
    private readonly object sync = new();
    private readonly Dictionary<string, ScanProgress> progress = [];
    private string status = "running";
    private string? errorCode;
    private ScanResult? result;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public bool IgnoreCase { get; } = ignoreCase;
    public CancellationTokenSource Cancellation { get; } = new();
    public bool Running { get { lock (sync) return status == "running"; } }
    public ScanResult Result { get { lock (sync) return result ?? throw new LensException("resultsUnavailable", status: 409); } }
    public void Report(ScanProgress value) { lock (sync) progress[value.Workspace] = value; }
    public void Complete(ScanResult value) { lock (sync) { result = value; status = "completed"; } }
    public void Fail(string code) { lock (sync) { status = code == "cancelled" ? "cancelled" : "failed"; errorCode = code; } }
    public object Snapshot() { lock (sync) return new { Id, Status = status, ErrorCode = errorCode, Progress = progress.Values.ToArray() }; }
}

public sealed class LensSession
{
    private readonly AltiumClient altium;
    private readonly IHostApplicationLifetime lifetime;
    private readonly LocalStore? store;
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private WorkspaceConnection? a;
    private WorkspaceConnection? b;
    private ScanJob? job;
    private ScanResult? comparison;
    private bool comparisonRestored;
    private string? language;
    private string? storageError;
    public LensSession(AltiumClient altium, IHostApplicationLifetime lifetime, LocalStore? store = null)
    {
        this.altium = altium; this.lifetime = lifetime; this.store = store;
        try
        {
            if (store?.Load() is { } saved)
            {
                a = Restore(saved.A); b = Restore(saved.B); comparison = saved.Comparison;
                comparisonRestored = comparison is not null; language = saved.Language;
            }
        }
        catch (LensException ex) { storageError = ex.Code; }
    }
    private static WorkspaceConnection? Restore(WorkspaceConnection? connection) => connection is null ? null :
        connection with { Info = connection.Info with { Restored = true } };
    public object Current()
    {
        lock (sync) return new { A = a?.Info, B = b?.Info, Job = job?.Snapshot(), HasComparison = comparison is not null,
            ComparisonRestored = comparisonRestored, Language = language,
            SavedCredentials = store is null ? Array.Empty<string>() : new[] { a?.Info.Slot, b?.Info.Slot }.OfType<string>().ToArray(),
            DataDirectory = store?.DirectoryPath, StorageError = storageError };
    }
    private void Persist()
    {
        store?.Save(new(1, a, b, comparison, language));
        storageError = null;
    }
    public ScanResult Comparison { get { lock (sync) return comparison ?? throw new LensException("resultsUnavailable", status: 409); } }
    public async Task<WorkspaceInfo> Connect(string slot, ConnectRequest request, CancellationToken ct)
    {
        if (slot is not ("a" or "b")) throw new LensException("invalidSlot");
        await gate.WaitAsync(ct);
        try
        {
            if (job?.Running == true) throw new LensException("scanRunning", status: 409);
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                var previous = slot == "a" ? a : b;
                if (previous is null) throw new LensException("invalidToken");
                if (AltiumClient.Endpoint(request.Endpoint).AbsoluteUri != previous.Info.Endpoint) throw new LensException("savedTokenMismatch");
                request = request with { Token = previous.Token };
            }
            var connection = await altium.Connect(slot, request, ct);
            lock (sync)
            {
                var oldA = a; var oldB = b; var oldComparison = comparison;
                if (slot == "a") a = connection; else b = connection;
                if (comparison is not null && (comparison.A.Endpoint != a?.Info.Endpoint || comparison.B.Endpoint != b?.Info.Endpoint)) comparison = null;
                try { Persist(); }
                catch (LensException ex) { a = oldA; b = oldB; comparison = oldComparison; storageError = ex.Code; throw; }
                job?.Cancellation.Dispose(); job = null;
            }
            return connection.Info;
        }
        finally { gate.Release(); }
    }
    public async Task<object> Start(ScanRequest request, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (job?.Running == true) throw new LensException("scanRunning", status: 409);
            if (a is null || b is null) throw new LensException("connectBoth");
            if (new Uri(a.Info.Endpoint).Host.Equals(new Uri(b.Info.Endpoint).Host, StringComparison.OrdinalIgnoreCase)) throw new LensException("sameWorkspace");
            lock (sync) { job?.Cancellation.Dispose(); job = new(request.IgnoreCase); }
            _ = Run(job, a, b);
            return job.Snapshot();
        }
        finally { gate.Release(); }
    }
    private async Task Run(ScanJob scan, WorkspaceConnection first, WorkspaceConnection second)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(scan.Cancellation.Token, lifetime.ApplicationStopping);
        try
        {
            var inventories = new System.Collections.Concurrent.ConcurrentDictionary<string, InventoryDiagnostics>();
            void Inventory(InventoryDiagnostics value) => inventories[value.Workspace] = value;
            var tasks = new[] { altium.Load(first, scan.Report, linked.Token, Inventory), altium.Load(second, scan.Report, linked.Token, Inventory) };
            var loaded = await Task.WhenAll(tasks);
            linked.Token.ThrowIfCancellationRequested();
            var result = DuplicateDetector.Compare(first.Info, loaded[0], second.Info, loaded[1], scan.IgnoreCase,
                inventories: inventories.Values.OrderBy(x => x.Workspace).ToArray());
            lock (sync)
            {
                comparison = result; comparisonRestored = false;
                try { Persist(); } catch (LensException ex) { storageError = ex.Code; }
                scan.Complete(result);
            }
        }
        catch (OperationCanceledException) { scan.Fail("cancelled"); }
        catch (LensException ex) { scan.Fail(ex.Code); }
        catch (Exception) { scan.Fail("unexpectedError"); }
    }
    public ScanJob GetJob(string id) { lock (sync) return job is not null && job.Id == id ? job : throw new LensException("jobNotFound", status: 404); }
    public void SetLanguage(string value)
    {
        if (value is not ("fr" or "en" or "es")) throw new LensException("invalidRequest");
        lock (sync)
        {
            var previous = language; language = value;
            try { Persist(); } catch (LensException ex) { language = previous; storageError = ex.Code; throw; }
        }
    }
    public async Task Forget(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            lock (sync)
            {
                if (job?.Running == true) throw new LensException("scanRunning", status: 409);
                store?.Clear(); a = null; b = null; comparison = null; comparisonRestored = false; storageError = null;
                job?.Cancellation.Dispose(); job = null;
            }
        }
        finally { gate.Release(); }
    }
}
