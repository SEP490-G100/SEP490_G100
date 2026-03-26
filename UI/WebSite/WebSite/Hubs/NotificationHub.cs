using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebSite.Hubs;

[Authorize]
public class NotificationHub : Hub
{
}
