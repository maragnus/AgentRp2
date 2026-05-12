using System.Collections;
using System.Reflection;

namespace AgentRp.Components.Common;

static class StatefulFormPathResolver
{
    public static object? GetValue(object? root, string path)
    {
        if (root is null)
            throw new InvalidOperationException($"Cannot resolve dirty path '{path}' because the form model is null.");

        if (string.IsNullOrWhiteSpace(path))
            return root;

        object? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is null)
                return null;

            current = ResolveSegment(current, segment, path);
        }

        return current;
    }

    public static string? FindObjectPath(object? root, object target)
    {
        if (root is null)
            return null;

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return FindObjectPath(root, target, "", visited, 0);
    }

    static string? FindObjectPath(object current, object target, string path, HashSet<object> visited, int depth)
    {
        if (ReferenceEquals(current, target))
            return path;

        if (depth > 6 || IsLeaf(current.GetType()) || !visited.Add(current))
            return null;

        foreach (var property in ReadableProperties(current.GetType()))
        {
            var value = property.GetValue(current);
            if (value is null)
                continue;

            var childPath = string.IsNullOrWhiteSpace(path) ? property.Name : $"{path}.{property.Name}";
            if (ReferenceEquals(value, target))
                return childPath;

            if (value is string || value is IEnumerable)
                continue;

            var result = FindObjectPath(value, target, childPath, visited, depth + 1);
            if (result is not null)
                return result;
        }

        return null;
    }

    static object? ResolveSegment(object current, string segment, string path)
    {
        if (current is IDictionary dictionary)
        {
            if (dictionary.Contains(segment))
                return dictionary[segment];

            throw new InvalidOperationException($"Cannot resolve dirty path '{path}' because dictionary key '{segment}' was not found.");
        }

        var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.GetIndexParameters().Length > 0)
            throw new InvalidOperationException($"Cannot resolve dirty path '{path}' because '{segment}' is not a public readable property on {current.GetType().Name}.");

        return property.GetValue(current);
    }

    static IEnumerable<PropertyInfo> ReadableProperties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);

    static bool IsLeaf(Type type) =>
        type.IsPrimitive ||
        type.IsEnum ||
        type == typeof(string) ||
        type == typeof(decimal) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(Guid);
}
