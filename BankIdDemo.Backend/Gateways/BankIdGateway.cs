using Activout.RestClient;

namespace BankIdDemo.Backend.Gateways;

internal class BankIdGateway(IBankIdClient bankIdClient) : IBankIdGateway
{
    public async Task<AuthResponse> Auth(string endUserIp)
    {
        var result = await bankIdClient.Auth(new ApiAuthRequest(endUserIp));
        return new AuthResponse(result.OrderRef, result.AutoStartToken);
    }

    public async Task<AuthResponse> Sign(string endUserIp)
    {
        var result = await bankIdClient.Sign(new ApiAuthRequest(endUserIp));
        return new AuthResponse(result.OrderRef, result.AutoStartToken);
    }

    public async Task<CollectResponse> Collect(string orderRef)
    {
        var result = await bankIdClient.Collect(new ApiOrderRefRequest(orderRef));
        return new CollectResponse(result.OrderRef, ParseStatus(result.Status), ParseHintCode(result.HintCode),
            ParseCompletionData(result.CompletionData));

        CompletionData? ParseCompletionData(ApiCompletionData? resultCompletionData) => resultCompletionData is null
                ? null
                : new CompletionData(
                    new User(resultCompletionData.User.PersonalNumber, resultCompletionData.User.Name,
                        resultCompletionData.User.GivenName, resultCompletionData.User.Surname),
                    new Device(resultCompletionData.Device.IpAddress, resultCompletionData.Device.Uhi),
                    resultCompletionData.BankIdIssueDate,
                    resultCompletionData.StepUp == null ?  null : new StepUp(resultCompletionData.StepUp.Mrtd),
                    resultCompletionData.Signature,
                    resultCompletionData.OcspResponse);

        BankIdHintCode? ParseHintCode(string? resultHintCode) => resultHintCode is null
            ? null
            : Enum.TryParse<BankIdHintCode>(resultHintCode, ignoreCase: true, out var hintCode)
                ? hintCode
                : BankIdHintCode.Unknown;

        BankIdStatus ParseStatus(string resultStatus) =>
            Enum.TryParse<BankIdStatus>(resultStatus, ignoreCase: true, out var status)
                ? status
                : BankIdStatus.Unknown;
    }

    public async Task Cancel(string orderRef)
    {
        try
        {
            await bankIdClient.Cancel(new ApiOrderRefRequest(orderRef));
        }
        catch (RestClientException)
        {
            // Ignore
        }
    }
}