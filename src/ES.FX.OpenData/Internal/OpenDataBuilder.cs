using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Internal;

internal sealed class OpenDataBuilder(IServiceCollection services) : IOpenDataBuilder
{
    public IServiceCollection Services => services;
}
