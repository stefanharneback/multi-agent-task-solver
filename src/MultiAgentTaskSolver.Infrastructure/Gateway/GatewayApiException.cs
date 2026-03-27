using System.Net;

namespace MultiAgentTaskSolver.Infrastructure.Gateway;

public sealed class GatewayApiException : Exception
{
    public GatewayApiException() { }

    public GatewayApiException(string message)
        : base(message) { }

    public GatewayApiException(string message, Exception innerException)
        : base(message, innerException) { }

    public GatewayApiException(HttpStatusCode statusCode, string message, string responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; } = string.Empty;
}
