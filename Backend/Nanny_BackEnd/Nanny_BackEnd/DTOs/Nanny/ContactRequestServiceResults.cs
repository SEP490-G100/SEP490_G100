namespace Nanny_BackEnd.DTOs.Nanny;

/// <summary>HTTP + JSON body cho response từ IContactRequestService (controller: StatusCode(Body) hoặc Ok).</summary>
public sealed class ContactRequestEndpointResult
{
    public int StatusCode { get; init; } = 200;
    public object Body { get; init; } = null!;
}
