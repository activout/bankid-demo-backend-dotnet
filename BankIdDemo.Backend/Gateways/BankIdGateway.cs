namespace BankIdDemo.Backend.Gateways;

internal class BankIdGateway : IBankIdGateway
{
    private readonly IBankIdClient _bankIdClient;

    public BankIdGateway(IBankIdClient bankIdClient)
    {
        _bankIdClient = bankIdClient;
    }

    public async Task<AuthResponse> Auth(string endUserIp)
    {
        var result = await _bankIdClient.Auth(new ApiAuthRequest(endUserIp));
        return new AuthResponse(result.OrderRef, result.AutoStartToken, result.QrStartToken, result.QrStartSecret);
    }
    
    public async Task<AuthResponse> Sign(string endUserIp)
    {
        var result = await _bankIdClient.Auth(new ApiAuthRequest(endUserIp));
        return new AuthResponse(result.OrderRef, result.AutoStartToken, result.QrStartToken, result.QrStartSecret);
    }
}