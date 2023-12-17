using Activout.RestClient;
using Activout.RestClient.Helpers;
using Activout.RestClient.Helpers.Implementation;
using Activout.RestClient.Implementation;
using Activout.RestClient.ParamConverter;
using Activout.RestClient.ParamConverter.Implementation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BankIdDemo.Backend;

public static class RestClientExtensions
{
    public static IServiceCollection AddRestClient(this IServiceCollection self)
    {
        self.TryAddTransient<IDuckTyping, DuckTyping>();
        self.TryAddTransient<IParamConverterManager, ParamConverterManager>();
        self.TryAddTransient<IRestClientFactory, RestClientFactory>();
        self.TryAddTransient<ITaskConverterFactory, TaskConverter2Factory>();
        return self;
    }
}