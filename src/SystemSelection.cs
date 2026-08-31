namespace prepareBikeParking;

/// <summary>
/// Turns what the operator typed for "which system" into a list of ids.
/// Accepts a single id, a comma-separated list, or "all".
/// </summary>
public static class SystemSelection
{
    public static async Task<List<int>> ResolveAsync(string spec)
    {
        spec = (spec ?? "").Trim();

        if (spec.Length == 0)
        {
            throw new ArgumentException("No system specified. Pass a system ID, a comma-separated list, or 'all'.");
        }

        if (spec.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var all = await BikeShareSystemLoader.LoadAllSystemsAsync();
            var ids = all.Select(s => s.Id).OrderBy(id => id).ToList();
            if (ids.Count == 0)
            {
                throw new InvalidOperationException("'all' matched no systems - bikeshare_systems.json is empty.");
            }
            return ids;
        }

        var parsed = new List<int>();
        foreach (var token in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, out var id))
            {
                throw new ArgumentException($"'{token}' is not a system ID. Pass a number, a comma-separated list, or 'all'.");
            }
            if (!parsed.Contains(id)) parsed.Add(id);
        }

        return parsed;
    }
}
