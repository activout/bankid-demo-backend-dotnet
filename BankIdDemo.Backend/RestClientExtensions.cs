using Activout.RestClient;
using Activout.RestClient.Implementation;
using Activout.RestClient.ParamConverter;
using Activout.RestClient.ParamConverter.Implementation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BankIdDemo.Backend;

public static class RestClientExtensions
{
    public static IServiceCollection AddRestClient(this IServiceCollection self)
    {
        self.TryAddTransient<IParamConverterManager, ParamConverterManager>();
        self.TryAddTransient<IRestClientFactory, RestClientFactory>();
        return self;
    }
}
