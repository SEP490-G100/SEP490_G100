using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebSite.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct() ?? [];
        foreach (var role in roles)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
        }

        await base.OnConnectedAsync();
    }
}
