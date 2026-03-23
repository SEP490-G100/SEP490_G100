using Microsoft.Extensions.Configuration;

namespace Nanny_BackEnd.Validations;

public class PasswordValidator
{
    private readonly int _minLength;
    private readonly bool _requireDigit;
    private readonly bool _requireUppercase;
    private readonly bool _requireLowercase;
    private readonly bool _requireSpecialChar;

    public PasswordValidator(IConfiguration config)
    {
        var section = config.GetSection("PasswordPolicy");
        _minLength = section.GetValue("MinLength", 8);
        _requireDigit = section.GetValue("RequireDigit", true);
        _requireUppercase = section.GetValue("RequireUppercase", true);
        _requireLowercase = section.GetValue("RequireLowercase", true);
        _requireSpecialChar = section.GetValue("RequireSpecialChar", true);
    }

    public (bool IsValid, List<string> Errors) Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password) || password.Length < _minLength)
            errors.Add($"Mật khẩu phải có ít nhất {_minLength} ký tự.");

        if (_requireDigit && !password.Any(char.IsDigit))
            errors.Add("Mật khẩu phải chứa ít nhất 1 chữ số.");

        if (_requireUppercase && !password.Any(char.IsUpper))
            errors.Add("Mật khẩu phải chứa ít nhất 1 chữ hoa.");

        if (_requireLowercase && !password.Any(char.IsLower))
            errors.Add("Mật khẩu phải chứa ít nhất 1 chữ thường.");

        if (_requireSpecialChar && password.All(c => char.IsLetterOrDigit(c)))
            errors.Add("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");

        return (errors.Count == 0, errors);
    }
}
