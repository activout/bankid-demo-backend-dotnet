using BankIdDemo.Backend.Gateways;
using Moq;
using Xunit;
using RestClientException = Activout.RestClient.RestClientException;
using System.Reflection;

namespace BankIdDemo.Backend.Test;

public class BankIdGatewayTests
{
    private readonly Mock<IBankIdClient> _mockBankIdClient;
    private readonly BankIdGateway _gateway;

    public BankIdGatewayTests()
    {
        _mockBankIdClient = new Mock<IBankIdClient>();
        _gateway = new BankIdGateway(_mockBankIdClient.Object);
    }

    [Fact]
    public async Task Auth_ShouldReturnAuthResponse_WhenApiCallSucceeds()
    {
        // Arrange
        var endUserIp = "192.168.1.1";
        var expectedOrderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var expectedAutoStartToken = "67df3917-fa0d-44e5-b327-edcc928297f8";

        var apiResponse = new ApiAuthResponse(
            expectedOrderRef,
            expectedAutoStartToken,
            "67df3917-fa0d-44e5-b327-edcc928297f8",
            "d28db9732dcef2e85f0bc74b2a2b1ad4"
        );

        _mockBankIdClient
            .Setup(x => x.Auth(It.Is<ApiAuthRequest>(req => req.EndUserIp == endUserIp)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Auth(endUserIp);

        // Assert
        Assert.Equal(expectedOrderRef, result.OrderRef);
        Assert.Equal(expectedAutoStartToken, result.AutoStartToken);
        _mockBankIdClient.Verify(x => x.Auth(It.Is<ApiAuthRequest>(req => req.EndUserIp == endUserIp)), Times.Once);
    }

    [Fact]
    public async Task Sign_ShouldReturnAuthResponse_WhenApiCallSucceeds()
    {
        // Arrange
        var endUserIp = "192.168.1.1";
        var expectedOrderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var expectedAutoStartToken = "67df3917-fa0d-44e5-b327-edcc928297f8";

        var apiResponse = new ApiAuthResponse(
            expectedOrderRef,
            expectedAutoStartToken,
            "67df3917-fa0d-44e5-b327-edcc928297f8",
            "d28db9732dcef2e85f0bc74b2a2b1ad4"
        );

        _mockBankIdClient
            .Setup(x => x.Sign(It.Is<ApiAuthRequest>(req => req.EndUserIp == endUserIp)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Sign(endUserIp);

        // Assert
        Assert.Equal(expectedOrderRef, result.OrderRef);
        Assert.Equal(expectedAutoStartToken, result.AutoStartToken);
        _mockBankIdClient.Verify(x => x.Sign(It.Is<ApiAuthRequest>(req => req.EndUserIp == endUserIp)), Times.Once);
    }

    [Fact]
    public async Task Collect_ShouldReturnCollectResponse_WhenApiCallSucceedsWithPendingStatus()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiResponse = new ApiCollectResponse(
            orderRef,
            "pending",
            "outstandingTransaction",
            null
        );

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Pending, result.Status);
        Assert.Equal(BankIdHintCode.OutstandingTransaction, result.HintCode);
        Assert.Null(result.CompletionData);
        _mockBankIdClient.Verify(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact]
    public async Task Collect_ShouldReturnCollectResponseWithCompletionData_WhenApiCallSucceedsWithCompleteStatus()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiCompletionData = new ApiCompletionData(
            new ApiUser("190000000000", "Karl Karlsson", "Karl", "Karlsson"),
            new ApiDevice("192.168.1.1", "Fz+wP+oHpbYYovV31Smo08fqF+JmhNgW2JPm7bv4SfbfHZNBn6FTiQfhP+o3u5w="),
            "2020-01-01",
            new ApiStepUp(true),
            "PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0iVVRGLTgiIHN0YW5kYWxvbmU9Im5vIj8+PFNpZ25hdHVyZSB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC8wOS94bWxkc2lnIyI+",
            "MIIHeAYJKoZIhvcNAQcCoIIHaTCCB2UCAQExDzANBglghkgBZQMEAgEFADCCBYkGCSqGSIb3DQEHAaCCBXoEggV2MIIFcjCCBW4wggRWoAMCAQICEHPkL1VqfOHfXcRw+xhhiVgwDQYJKoZIhvcNAQELBQAwSTELMAkGA1UEBhMCU0UxIDAeBgNVBAoMF1N3ZWRiYW5rIEFCIChwdWJsKQ=="
        );

        var apiResponse = new ApiCollectResponse(
            orderRef,
            "complete",
            null,
            apiCompletionData
        );

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Complete, result.Status);
        Assert.Null(result.HintCode);
        Assert.NotNull(result.CompletionData);
        
        var completionData = result.CompletionData!;
        Assert.Equal("190000000000", completionData.User.PersonalNumber);
        Assert.Equal("Karl Karlsson", completionData.User.Name);
        Assert.Equal("Karl", completionData.User.GivenName);
        Assert.Equal("Karlsson", completionData.User.Surname);
        Assert.Equal("192.168.1.1", completionData.Device.IpAddress);
        Assert.Equal("Fz+wP+oHpbYYovV31Smo08fqF+JmhNgW2JPm7bv4SfbfHZNBn6FTiQfhP+o3u5w=", completionData.Device.UniqueHardwareId);
        Assert.Equal("2020-01-01", completionData.BankIdIssueDate);
        Assert.NotNull(completionData.StepUp);
        Assert.True(completionData.StepUp!.Mrtd);
        _mockBankIdClient.Verify(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact]
    public async Task Collect_ShouldReturnUnknownStatus_WhenApiReturnsUnrecognizedStatus()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiResponse = new ApiCollectResponse(
            orderRef,
            "unknownStatus",
            "unknownHintCode",
            null
        );

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Unknown, result.Status);
        Assert.Equal(BankIdHintCode.Unknown, result.HintCode);
        _mockBankIdClient.Verify(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact]
    public async Task Collect_ShouldReturnNullHintCode_WhenApiReturnsNullHintCode()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiResponse = new ApiCollectResponse(
            orderRef,
            "pending",
            null,
            null
        );

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Pending, result.Status);
        Assert.Null(result.HintCode);
        _mockBankIdClient.Verify(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldCompleteSuccessfully_WhenApiCallSucceeds()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";

        _mockBankIdClient
            .Setup(x => x.Cancel(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await _gateway.Cancel(orderRef); // Should not throw
        _mockBankIdClient.Verify(x => x.Cancel(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldIgnoreRestClientException_WhenApiCallFails()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";

        // Create a RestClientException using reflection to find the proper constructor
        var exceptionType = typeof(RestClientException);
        var constructors = exceptionType.GetConstructors();
        RestClientException restClientException;
        
        if (constructors.Any(c => c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(string)))
        {
            restClientException = (RestClientException)Activator.CreateInstance(exceptionType, "Test error")!;
        }
        else if (constructors.Any(c => c.GetParameters().Length == 0))
        {
            restClientException = (RestClientException)Activator.CreateInstance(exceptionType)!;
        }
        else
        {
            // If we can't create one, use the first constructor with nulls
            var firstConstructor = constructors.First();
            var parameters = firstConstructor.GetParameters().Select(p => 
                p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            restClientException = (RestClientException)Activator.CreateInstance(exceptionType, parameters)!;
        }

        _mockBankIdClient
            .Setup(x => x.Cancel(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ThrowsAsync(restClientException);

        // Act & Assert
        await _gateway.Cancel(orderRef); // Should not throw despite API error
        _mockBankIdClient.Verify(x => x.Cancel(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)), Times.Once);
    }

    [Fact] 
    public async Task Collect_ShouldReturnCollectResponseWithNullStepUp_WhenApiReturnsNullStepUp()
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiCompletionData = new ApiCompletionData(
            new ApiUser("190000000000", "Karl Karlsson", "Karl", "Karlsson"),
            new ApiDevice("192.168.1.1", "Fz+wP+oHpbYYovV31Smo08fqF+JmhNgW2JPm7bv4SfbfHZNBn6FTiQfhP+o3u5w="),
            "2020-01-01",
            null, // StepUp is null
            "signature",
            "ocspResponse"
        );

        var apiResponse = new ApiCollectResponse(
            orderRef,
            "complete",
            null,
            apiCompletionData
        );

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(orderRef, result.OrderRef);
        Assert.Equal(BankIdStatus.Complete, result.Status);
        Assert.NotNull(result.CompletionData);
        Assert.Null(result.CompletionData!.StepUp);
    }

    [Theory]
    [InlineData("failed", BankIdStatus.Failed)]
    [InlineData("complete", BankIdStatus.Complete)]
    [InlineData("pending", BankIdStatus.Pending)]
    [InlineData("FAILED", BankIdStatus.Failed)] // Test case insensitivity
    [InlineData("Complete", BankIdStatus.Complete)]
    [InlineData("Pending", BankIdStatus.Pending)]
    public async Task Collect_ShouldParseStatusCorrectly_ForValidStatuses(string apiStatus, BankIdStatus expectedStatus)
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiResponse = new ApiCollectResponse(orderRef, apiStatus, null, null);

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
    }

    [Theory]
    [InlineData("noClient", BankIdHintCode.NoClient)]
    [InlineData("started", BankIdHintCode.Started)]
    [InlineData("userMrtd", BankIdHintCode.UserMrtd)]
    [InlineData("userCallConfirm", BankIdHintCode.UserCallConfirm)]
    [InlineData("NOCLIENT", BankIdHintCode.NoClient)] // Test case insensitivity
    [InlineData("Started", BankIdHintCode.Started)]
    public async Task Collect_ShouldParseHintCodeCorrectly_ForValidHintCodes(string apiHintCode, BankIdHintCode expectedHintCode)
    {
        // Arrange
        var orderRef = "131daac9-16c6-4618-beb0-365768f37288";
        var apiResponse = new ApiCollectResponse(orderRef, "pending", apiHintCode, null);

        _mockBankIdClient
            .Setup(x => x.Collect(It.Is<ApiOrderRefRequest>(req => req.OrderRef == orderRef)))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _gateway.Collect(orderRef);

        // Assert
        Assert.Equal(expectedHintCode, result.HintCode);
    }
}