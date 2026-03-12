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

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _auth.register(request);
            return Ok(new { success = true, message = "Đăng ký thành công. Vui lòng xác thực email.", data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, Fail($"Lỗi hệ thống: {detail}"));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _auth.login(request);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _auth.refreshToken(request);
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _auth.googleLogin(request);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("google-callback")]
    public async Task<IActionResult> GoogleCallback([FromBody] GoogleAuthCodeRequest request)
    {
        try
        {
            var result = await _auth.googleLoginWithCode(request);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            await _auth.changePassword(GetCurrentUserId(), request);
            return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(Fail(ex.Message)); }
        catch (InvalidOperationException ex)   { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var (success, message) = await _auth.forgotPassword(request.Email);
        return success ? Ok(new { success = true, message }) : BadRequest(Fail(message));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _auth.resetPassword(request);
            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công." });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("resend-verify")]
    public async Task<IActionResult> ResendVerifyEmail([FromBody] ResendVerifyRequest request)
    {
        await _auth.resendVerifyEmail(request.Email);
        return Ok(new { success = true, message = "Nếu email tồn tại và chưa xác thực, mã OTP mới đã được gửi." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            await _auth.verifyEmail(request);
            return Ok(new { success = true, message = "Xác thực email thành công." });
        }
        catch (InvalidOperationException ex) { return BadRequest(Fail(ex.Message)); }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _auth.logout(request.RefreshToken);
        return Ok(new { success = true, message = "Đăng xuất thành công." });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private static object Fail(string message) => new { success = false, message };
}
