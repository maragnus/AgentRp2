namespace AgentRp.Components.Common;

public sealed record StatefulFormPathScope(string PathPrefix)
{
    public string Combine(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathPrefix;

        if (string.IsNullOrWhiteSpace(PathPrefix))
            return path;

        return $"{PathPrefix}.{path}";
    }
}
