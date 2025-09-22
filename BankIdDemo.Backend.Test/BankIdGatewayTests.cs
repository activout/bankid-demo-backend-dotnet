using System.Net;
using System.Net.Http;
using System.Text.Json;
using Activout.RestClient;
using Activout.RestClient.Helpers;
using Activout.RestClient.Helpers.Implementation;
using Activout.RestClient.Implementation;
using Activout.RestClient.ParamConverter;
using Activout.RestClient.ParamConverter.Implementation;
using Activout.RestClient.Serialization.Implementation;
using BankIdDemo.Backend.Gateways;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RichardSzalay.MockHttp;
using Xunit;
using JsonSerializer = Activout.RestClient.Serialization.Implementation.JsonSerializer;

namespace BankIdDemo.Backend.Test;

public class BankIdGatewayTests
{
    private const string BaseUri = "https://appapi2.test.bankid.com/rp/v6.0";
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly IRestClientFactory _restClientFactory;

    public BankIdGatewayTests()
    {
        var services = new ServiceCollection();
        services.AddTransient<IDuckTyping, DuckTyping>();
        services.AddTransient<IParamConverterManager, ParamConverterManager>();
        services.AddTransient<IRestClientFactory, RestClientFactory>();
        services.AddTransient<ITaskConverterFactory, TaskConverter2Factory>();
        
        var serviceProvider = services.BuildServiceProvider();
        _restClientFactory = serviceProvider.GetRequiredService<IRestClientFactory>();
    }

    private IBankIdClient CreateBankIdClient()
    {
        var jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        return _restClientFactory
            .CreateBuilder()
            .With(new JsonDeserializer(jsonSerializerSettings) { Order = -100 })
            .With(new JsonSerializer(jsonSerializerSettings) { Order = -100 })
            .With(_mockHttp.ToHttpClient())
            .BaseUri(new Uri(BaseUri))
            .Build<IBankIdClient>();
    }

    private IBankIdGateway CreateBankIdGateway()
    {
        var bankIdClient = CreateBankIdClient();
        return new BankIdGateway(bankIdClient);
    }

    [Fact]
    public async Task Auth_ShouldReturnAuthResponse_WhenSuccessful()
    {
        // Arrange
        const string endUserIp = "192.168.1.1";
        const string expectedOrderRef = "order-123";
        const string expectedAutoStartToken = "auto-start-token-456";

        var responseData = new ApiAuthResponse(
            expectedOrderRef,
            expectedAutoStartToken,
            "qr-start-token",
            "qr-start-secret"
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/auth")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Auth(endUserIp);

        // Assert
        Assert.Equal(expectedOrderRef, result.OrderRef);
        Assert.Equal(expectedAutoStartToken, result.AutoStartToken);
    }

    [Fact]
    public async Task Sign_ShouldReturnAuthResponse_WhenSuccessful()
    {
        // Arrange
        const string endUserIp = "192.168.1.2";
        const string expectedOrderRef = "sign-order-789";
        const string expectedAutoStartToken = "sign-auto-start-token-101";

        var responseData = new ApiAuthResponse(
            expectedOrderRef,
            expectedAutoStartToken,
            "sign-qr-start-token",
            "sign-qr-start-secret"
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/sign")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Sign(endUserIp);

        // Assert
        Assert.Equal(expectedOrderRef, result.OrderRef);
        Assert.Equal(expectedAutoStartToken, result.AutoStartToken);
    }

    [Fact]
    public async Task Collect_ShouldReturnPendingResponse_WhenTransactionIsPending()
    {
        // Arrange
        const string orderRef = "pending-order-123";

        var responseData = new ApiCollectResponse(
            orderRef,
            "pending",
            "outstandingTransaction",
            null
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Pending, result.Status);
        Assert.Equal(BankIdHintCode.OutstandingTransaction, result.HintCode);
        Assert.Null(result.CompletionData);
    }

    [Fact]
    public async Task Collect_ShouldReturnCompleteResponse_WhenTransactionIsComplete()
    {
        // Arrange
        const string orderRef = "complete-order-456";

        var completionData = new ApiCompletionData(
            new ApiUser("198001011234", "Test Testsson", "Test", "Testsson"),
            new ApiDevice("192.168.1.1", "unique-hardware-id"),
            "2023-06-29",
            new ApiStepUp(false),
            "signature-data",
            "ocsp-response"
        );

        var responseData = new ApiCollectResponse(
            orderRef,
            "complete",
            null,
            completionData
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Complete, result.Status);
        Assert.Null(result.HintCode);
        Assert.NotNull(result.CompletionData);
        Assert.Equal("198001011234", result.CompletionData.User.PersonalNumber);
        Assert.Equal("Test Testsson", result.CompletionData.User.Name);
        Assert.Equal("Test", result.CompletionData.User.GivenName);
        Assert.Equal("Testsson", result.CompletionData.User.Surname);
        Assert.Equal("192.168.1.1", result.CompletionData.Device.IpAddress);
        Assert.Equal("unique-hardware-id", result.CompletionData.Device.UniqueHardwareId);
        Assert.Equal("2023-06-29", result.CompletionData.BankIdIssueDate);
        Assert.NotNull(result.CompletionData.StepUp);
        Assert.False(result.CompletionData.StepUp.Mrtd);
        Assert.Equal("signature-data", result.CompletionData.Signature);
        Assert.Equal("ocsp-response", result.CompletionData.OcspResponse);
    }

    [Fact]
    public async Task Collect_ShouldReturnFailedResponse_WhenTransactionFails()
    {
        // Arrange
        const string orderRef = "failed-order-789";

        var responseData = new ApiCollectResponse(
            orderRef,
            "failed",
            "userCallConfirm",
            null
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Failed, result.Status);
        Assert.Equal(BankIdHintCode.UserCallConfirm, result.HintCode);
        Assert.Null(result.CompletionData);
    }

    [Fact]
    public async Task Collect_ShouldHandleUnknownStatus_WhenStatusIsNotRecognized()
    {
        // Arrange
        const string orderRef = "unknown-status-order";

        var responseData = new ApiCollectResponse(
            orderRef,
            "unknownStatus",
            "unknownHintCode",
            null
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Unknown, result.Status);
        Assert.Equal(BankIdHintCode.Unknown, result.HintCode);
        Assert.Null(result.CompletionData);
    }

    [Fact]
    public async Task Collect_ShouldHandleNullStepUp_InCompletionData()
    {
        // Arrange
        const string orderRef = "null-stepup-order";

        var completionData = new ApiCompletionData(
            new ApiUser("198001011234", "Test Testsson", "Test", "Testsson"),
            new ApiDevice("192.168.1.1", "unique-hardware-id"),
            "2023-06-29",
            null, // StepUp is null
            "signature-data",
            "ocsp-response"
        );

        var responseData = new ApiCollectResponse(
            orderRef,
            "complete",
            null,
            completionData
        );

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", JsonConvert.SerializeObject(responseData, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));

        var gateway = CreateBankIdGateway();

        // Act
        var result = await gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Complete, result.Status);
        Assert.NotNull(result.CompletionData);
        Assert.Null(result.CompletionData.StepUp);
    }

    [Fact]
    public async Task Cancel_ShouldCompleteSuccessfully_WhenRequestIsValid()
    {
        // Arrange
        const string orderRef = "cancel-order-123";

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/cancel")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond(HttpStatusCode.OK);

        var gateway = CreateBankIdGateway();

        // Act & Assert (should not throw)
        await gateway.Cancel(orderRef);
    }

    [Fact]
    public async Task Cancel_ShouldIgnoreRestClientException_WhenErrorOccurs()
    {
        // Arrange
        const string orderRef = "error-order-123";

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/cancel")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond(HttpStatusCode.BadRequest, "application/json", 
                JsonConvert.SerializeObject(new BankIdErrorResponse("invalidParameters", "Order not found")));

        var gateway = CreateBankIdGateway();

        // Act & Assert (should not throw, should ignore the exception)
        await gateway.Cancel(orderRef);
    }
}