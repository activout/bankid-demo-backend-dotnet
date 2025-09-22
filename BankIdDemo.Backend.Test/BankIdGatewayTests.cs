using System.Net;
using System.Net.Http;
using Activout.RestClient;
using Activout.RestClient.Helpers;
using Activout.RestClient.Helpers.Implementation;
using Activout.RestClient.Implementation;
using Activout.RestClient.ParamConverter;
using Activout.RestClient.ParamConverter.Implementation;
using Activout.RestClient.Serialization.Implementation;
using BankIdDemo.Backend.Gateways;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RichardSzalay.MockHttp;
using Xunit;
using JsonSerializer = Activout.RestClient.Serialization.Implementation.JsonSerializer;

namespace BankIdDemo.Backend.Test;

/// <summary>
/// Tests for the BankIdGateway implementation using mock HTTP responses.
/// This test suite validates the Swedish BankID API v6.0 integration through the IBankIdGateway interface,
/// covering auth, sign, collect, and cancel operations with various response scenarios.
/// </summary>
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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/auth")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{expectedOrderRef}"",
                ""autoStartToken"": ""{expectedAutoStartToken}"",
                ""qrStartToken"": ""qr-start-token"",
                ""qrStartSecret"": ""qr-start-secret""
            }}");

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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/sign")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{expectedOrderRef}"",
                ""autoStartToken"": ""{expectedAutoStartToken}"",
                ""qrStartToken"": ""sign-qr-start-token"",
                ""qrStartSecret"": ""sign-qr-start-secret""
            }}");

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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""complete"",
                ""completionData"": {{
                    ""user"": {{
                        ""personalNumber"": ""198001011234"",
                        ""name"": ""Test Testsson"",
                        ""givenName"": ""Test"",
                        ""surname"": ""Testsson""
                    }},
                    ""device"": {{
                        ""ipAddress"": ""192.168.1.1"",
                        ""uhi"": ""unique-hardware-id""
                    }},
                    ""bankIdIssueDate"": ""2023-06-29"",
                    ""stepUp"": {{
                        ""mrtd"": false
                    }},
                    ""signature"": ""signature-data"",
                    ""ocspResponse"": ""ocsp-response""
                }}
            }}");

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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""failed"",
                ""hintCode"": ""userCallConfirm""
            }}");

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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""unknownStatus"",
                ""hintCode"": ""unknownHintCode""
            }}");

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

        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""complete"",
                ""completionData"": {{
                    ""user"": {{
                        ""personalNumber"": ""198001011234"",
                        ""name"": ""Test Testsson"",
                        ""givenName"": ""Test"",
                        ""surname"": ""Testsson""
                    }},
                    ""device"": {{
                        ""ipAddress"": ""192.168.1.1"",
                        ""uhi"": ""unique-hardware-id""
                    }},
                    ""bankIdIssueDate"": ""2023-06-29"",
                    ""signature"": ""signature-data"",
                    ""ocspResponse"": ""ocsp-response""
                }}
            }}");

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
                @"{""errorCode"": ""invalidParameters"", ""details"": ""Order not found""}");

        var gateway = CreateBankIdGateway();

        // Act & Assert (should not throw, should ignore the exception)
        await gateway.Cancel(orderRef);
    }

    [Fact]
    public async Task AuthThenCollectScenario_ShouldFollowCompleteWorkflow_WhenAuthSuccessful()
    {
        // Arrange
        const string endUserIp = "192.168.1.10";
        const string orderRef = "scenario-auth-order-123";
        const string autoStartToken = "scenario-auto-start-token";

        var gateway = CreateBankIdGateway();

        // Mock auth response
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/auth")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""autoStartToken"": ""{autoStartToken}"",
                ""qrStartToken"": ""scenario-qr-start-token"",
                ""qrStartSecret"": ""scenario-qr-start-secret""
            }}");

        // Mock first collect response (pending)
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""pending"",
                ""hintCode"": ""outstandingTransaction""
            }}");

        // Act
        var authResult = await gateway.Auth(endUserIp);
        var collectResult = await gateway.Collect(authResult.OrderRef);

        // Assert
        Assert.Equal(orderRef, authResult.OrderRef);
        Assert.Equal(autoStartToken, authResult.AutoStartToken);
        Assert.Equal(orderRef, collectResult.OrderRef);
        Assert.Equal(BankIdStatus.Pending, collectResult.Status);
        Assert.Equal(BankIdHintCode.OutstandingTransaction, collectResult.HintCode);
        Assert.Null(collectResult.CompletionData);
    }

    [Fact]
    public async Task AuthThenCollectToCompleteScenario_ShouldReturnCompletionData_WhenUserCompletes()
    {
        // Arrange - Create fresh mock for this scenario
        var scenarioMock = new MockHttpMessageHandler();
        const string endUserIp = "192.168.1.11";
        const string orderRef = "scenario-auth-complete-456";
        const string autoStartToken = "scenario-complete-token";

        // Mock auth response
        scenarioMock
            .When(HttpMethod.Post, $"{BaseUri}/auth")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""autoStartToken"": ""{autoStartToken}"",
                ""qrStartToken"": ""complete-qr-start-token"",
                ""qrStartSecret"": ""complete-qr-start-secret""
            }}");

        // Mock first collect response (pending) 
        scenarioMock
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond(
                HttpStatusCode.OK,
                "application/json", 
                $@"{{
                    ""orderRef"": ""{orderRef}"",
                    ""status"": ""pending"",
                    ""hintCode"": ""userMrtd""
                }}")
            .Respond(
                HttpStatusCode.OK,
                "application/json",
                $@"{{
                    ""orderRef"": ""{orderRef}"",
                    ""status"": ""complete"",
                    ""completionData"": {{
                        ""user"": {{
                            ""personalNumber"": ""199001011234"",
                            ""name"": ""Jane Doe"",
                            ""givenName"": ""Jane"",
                            ""surname"": ""Doe""
                        }},
                        ""device"": {{
                            ""ipAddress"": ""192.168.1.11"",
                            ""uhi"": ""complete-hardware-id""
                        }},
                        ""bankIdIssueDate"": ""2024-01-15"",
                        ""stepUp"": {{
                            ""mrtd"": true
                        }},
                        ""signature"": ""complete-signature-data"",
                        ""ocspResponse"": ""complete-ocsp-response""
                    }}
                }}");

        // Create gateway with scenario-specific mock
        var gateway = _restClientFactory
            .CreateBuilder()
            .With(new JsonDeserializer(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            }) { Order = -100 })
            .With(new JsonSerializer(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            }) { Order = -100 })
            .With(scenarioMock.ToHttpClient())
            .BaseUri(new Uri(BaseUri))
            .Build<IBankIdClient>();

        var bankIdGateway = new BankIdGateway(gateway);

        // Act
        var authResult = await bankIdGateway.Auth(endUserIp);
        var firstCollectResult = await bankIdGateway.Collect(authResult.OrderRef);
        var secondCollectResult = await bankIdGateway.Collect(authResult.OrderRef);

        // Assert
        Assert.Equal(orderRef, authResult.OrderRef);
        Assert.Equal(autoStartToken, authResult.AutoStartToken);
        
        // First collect - pending
        Assert.Equal(orderRef, firstCollectResult.OrderRef);
        Assert.Equal(BankIdStatus.Pending, firstCollectResult.Status);
        Assert.Equal(BankIdHintCode.UserMrtd, firstCollectResult.HintCode);
        Assert.Null(firstCollectResult.CompletionData);
        
        // Second collect - complete
        Assert.Equal(orderRef, secondCollectResult.OrderRef);
        Assert.Equal(BankIdStatus.Complete, secondCollectResult.Status);
        Assert.Null(secondCollectResult.HintCode);
        Assert.NotNull(secondCollectResult.CompletionData);
        Assert.Equal("199001011234", secondCollectResult.CompletionData.User.PersonalNumber);
        Assert.Equal("Jane Doe", secondCollectResult.CompletionData.User.Name);
        Assert.Equal("Jane", secondCollectResult.CompletionData.User.GivenName);
        Assert.Equal("Doe", secondCollectResult.CompletionData.User.Surname);
        Assert.Equal("192.168.1.11", secondCollectResult.CompletionData.Device.IpAddress);
        Assert.Equal("complete-hardware-id", secondCollectResult.CompletionData.Device.UniqueHardwareId);
        Assert.Equal("2024-01-15", secondCollectResult.CompletionData.BankIdIssueDate);
        Assert.NotNull(secondCollectResult.CompletionData.StepUp);
        Assert.True(secondCollectResult.CompletionData.StepUp.Mrtd);
        Assert.Equal("complete-signature-data", secondCollectResult.CompletionData.Signature);
        Assert.Equal("complete-ocsp-response", secondCollectResult.CompletionData.OcspResponse);
    }

    [Fact]
    public async Task SignThenCollectToFailedScenario_ShouldReturnFailedStatus_WhenUserCancels()
    {
        // Arrange
        const string endUserIp = "192.168.1.12";
        const string orderRef = "scenario-sign-failed-789";
        const string autoStartToken = "scenario-failed-token";

        // Create two separate gateway instances to avoid mock conflicts
        var signGateway = CreateBankIdGateway();
        var collectGateway = CreateBankIdGateway();

        // Mock sign response
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/sign")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""autoStartToken"": ""{autoStartToken}"",
                ""qrStartToken"": ""failed-qr-start-token"",
                ""qrStartSecret"": ""failed-qr-start-secret""
            }}");

        // Mock collect response for failed transaction
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""failed"",
                ""hintCode"": ""userCallConfirm""
            }}");

        // Act
        var signResult = await signGateway.Sign(endUserIp);
        var collectResult = await collectGateway.Collect(signResult.OrderRef);

        // Assert
        Assert.Equal(orderRef, signResult.OrderRef);
        Assert.Equal(autoStartToken, signResult.AutoStartToken);
        
        // Collect - failed
        Assert.Equal(orderRef, collectResult.OrderRef);
        Assert.Equal(BankIdStatus.Failed, collectResult.Status);
        Assert.Equal(BankIdHintCode.UserCallConfirm, collectResult.HintCode);
        Assert.Null(collectResult.CompletionData);
    }

    [Fact]
    public async Task AuthThenCancelScenario_ShouldCancelPendingTransaction_WhenRequested()
    {
        // Arrange
        const string endUserIp = "192.168.1.13";
        const string orderRef = "scenario-auth-cancel-101";
        const string autoStartToken = "scenario-cancel-token";

        var gateway = CreateBankIdGateway();

        // Mock auth response
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/auth")
            .WithContent($"{{\"endUserIp\":\"{endUserIp}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""autoStartToken"": ""{autoStartToken}"",
                ""qrStartToken"": ""cancel-qr-start-token"",
                ""qrStartSecret"": ""cancel-qr-start-secret""
            }}");

        // Mock collect response (pending)
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/collect")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond("application/json", $@"{{
                ""orderRef"": ""{orderRef}"",
                ""status"": ""pending"",
                ""hintCode"": ""noClient""
            }}");

        // Mock cancel response
        _mockHttp
            .When(HttpMethod.Post, $"{BaseUri}/cancel")
            .WithContent($"{{\"orderRef\":\"{orderRef}\"}}")
            .Respond(HttpStatusCode.OK);

        // Act
        var authResult = await gateway.Auth(endUserIp);
        var collectResult = await gateway.Collect(authResult.OrderRef);
        // Act & Assert (should not throw)
        await gateway.Cancel(authResult.OrderRef);

        // Assert
        Assert.Equal(orderRef, authResult.OrderRef);
        Assert.Equal(autoStartToken, authResult.AutoStartToken);
        Assert.Equal(orderRef, collectResult.OrderRef);
        Assert.Equal(BankIdStatus.Pending, collectResult.Status);
        Assert.Equal(BankIdHintCode.NoClient, collectResult.HintCode);
        Assert.Null(collectResult.CompletionData);
    }
}