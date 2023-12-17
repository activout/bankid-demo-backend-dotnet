using Activout.RestClient;
using Activout.RestClient.Serialization.Implementation;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using JsonSerializer = Activout.RestClient.Serialization.Implementation.JsonSerializer;

namespace BankIdDemo.Backend.Gateways;

public class BankIdClientFactory(
    HttpClient httpClient,
    IRestClientFactory restClientFactory,
    IOptions<BankIdSettings> bankIdSettings)
{
    private readonly BankIdSettings _settings = bankIdSettings.Value;

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        Converters = SerializationManager.DefaultJsonConverters.ToList(),
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    public IBankIdClient Create()
    {
        return restClientFactory
            .CreateBuilder()
            .With(new JsonDeserializer(_jsonSerializerSettings) { Order = -100 })
            .With(new JsonSerializer(_jsonSerializerSettings) { Order = -100 })
            .With(httpClient)
            .BaseUri(new Uri(_settings.ApiUrl))
            .Build<IBankIdClient>();
    }
}