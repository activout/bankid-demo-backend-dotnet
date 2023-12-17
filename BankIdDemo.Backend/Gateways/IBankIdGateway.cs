namespace BankIdDemo.Backend.Gateways;

public enum BankIdStatus
{
    Pending,
    Failed,
    Complete
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
    string AutoStartToken,
    string QrStartToken,
    string QrStartSecret);

public interface IBankIdGateway
{
    Task<AuthResponse> Auth(string endUserIp);
    Task<AuthResponse> Sign(string endUserIp);
    
}