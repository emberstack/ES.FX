namespace ES.FX.OpenData.Internal;

internal sealed class OpenDataHub(IServiceProvider provider, IEnumerable<IOpenDatasetRegistration> registrations)
    : IOpenData
{
    private readonly Lazy<IReadOnlyList<OpenDatasetInfo>> _datasets =
        new(() => registrations.Select(r => r.Info).ToArray());

    public T GetDataset<T>() where T : class, IOpenDataset =>
        provider.GetService(typeof(T)) as T
        ?? throw new InvalidOperationException(
            $"OpenData dataset '{typeof(T).Name}' is not registered. Install its ES.FX.OpenData.* package and " +
            "call the matching Add… method (e.g. AddCountries()) on the AddOpenData() builder.");

    public IReadOnlyList<OpenDatasetInfo> Datasets => _datasets.Value;
}
