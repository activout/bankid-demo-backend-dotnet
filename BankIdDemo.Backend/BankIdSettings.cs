namespace BankIdDemo.Backend;

public record BankIdSettings(string ApiUrl = "", string SslCertificatePath = "", string SslCertificatePassword = "")
{
    public const string Key = "BankId";

    public BankIdSettings() : this("", "", "")
    {
    }
}