using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace WebSite.Infrastructure;

public sealed class VietnameseValidationMetadataProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (HasCustomMessage(attribute))
            {
                continue;
            }

            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "{0} là bắt buộc.",
                StringLengthAttribute stringLength when stringLength.MinimumLength > 0 => "{0} phải có từ {2} đến {1} ký tự.",
                StringLengthAttribute => "{0} không được vượt quá {1} ký tự.",
                MinLengthAttribute => "{0} phải có ít nhất {1} ký tự.",
                MaxLengthAttribute => "{0} không được vượt quá {1} ký tự.",
                RangeAttribute => "{0} phải nằm trong khoảng từ {1} đến {2}.",
                EmailAddressAttribute => "{0} không đúng định dạng email.",
                PhoneAttribute => "{0} không đúng định dạng số điện thoại.",
                UrlAttribute => "{0} không đúng định dạng liên kết.",
                CompareAttribute => "{0} không khớp.",
                RegularExpressionAttribute => "{0} không đúng định dạng.",
                EnumDataTypeAttribute => "{0} không hợp lệ.",
                CreditCardAttribute => "{0} không đúng định dạng thẻ.",
                FileExtensionsAttribute => "{0} có định dạng tệp không hợp lệ.",
                _ => attribute.ErrorMessage
            };
        }
    }

    private static bool HasCustomMessage(ValidationAttribute attribute) =>
        !string.IsNullOrWhiteSpace(attribute.ErrorMessage) ||
        !string.IsNullOrWhiteSpace(attribute.ErrorMessageResourceName);
}
