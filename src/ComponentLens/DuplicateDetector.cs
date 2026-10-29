using System.Text;
using System.Text.RegularExpressions;

namespace ComponentLens;

public static partial class DuplicateDetector
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
    [GeneratedRegex(@"^(.*?)(\d+)$")]
    private static partial Regex IndexedName();
    [GeneratedRegex(@"[^\p{L}\p{Nd}]")]
    private static partial Regex ParameterSeparators();

    public static string Normalize(string value, bool ignoreCase, bool manufacturer = false)
    {
        if (!ignoreCase) return value;
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        if (manufacturer) normalized = Spaces().Replace(normalized, " ");
        return normalized.ToUpperInvariant();
    }

    private static bool Usable(string value) => !new[] { "", "-", "--", "N/A", "NA", "NONE", "NULL", "UNKNOWN", "?" }
        .Contains(value.Trim().ToUpperInvariant(), StringComparer.Ordinal);

    public static IReadOnlyList<Identity> Identities(Component component, bool ignoreCase)
    {
        var identities = new Dictionary<(string, string), (string Manufacturer, string Mpn, HashSet<string> Evidence)>();
        void Add(string manufacturer, string mpn, string evidence)
        {
            if (!Usable(manufacturer) || !Usable(mpn)) return;
            var key = (Normalize(manufacturer, ignoreCase, true), Normalize(mpn, ignoreCase));
            if (!identities.TryGetValue(key, out var identity))
                identity = (manufacturer, mpn, []);
            identity.Evidence.Add(evidence);
            identities[key] = identity;
        }
        foreach (var part in component.PartChoices) Add(part.Manufacturer, part.Mpn, "partChoice");

        // Item parameters are inherited; revision parameters override a parameter with the same semantic name.
        var manufacturers = new Dictionary<string, string>();
        var mpns = new Dictionary<string, string>();
        foreach (var parameter in component.ItemParameters.Concat(component.Parameters))
        {
            var name = ParameterSeparators().Replace(parameter.Name.Normalize(NormalizationForm.FormKC), "").ToLowerInvariant();
            var index = "";
            var numbered = IndexedName().Match(name);
            if (numbered.Success) { name = numbered.Groups[1].Value; index = numbered.Groups[2].Value; }
            if (name is "manufacturer" or "manufacturername" or "mfr" or "mfrname") manufacturers[index] = parameter.Value;
            if (name is "manufacturerpartnumber" or "mpn" or "mfrpartnumber" or "manufacturerpn") mpns[index] = parameter.Value;
        }
        foreach (var (index, manufacturer) in manufacturers)
            if (mpns.TryGetValue(index, out var mpn)) Add(manufacturer, mpn, "parameters");
        return identities.Values.Select(x => new Identity(x.Manufacturer, x.Mpn, x.Evidence.Order().ToArray())).ToArray();
    }

    public static ScanResult Compare(WorkspaceInfo a, IReadOnlyList<Component> componentsA,
        WorkspaceInfo b, IReadOnlyList<Component> componentsB, bool ignoreCase, bool demo = false)
    {
        var index = new Dictionary<(string, string), (List<Occurrence> A, List<Occurrence> B)>();
        var issues = new List<QualityIssue>();
        int comparedA = 0, comparedB = 0;
        void Index(WorkspaceInfo workspace, IReadOnlyList<Component> components)
        {
            foreach (var component in components.DistinctBy(c => c.Id))
            {
                var identities = Identities(component, ignoreCase);
                if (identities.Count == 0) issues.Add(new(workspace.Slot, workspace.Name, component.Id, component.Name, component.Category, "missingIdentity"));
                else if (workspace.Slot == "a") comparedA++; else comparedB++;
                if (component.DetailError is not null) issues.Add(new(workspace.Slot, workspace.Name, component.Id, component.Name, component.Category, "detailFailed"));
                foreach (var identity in identities)
                {
                    var key = (Normalize(identity.Manufacturer, ignoreCase, true), Normalize(identity.Mpn, ignoreCase));
                    if (!index.TryGetValue(key, out var group)) group = ([], []);
                    var occurrence = new Occurrence(workspace.Slot, workspace.Name, component.Id, component.Name, component.Revision,
                        component.Description, component.Category, component.Folder, identity.Manufacturer, identity.Mpn, identity.Evidence, component.DetailError);
                    (workspace.Slot == "a" ? group.A : group.B).Add(occurrence);
                    index[key] = group;
                }
            }
        }
        Index(a, componentsA); Index(b, componentsB);
        var groups = index.Where(x => x.Value.A.Count + x.Value.B.Count > 1)
            .OrderBy(x => x.Key.Item1, StringComparer.Ordinal).ThenBy(x => x.Key.Item2, StringComparer.Ordinal)
            .Select((x, i) => new DuplicateGroup($"D{i + 1:00000}", x.Value.A.Concat(x.Value.B).First().Manufacturer,
                x.Value.A.Concat(x.Value.B).First().Mpn,
                x.Value.A.Count > 0 && x.Value.B.Count > 0 ? "cross" : x.Value.A.Count > 1 ? "a" : "b",
                x.Value.A.OrderBy(o => o.Name, StringComparer.Ordinal).ToArray(), x.Value.B.OrderBy(o => o.Name, StringComparer.Ordinal).ToArray())).ToArray();
        var failures = issues.Count(x => x.Reason == "detailFailed");
        return new("1.0.0", demo, failures > 0 ? "partial" : "complete", DateTimeOffset.UtcNow, ignoreCase, a, b,
            new(componentsA.Count, componentsB.Count, comparedA, comparedB,
                groups.Count(x => x.Scope == "cross"), groups.Count(x => x.A.Count > 1), groups.Count(x => x.B.Count > 1),
                issues.Count(x => x.Reason == "missingIdentity"), failures), groups, issues);
    }
}
