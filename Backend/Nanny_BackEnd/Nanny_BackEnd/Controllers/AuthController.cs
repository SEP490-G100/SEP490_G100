using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService auth, ILogger<AuthController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _auth.RegisterAsync(request);
            return Ok(new { success = true, message = "Đăng ký thành công. Vui lòng xác thực email.", data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var (response, isPending) = await _auth.LoginAsync(request);
            if (isPending)
                return Ok(new { success = false, needsVerification = true, email = request.Email,
                                message = "Email chưa được xác thực. Mã OTP mới đã được gửi đến email của bạn." });

            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected login error for email {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Fail("Dang nhap that bai do loi may chu. Vui long thu lai sau."));
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _auth.RefreshTokenAsync(request);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _auth.GoogleLoginAsync(request);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception ex)                 { return Unauthorized(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            await _auth.ChangePasswordAsync(GetCurrentUserId(), request);
            return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
        catch (InvalidOperationException ex)   { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var (success, message) = await _auth.ForgotPasswordAsync(request.Email);
        return success ? Ok(new { success = true, message }) : BadRequest(Fail(message));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _auth.ResetPasswordAsync(request);
            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công." });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("resend-verify")]
    public async Task<IActionResult> ResendVerifyEmail([FromBody] ResendVerifyRequest request)
    {
        try
        {
            await _auth.ResendVerifyEmailAsync(request.Email);
            return Ok(new { success = true, message = "Mã OTP mới đã được gửi đến email của bạn." });
        }
        catch (Exception ex)
        {
            return BadRequest(Fail($"Không thể gửi email: {ex.Message}"));
        }
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            await _auth.VerifyEmailAsync(request);
            return Ok(new { success = true, message = "Xác thực email thành công." });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("set-role")]
    public async Task<IActionResult> SetRole([FromBody] SetRoleRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _auth.SetRoleAsync(userId, request.Role);
            return Ok(new { success = true, message = "Cập nhật vai trò thành công.", data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return Ok(new { success = true, message = "Đăng xuất thành công." });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value
               ?? throw new UnauthorizedAccessException("Token không chứa user ID.");
        return Guid.Parse(sub);
    }

    private static object Fail(string message) => new { success = false, message };
}
