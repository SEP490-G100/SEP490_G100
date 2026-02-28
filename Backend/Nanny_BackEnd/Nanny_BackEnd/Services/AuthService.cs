using Google.Apis.Auth;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Services;

public class AuthService
{
    private readonly UserRepository _userRepo;
    private readonly RefreshTokenRepository _tokenRepo;
    private readonly JwtService _jwt;
    private readonly OtpService _otp;
    private readonly EmailService _email;
    private readonly PasswordValidator _pwdValidator;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public AuthService(
        UserRepository userRepo,
        RefreshTokenRepository tokenRepo,
        JwtService jwt,
        OtpService otp,
        EmailService email,
        PasswordValidator pwdValidator,
        IConfiguration config,
        IHttpClientFactory httpFactory)
    {
        _userRepo = userRepo;
        _tokenRepo = tokenRepo;
        _jwt = jwt;
        _otp = otp;
        _email = email;
        _pwdValidator = pwdValidator;
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userRepo.FindByEmailAsync(request.Email);
        if (existing != null)
        {
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
        await _userRepo.AssignRoleAsync(user.Id, AuthConstants.DefaultRole);
        await _userRepo.SaveChangesAsync();

        await TrySendOtpEmailAsync(user.Email, user.Id, OtpPurpose.VerifyEmail);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.FindByEmailAsync(request.Email);
        if (user == null || user.AuthProvider == (int)AuthProvider.Google || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

        if (user.Status != (int)UserStatus.Active)
            throw new UnauthorizedAccessException("Tài khoản chưa được kích hoạt.");

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();

        return await BuildLoginResponseAsync(user);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var userId = _jwt.GetUserIdFromToken(request.AccessToken)
            ?? throw new UnauthorizedAccessException("Token không hợp lệ.");

        var stored = await _tokenRepo.FindByTokenAsync(request.RefreshToken);
        if (stored == null || stored.UserId != userId || stored.IsRevoked || stored.IsUsed || stored.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token không hợp lệ hoặc đã hết hạn.");

        var user = await _userRepo.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("Người dùng không tồn tại.");

        stored.IsUsed = true;
        stored.UpdatedAt = DateTime.UtcNow;

        var response = await BuildLoginResponseAsync(user);
        stored.ReplacedByToken = response.RefreshToken;
        await _tokenRepo.SaveChangesAsync();

        return response;
    }

    public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _config["Google:ClientId"] }
        });

        return await ProcessGoogleLoginAsync(payload);
    }

    public async Task<LoginResponse> GoogleLoginWithCodeAsync(GoogleAuthCodeRequest request)
    {
        var idToken = await ExchangeAuthCodeForIdTokenAsync(request);
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _config["Google:ClientId"] }
        });

        return await ProcessGoogleLoginAsync(payload);
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
            return (false, "Tài khoản này sử dụng Google để đăng nhập. Không thể đặt lại mật khẩu.");

        try
        {
            var code = await _otp.GenerateAsync(email, OtpPurpose.ForgotPassword, user.Id);
            await _email.SendOtpEmailAsync(email, code, "ForgotPassword");
            return (true, "Mã OTP đã được gửi đến email của bạn.");
        }
        catch (Exception ex)
        {
            return (false, $"Không thể gửi email: {ex.Message}");
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

        await TrySendOtpEmailAsync(user.Email, user.Id, OtpPurpose.VerifyEmail);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _tokenRepo.FindByTokenAsync(refreshToken);
        if (token == null) return;

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        await _tokenRepo.SaveChangesAsync();
    }

    // Private helpers 

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
            await _userRepo.AssignRoleAsync(user.Id, AuthConstants.DefaultRole);
            await _userRepo.SaveChangesAsync();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepo.SaveChangesAsync();

        return await BuildLoginResponseAsync(user);
    }

    private async Task<string> ExchangeAuthCodeForIdTokenAsync(GoogleAuthCodeRequest request)
    {
        using var http = _httpFactory.CreateClient();
        var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"]          = request.AuthCode,
                ["client_id"]     = _config["Google:ClientId"]!,
                ["client_secret"] = _config["Google:ClientSecret"]!,
                ["redirect_uri"]  = request.RedirectUri,
                ["grant_type"]    = "authorization_code"
            }));

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        if (data == null || !data.ContainsKey("id_token"))
            throw new InvalidOperationException("Không thể xác thực với Google.");

        return data["id_token"].ToString()!;
    }

    private async Task<LoginResponse> BuildLoginResponseAsync(User user)
    {
        var roles = await _userRepo.GetRolesAsync(user.Id);
        var (accessToken, expiresAt, jwtId) = _jwt.GenerateAccessToken(user, roles);
        var refreshToken = _jwt.GenerateRefreshToken();

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

        var authProvider = user.AuthProvider == (int)AuthProvider.Google ? "google" : "email";

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AvatarUrl = user.AvatarUrl,
                EmailConfirmed = user.EmailConfirmed,
                AuthProvider = authProvider,
                Roles = roles
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
