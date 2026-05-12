namespace AgentRp.Components.Common;

public static class StatefulFormCollectionDirty
{
    public static bool ItemDirty<TItem>(
        IEnumerable<TItem> currentItems,
        IEnumerable<TItem> baselineItems,
        string id,
        Func<TItem, string> getId)
    {
        var current = currentItems.FirstOrDefault(item => string.Equals(getId(item), id, StringComparison.Ordinal));
        var baseline = baselineItems.FirstOrDefault(item => string.Equals(getId(item), id, StringComparison.Ordinal));
        return !StatefulFormSnapshot.Equivalent(current, baseline);
    }

    public static bool ValueDirty<TValue>(TValue current, TValue baseline) =>
        !StatefulFormSnapshot.Equivalent(current, baseline);

    public static bool AnyDirty<TItem>(
        IEnumerable<TItem> currentItems,
        IEnumerable<TItem> baselineItems) =>
        !StatefulFormSnapshot.Equivalent(currentItems, baselineItems);
}
