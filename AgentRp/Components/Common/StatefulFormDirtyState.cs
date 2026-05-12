namespace AgentRp.Components.Common;

public static class StatefulFormDirtyState
{
    public static bool Resolve(
        IStatefulFormContext? form,
        StatefulFormPathScope? scope,
        bool dirty,
        string? dirtyPath,
        IReadOnlyList<string>? dirtyPaths,
        string? dirtyScopeId)
    {
        if (dirty)
            return true;

        if (!string.IsNullOrWhiteSpace(dirtyScopeId) && form?.IsScopeDirty(dirtyScopeId) == true)
            return true;

        if (!string.IsNullOrWhiteSpace(dirtyPath) && form?.IsPathDirty(scope?.Combine(dirtyPath) ?? dirtyPath) == true)
            return true;

        return dirtyPaths is not null && dirtyPaths.Any(path => form?.IsPathDirty(scope?.Combine(path) ?? path) == true);
    }
}
