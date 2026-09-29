using System.Net;
using Activout.RestClient;
using BankIdDemo.Backend.Gateways;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RichardSzalay.MockHttp;

namespace BankIdDemo.Backend.Test;

// Runs the production client pipeline (BankIdClientFactory + BankIdApiHandler) against a mocked BankID API.
// Every request uses Expect + VerifyNoOutstandingExpectation, so a wrong URL or body fails the test.
public class BankIdGatewayTests
{
    private const string ApiUrl = "https://appapi2.test.bankid.com/rp/v6.0";
    private const string OrderRef = "131daac9-16c6-4618-beb0-365768f37288";
    private const string Json = "application/json";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly IBankIdGateway _gateway;

    public BankIdGatewayTests()
    {
        var restClientFactory = new ServiceCollection()
            .AddRestClient()
            .BuildServiceProvider()
            .GetRequiredService<IRestClientFactory>();
        var httpClient = new HttpClient(new BankIdApiHandler(NullLogger<BankIdApiHandler>.Instance)
        {
            InnerHandler = _mockHttp
        });
        var client = new BankIdClientFactory(httpClient, restClientFactory,
            Options.Create(new BankIdSettings(ApiUrl: ApiUrl))).Create();
        _gateway = new BankIdGateway(client);
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("sign")]
    public async Task AuthAndSign_PostEndUserIp_AndReturnOrderRefAndAutoStartToken(string endpoint)
    {
        _mockHttp.Expect(HttpMethod.Post, $"{ApiUrl}/{endpoint}")
            .WithContent("""{"endUserIp":"192.0.2.1"}""")
            .Respond(Json, $$"""
                {"orderRef":"{{OrderRef}}","autoStartToken":"7c40b5c9-fa74-49cf-b98c-bfe651f9a7c6",
                 "qrStartToken":"67df3917-fa0d-44e5-b327-edcc928297f8","qrStartSecret":"d28db9a7-4cde-429e-a983-359be676944c"}
                """);

        var result = endpoint == "auth"
            ? await _gateway.Auth("192.0.2.1")
            : await _gateway.Sign("192.0.2.1");

        Assert.Equal(new AuthResponse(OrderRef, "7c40b5c9-fa74-49cf-b98c-bfe651f9a7c6"), result);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Collect_Pending_MapsStatusAndHintCode()
    {
        ExpectCollect($$"""{"orderRef":"{{OrderRef}}","status":"pending","hintCode":"outstandingTransaction"}""");

        var result = await _gateway.Collect(OrderRef);

        Assert.Equal(new CollectResponse(OrderRef, BankIdStatus.Pending, BankIdHintCode.OutstandingTransaction, null),
            result);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Collect_Complete_MapsCompletionData()
    {
        ExpectCollect($$$"""
            {"orderRef":"{{{OrderRef}}}","status":"complete","completionData":{
              "user":{"personalNumber":"190000000000","name":"Karl Karlsson","givenName":"Karl","surname":"Karlsson"},
              "device":{"ipAddress":"192.0.2.1","uhi":"OZvYM9VvyiAmG7NA5jU5zRGcmkI="},
              "bankIdIssueDate":"2020-02-01","stepUp":{"mrtd":true},"signature":"c2ln","ocspResponse":"b2NzcA=="}}
            """);

        var result = await _gateway.Collect(OrderRef);

        Assert.Equal(BankIdStatus.Complete, result.Status);
        Assert.Null(result.HintCode);
        var data = result.CompletionData!;
        Assert.Equal(new User("190000000000", "Karl Karlsson", "Karl", "Karlsson"), data.User);
        Assert.Equal(new Device("192.0.2.1", "OZvYM9VvyiAmG7NA5jU5zRGcmkI="), data.Device);
        Assert.Equal(new StepUp(true), data.StepUp);
        Assert.Equal(("2020-02-01", "c2ln", "b2NzcA=="), (data.BankIdIssueDate, data.Signature, data.OcspResponse));
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Collect_FailedWithUnmappedHintCode_MapsToUnknown()
    {
        ExpectCollect($$"""{"orderRef":"{{OrderRef}}","status":"failed","hintCode":"userCancel"}""");

        var result = await _gateway.Collect(OrderRef);

        Assert.Equal(new CollectResponse(OrderRef, BankIdStatus.Failed, BankIdHintCode.Unknown, null), result);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Cancel_PostsOrderRef_AndIgnoresErrorResponse()
    {
        _mockHttp.Expect(HttpMethod.Post, $"{ApiUrl}/cancel")
            .WithContent($$"""{"orderRef":"{{OrderRef}}"}""")
            .Respond(HttpStatusCode.BadRequest, Json, """{"errorCode":"invalidParameters","details":"No such order"}""");

        await _gateway.Cancel(OrderRef);

        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Auth_ErrorResponse_Throws()
    {
        _mockHttp.Expect(HttpMethod.Post, $"{ApiUrl}/auth")
            .Respond(HttpStatusCode.BadRequest, Json, """{"errorCode":"alreadyInProgress","details":""}""");

        var exception = await Assert.ThrowsAsync<RestClientException>(() => _gateway.Auth("192.0.2.1"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    private void ExpectCollect(string response) =>
        _mockHttp.Expect(HttpMethod.Post, $"{ApiUrl}/collect")
            .WithContent($$"""{"orderRef":"{{OrderRef}}"}""")
            .Respond(Json, response);
}
