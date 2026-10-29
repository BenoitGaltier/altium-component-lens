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

public sealed class LensSession(AltiumClient altium, IHostApplicationLifetime lifetime)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private WorkspaceConnection? a;
    private WorkspaceConnection? b;
    private ScanJob? job;
    public object Current() => new { A = a?.Info, B = b?.Info, Job = job?.Snapshot() };
    public async Task<WorkspaceInfo> Connect(string slot, ConnectRequest request, CancellationToken ct)
    {
        if (slot is not ("a" or "b")) throw new LensException("invalidSlot");
        await gate.WaitAsync(ct);
        try
        {
            if (job?.Running == true) throw new LensException("scanRunning", status: 409);
            var connection = await altium.Connect(slot, request, ct);
            if (slot == "a") a = connection; else b = connection;
            job?.Cancellation.Dispose();
            job = null;
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
            job?.Cancellation.Dispose();
            job = new(request.IgnoreCase);
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
            var tasks = new[] { altium.Load(first, scan.Report, linked.Token), altium.Load(second, scan.Report, linked.Token) };
            var loaded = await Task.WhenAll(tasks);
            linked.Token.ThrowIfCancellationRequested();
            scan.Complete(DuplicateDetector.Compare(first.Info, loaded[0], second.Info, loaded[1], scan.IgnoreCase));
        }
        catch (OperationCanceledException) { scan.Fail("cancelled"); }
        catch (LensException ex) { scan.Fail(ex.Code); }
        catch (Exception) { scan.Fail("unexpectedError"); }
    }
    public ScanJob GetJob(string id) => job is not null && job.Id == id ? job : throw new LensException("jobNotFound", status: 404);
}
