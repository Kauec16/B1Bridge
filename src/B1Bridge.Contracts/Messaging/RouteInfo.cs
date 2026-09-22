namespace B1Bridge.Contracts.Messaging;

public sealed class RouteInfo
{
    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string?> Query { get; init; } =
        new Dictionary<string, string?>();
}
