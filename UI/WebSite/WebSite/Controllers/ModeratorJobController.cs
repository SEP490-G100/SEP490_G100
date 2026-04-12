using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebSite.Controllers
{
    [Authorize(Roles = "Moderator")]
    [Route("Moderator")]

    public class ModeratorJobController : ControllerBase
    {
    }
}
