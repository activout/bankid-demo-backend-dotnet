using Activout.RestClient;

namespace BankIdDemo.Backend.Gateways;

// https://www.bankid.com/en/utvecklare/guider/teknisk-integrationsguide/graenssnittsbeskrivning/auth

[ErrorResponse(typeof(BankIdErrorResponse))]
public interface IBankIdClient
{
    [Post("auth")]
    Task<ApiAuthResponse> Auth(ApiAuthRequest request);

    [Post("sign")]
    Task<ApiAuthResponse> Sign(ApiAuthRequest request);

    [Post("collect")]
    Task<ApiCollectResponse> Collect(ApiCollectRequest request);
}

public record BankIdErrorResponse(string ErrorCode, string Details);

public record ApiCollectResponse(string OrderRef, string Status, string? HintCode, ApiCompletionData? CompletionData);

public record ApiCompletionData(
    ApiUser User,
    ApiDevice Device,
    string BankIdIssueDate,
    ApiStepUp StepUp,
    string Signature,
    string OcspResponse);

public record ApiUser(string PersonalNumber, string Name, string GivenName, string Surname);
public record ApiDevice(string IpAddress, string Uhi);
public record ApiStepUp(bool Mrtd);

public record ApiCollectRequest(string OrderRef);

public record ApiAuthRequirements(bool? PinCode);

public record ApiAuthRequest(
    string EndUserIp,
    ApiAuthRequirements? Requirement = null,
    string? UserVisibleData = null,
    string? UserNonVisibleData = null,
    string? UserVisibleDataFormat = null);

public record ApiAuthResponse(
    string OrderRef,
    string AutoStartToken,
    string QrStartToken,
    string QrStartSecret);