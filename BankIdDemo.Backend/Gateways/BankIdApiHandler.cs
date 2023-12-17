namespace BankIdDemo.Backend.Gateways;

public class BankIdApiHandler(ILogger<BankIdApiHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        PreventUnsupportedMediaTypeError(request);
        await LogRequestContent(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task LogRequestContent(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            await request.Content.LoadIntoBufferAsync();
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("BankIdApiHandler: {Method} {Uri} {Content}",
                request.Method, request.RequestUri, content);
        }
    }

    private static void PreventUnsupportedMediaTypeError(HttpRequestMessage request)
    {
        if (request.Content?.Headers.ContentType != null)
        {
            request.Content.Headers.ContentType.CharSet = null;
        }
    }
}