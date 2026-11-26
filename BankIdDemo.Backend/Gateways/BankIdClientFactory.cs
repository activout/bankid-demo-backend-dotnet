using System.Text.Json;
using System.Text.Json.Serialization;
using Activout.RestClient;
using Activout.RestClient.Json;
using Microsoft.Extensions.Options;

namespace BankIdDemo.Backend.Gateways;

public class BankIdClientFactory(
    HttpClient httpClient,
    IRestClientFactory restClientFactory,
    IOptions<BankIdSettings> bankIdSettings)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly BankIdSettings _settings = bankIdSettings.Value;

    public IBankIdClient Create()
    {
        return restClientFactory
            .CreateBuilder()
            .WithSystemTextJson(JsonOptions)
            .With(httpClient)
            .BaseUri(new Uri(_settings.ApiUrl))
            .Build<IBankIdClient>();
    }
}
