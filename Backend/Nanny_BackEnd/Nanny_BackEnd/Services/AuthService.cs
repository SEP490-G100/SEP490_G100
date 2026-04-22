using Google.Apis.Auth;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _tokenRepo;
    private readonly IJwtService _jwt;
    private readonly IOtpService _otp;
    private readonly IEmailService _email;
    private readonly PasswordValidator _pwdValidator;
    private readonly IConfiguration _config;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository tokenRepo,
        IJwtService jwt,
        IOtpService otp,
        IEmailService email,
        PasswordValidator pwdValidator,
        IConfiguration config)
    {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _jwt = jwt;
        _otp = otp;
        _email = email;
        _pwdValidator = pwdValidator;
        _config = config;
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        var existing = await _userRepo.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            if (existing.Status == (int)UserStatus.Pending && existing.AuthProvider == (int)AuthProvider.Email)
            {
                await ReRegisterPendingUserAsync(existing);
                return;
            }

            var msg = existing.AuthProvider == (int)AuthProvider.Google
                ? "Email này đã đăng ký bằng Google. Vui lòng đăng nhập bằng Google."
                : "Email đã được đăng ký.";
            throw new InvalidOperationException(msg);
        }

        ValidatePasswordOrThrow(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            AuthProvider = (int)AuthProvider.Email,
            Status = (int)UserStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _userRepo.Add(user);

        if (!string.IsNullOrWhiteSpace(request.Role))
            await _userRepo.AssignRoleAsync(user.Id, request.Role.Trim());

        await _userRepo.SaveChangesAsync();
        await TrySendOtpEmailAsync(user.Email, user.Id, OtpPurpose.VerifyEmail);
    }

    public async Task<(LoginResponse? Response, bool IsPending)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.FindByEmailAsync(request.Email);
        if (user == null || user.AuthProvider == (int)AuthProvider.Google || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (user.Status == (int)UserStatus.Pending)
        {
            await TrySendOtpEmailAsync(user.Email, user.Id, OtpPurpose.VerifyEmail);
            return (null, true);
        }

        if (user.Status == (int)UserStatus.Banned)
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ.");

        if (user.Status == (int)UserStatus.Inactive)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        if (user.Status != (int)UserStatus.Active)
            throw new UnauthorizedAccessException("Tài khoản không thể đăng nhập.");

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();

        return (await BuildLoginResponseAsync(user, revokeExisting: true), false);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var (userId, jwtId) = _jwt.GetTokenClaims(request.AccessToken);
        if (userId == null || jwtId == null)
            throw new UnauthorizedAccessException("Token không hợp lệ.");

        var stored = await _tokenRepo.FindByTokenAsync(request.RefreshToken);
        if (stored == null
            || stored.UserId != userId
            || stored.JwtId  != jwtId
            || stored.IsRevoked
            || stored.IsUsed
            || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");

        var user = await _userRepo.FindByIdAsync(userId.Value)
            ?? throw new UnauthorizedAccessException("Người dùng không tồn tại.");

        if (user.Status == (int)UserStatus.Banned)
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");

        if (user.Status == (int)UserStatus.Inactive)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        stored.IsUsed = true;
        stored.UpdatedAt = DateTime.UtcNow;

        var response = await BuildLoginResponseAsync(user);
        await _tokenRepo.SaveChangesAsync();

        return response;
    }

    public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        var payload = await ValidateGoogleTokenAsync(request.IdToken);
        return await ProcessGoogleLoginAsync(payload);
    }

    protected virtual async Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string idToken)
    {
        return await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _config["Google:ClientId"] }
        });
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        if (user.AuthProvider == (int)AuthProvider.Google)
            throw new InvalidOperationException("Tài khoản Google không sử dụng mật khẩu.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        ValidatePasswordOrThrow(request.NewPassword);
        await UpdatePasswordAsync(user, request.NewPassword);
    }

    public async Task<(bool success, string message)> ForgotPasswordAsync(string email)
    {
        var user = await _userRepo.FindByEmailAsync(email);
        if (user == null)
            return (true, "Nếu email tồn tại, mã OTP đã được gửi.");

        if (user.AuthProvider == (int)AuthProvider.Google)
            return (true, "Nếu email tồn tại, mã OTP đã được gửi.");

        try
        {
            var code = await _otp.GenerateAsync(email, OtpPurpose.ForgotPassword, user.Id);
            await _email.SendOtpEmailAsync(email, code, "ForgotPassword");
            return (true, "Mã OTP đã được gửi đến email của bạn.");
        }
        catch
        {
            return (false, "Không thể gửi email lúc này. Vui lòng thử lại sau.");
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var otp = await _otp.ValidateAsync(request.Email, request.OtpCode, OtpPurpose.ForgotPassword)
            ?? throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");

        var user = await _userRepo.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        if (user.AuthProvider == (int)AuthProvider.Google)
            throw new InvalidOperationException("Tài khoản Google không sử dụng mật khẩu.");

        ValidatePasswordOrThrow(request.NewPassword);
        await UpdatePasswordAsync(user, request.NewPassword);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request)
    {
        var otp = await _otp.ValidateAsync(request.Email, request.OtpCode, OtpPurpose.VerifyEmail)
            ?? throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");

        var user = await _userRepo.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        user.EmailConfirmed = true;
        user.Status = (int)UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();
    }

    public async Task ResendVerifyEmailAsync(string email)
    {
        var user = await _userRepo.FindByEmailAsync(email);
        if (user == null || user.EmailConfirmed || user.AuthProvider == (int)AuthProvider.Google)
            return;

        var code = await _otp.GenerateAsync(user.Email, OtpPurpose.VerifyEmail, user.Id);
        await _email.SendOtpEmailAsync(user.Email, code, "VerifyEmail");
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _tokenRepo.FindByTokenAsync(refreshToken);
        if (token == null) return;

        await _tokenRepo.RevokeAllForUserAsync(token.UserId);
        await _tokenRepo.SaveChangesAsync();
    }

    public async Task<LoginResponse> SetRoleAsync(Guid userId, string role)
    {
        var validRoles = new[] { "Parent", "Nanny" };
        if (!validRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vai trò không hợp lệ. Chỉ chấp nhận Parent hoặc Nanny.");

        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        // Xóa tất cả role cũ rồi gán role mới
        await _userRepo.RemoveAllRolesAsync(userId);
        await _userRepo.AssignRoleAsync(userId, role);
        await _userRepo.SaveChangesAsync();

        // Trả về token mới chứa role đã cập nhật
        return await BuildLoginResponseAsync(user);
    }

    private async Task ReRegisterPendingUserAsync(User existing)
    {
        // Không overwrite bất kỳ dữ liệu nào — chỉ gửi lại OTP xác thực.
        // Việc thay đổi thông tin chỉ được phép sau khi email đã được xác thực.
        await TrySendOtpEmailAsync(existing.Email, existing.Id, OtpPurpose.VerifyEmail);
    }

    private async Task<LoginResponse> ProcessGoogleLoginAsync(GoogleJsonWebSignature.Payload payload)
    {
        var user = await _userRepo.FindByEmailAsync(payload.Email);

        if (user?.AuthProvider == (int)AuthProvider.Email)
            throw new InvalidOperationException("Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng email.");

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = payload.Email,
                FirstName = payload.GivenName ?? "",
                LastName = payload.FamilyName ?? "",
                AvatarUrl = payload.Picture,
                GoogleId = payload.Subject,
                AuthProvider = (int)AuthProvider.Google,
                EmailConfirmed = true,
                Status = (int)UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            _userRepo.Add(user);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();

        return await BuildLoginResponseAsync(user, revokeExisting: true);
    }

    private async Task<LoginResponse> BuildLoginResponseAsync(User user, bool revokeExisting = false)
    {
        if (revokeExisting)
            await _tokenRepo.RevokeAllForUserAsync(user.Id);

        var roles = await _userRepo.GetRolesAsync(user.Id);
        var (accessToken, expiresAt, jwtId) = _jwt.GenerateAccessToken(user, roles);
        var refreshToken = _jwt.GenerateRefreshToken();
        var firstName = user.FirstName ?? string.Empty;
        var lastName = user.LastName ?? string.Empty;
        var email = user.Email ?? string.Empty;

        _tokenRepo.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            JwtId = jwtId,
            ExpiresAt = DateTime.UtcNow.AddDays(AuthConstants.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });
        await _tokenRepo.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                AvatarUrl = user.AvatarUrl,
                EmailConfirmed = user.EmailConfirmed,
                AuthProvider = user.AuthProvider == (int)AuthProvider.Google ? "google" : "email",
                Roles = roles.Where(static r => !string.IsNullOrWhiteSpace(r)).ToList()
            }
        };
    }

    private void ValidatePasswordOrThrow(string password)
    {
        var (isValid, errors) = _pwdValidator.Validate(password);
        if (!isValid)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    private async Task UpdatePasswordAsync(User user, string newPassword)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();
    }

    private async Task TrySendOtpEmailAsync(string email, Guid userId, OtpPurpose purpose)
    {
        var code = await _otp.GenerateAsync(email, purpose, userId);
        var purposeKey = purpose == OtpPurpose.VerifyEmail ? "VerifyEmail" : "ForgotPassword";
        try { await _email.SendOtpEmailAsync(email, code, purposeKey); }
        catch { /* Silent — email failure should not block the main flow */ }
    }
}
