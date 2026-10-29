namespace ComponentLens;

public sealed record Category(string Id, string Name, string Path);
public sealed record Parameter(string Name, string Value);
public sealed record PartChoice(string Manufacturer, string Mpn);
public sealed record Component(
    string Id, string Name, string Revision, string Description, Category? Category,
    string Folder, IReadOnlyList<Parameter> Parameters, IReadOnlyList<Parameter> ItemParameters,
    IReadOnlyList<PartChoice> PartChoices, string? DetailError = null);
public sealed record WorkspaceInfo(string Slot, string Name, string Endpoint, int ComponentCount, IReadOnlyList<Category> Categories);
public sealed record WorkspaceConnection(WorkspaceInfo Info, string Token);
public sealed record ConnectRequest(string Name, string Endpoint, string Token);
public sealed record ScanRequest(bool IgnoreCase = true);
public sealed record Identity(string Manufacturer, string Mpn, IReadOnlyList<string> Evidence);
public sealed record Occurrence(string Workspace, string WorkspaceName, string Id, string Name,
    string Revision, string Description, Category? Category, string Folder,
    string Manufacturer, string Mpn, IReadOnlyList<string> Evidence, string? DetailError);
public sealed record DuplicateGroup(string Id, string Manufacturer, string Mpn, string Scope,
    IReadOnlyList<Occurrence> A, IReadOnlyList<Occurrence> B);
public sealed record QualityIssue(string Workspace, string WorkspaceName, string Id, string Name,
    Category? Category, string Reason);
public sealed record ScanSummary(int ComponentsA, int ComponentsB, int ComparedA, int ComparedB,
    int CrossGroups, int InternalGroupsA, int InternalGroupsB, int MissingIdentity, int DetailFailures);
public sealed record InventoryDiagnostics(string Workspace, string WorkspaceName, int Pages, int RowsRead,
    int DistinctComponents, int RepeatedRows, IReadOnlyList<int> ReportedTotals, IReadOnlyList<string> Warnings);
public sealed record ScanResult(string Version, bool Demo, string Completeness, DateTimeOffset CreatedAt,
    bool IgnoreCase, WorkspaceInfo A, WorkspaceInfo B, ScanSummary Summary,
    IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<QualityIssue> Issues,
    IReadOnlyList<InventoryDiagnostics>? Inventories = null);
public sealed record ScanProgress(string Workspace, string Phase, int Loaded, int Total);
public sealed class LensException(string code, string? detail = null, int status = 400) : Exception(detail ?? code)
{
    public string Code { get; } = code;
    public string? Detail { get; } = detail;
    public int Status { get; } = status;
}
