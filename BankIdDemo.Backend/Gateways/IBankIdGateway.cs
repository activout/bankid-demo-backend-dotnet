namespace BankIdDemo.Backend.Gateways;

public enum BankIdStatus
{
    Unknown,
    Pending,
    Failed,
    Complete,
}

public enum BankIdHintCode
{
    Unknown,
    OutstandingTransaction,
    NoClient,
    Started,
    UserMrtd,
    UserCallConfirm
}

public record AuthResponse(
    string OrderRef,
    string AutoStartToken);

public record User(string PersonalNumber, string Name, string GivenName, string Surname);

public record Device(string IpAddress, string UniqueHardwareId);

public record StepUp(bool Mrtd);

public record CompletionData(
    User User,
    Device Device,
    string BankIdIssueDate,
    StepUp StepUp,
    string Signature,
    string OcspResponse);


public record CollectResponse(
    string OrderRef,
    BankIdStatus Status,
    BankIdHintCode? HintCode,
    CompletionData? CompletionData);

public interface IBankIdGateway
{
    Task<AuthResponse> Auth(string endUserIp);
    Task<AuthResponse> Sign(string endUserIp);

    Task<CollectResponse> Collect(string orderRef);
    Task Cancel(string orderRef);
}