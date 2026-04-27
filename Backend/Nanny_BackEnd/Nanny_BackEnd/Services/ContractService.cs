using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Mail;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class ContractService : IContractService
{
    private readonly IContractRepository _repo;
    private const string FieldValuesMarker = "\n[[NANNYMATCH_FIELD_VALUES]]:";

    public ContractService(IContractRepository repo) => _repo = repo;

    public async Task<ContractListResponseDto> GetMyContractsAsync(Guid userId)
    {
        var contracts = await _repo.GetContractsByUserIdAsync(userId);
        var result = new ContractListResponseDto();

        var displayContracts = contracts
            .GroupBy(c => c.HiringRecordId)
            .Select(group =>
            {
                var display = group
                    .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                    .First();
                return new
                {
                    Contract = display,
                    SortAt = display.UpdatedAt ?? display.CreatedAt
                };
            })
            .OrderByDescending(x => x.SortAt)
            .ToList();

        foreach (var entry in displayContracts)
        {
            var item = MapToListItem(entry.Contract);

            switch (entry.Contract.HiringRecord?.Status)
            {
                case (int)HiringRecordStatus.Active:
                    result.Active.Add(item);
                    break;
                case (int)HiringRecordStatus.Pending:
                    result.Pending.Add(item);
                    break;
                default:
                    result.History.Add(item);
                    break;
            }
        }

        return result;
    }

    public async Task<List<ContractTemplateOptionDto>> GetActiveContractTemplatesAsync()
    {
        var templates = await _repo.GetActiveContractTemplatesAsync();
        return templates.Select(t => new ContractTemplateOptionDto
        {
            Id = t.Id,
            Name = t.Name,
            Version = t.Version
        }).ToList();
    }

    public async Task<ContractTemplatePreviewDto> GetContractTemplatePreviewAsync(Guid templateId)
    {
        var template = await _repo.GetActiveContractTemplateByIdAsync(templateId)
            ?? throw new KeyNotFoundException("Không tìm thấy mẫu hợp đồng.");

        return new ContractTemplatePreviewDto
        {
            Id = template.Id,
            Name = template.Name,
            Version = template.Version,
            Content = template.Content ?? string.Empty
        };
    }

    public async Task<ContractDetailDto> GetContractDetailAsync(Guid userId, Guid? contractId, Guid? hiringRecordId)
    {
        Contract? contract;
        if (contractId.HasValue && contractId.Value != Guid.Empty)
        {
            contract = await _repo.GetContractDetailAsync(contractId.Value);
        }
        else if (hiringRecordId.HasValue && hiringRecordId.Value != Guid.Empty)
        {
            contract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId.Value);
        }
        else
        {
            throw new InvalidOperationException("Thiếu thông tin contractId hoặc hiringRecordId.");
        }

        if (contract == null)
            throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var currentUserRole = ResolveCurrentUserRole(contract, userId);
        return MapToDetail(contract, currentUserRole);
    }

    public async Task<ContractDetailDto> ParentConfirmInfoAsync(Guid contractId, Guid userId, ContractParentFillRequestDto request)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var role = ResolveCurrentUserRole(contract, userId);
        if (!string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ bố mẹ mới được phép thực hiện thao tác này.");

        if (contract.Status == 3)
            throw new InvalidOperationException("Hợp đồng đã được xác nhận hoàn tất, không thể chỉnh sửa.");

        var (_, currentValues) = ParseStoredContractContent(contract.ContractContent);
        ApplyParentDefaults(contract, request, currentValues);
        ValidateParentFillRequest(request);

        var values = BuildParentValues(request);
        MergeFieldValues(currentValues, values);
        contract.ContractContent = RenderTemplateWithValues(ContractTemplateDefaults.DefaultContent, currentValues);
        if (contract.Status == 0)
            contract.Status = 1;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = userId;

        if (contract.HiringRecord != null)
        {
            contract.HiringRecord.ParentConfirmedAt = DateTime.UtcNow;
            contract.HiringRecord.UpdatedAt = DateTime.UtcNow;
            contract.HiringRecord.UpdatedBy = userId;
        }

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, role);
    }

    public async Task<ContractDetailDto> NannyConfirmInfoAsync(Guid contractId, Guid userId, ContractNannyFillRequestDto request)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var role = ResolveCurrentUserRole(contract, userId);
        if (!string.Equals(role, "Nanny", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ bảo mẫu mới được phép thực hiện thao tác này.");

        if (contract.Status == 0)
            throw new InvalidOperationException("Bố mẹ chưa xác nhận thông tin nên bảo mẫu chưa thể điền.");
        if (contract.Status == 3)
            throw new InvalidOperationException("Hợp đồng đã được xác nhận hoàn tất, không thể chỉnh sửa.");

        var (_, currentValues) = ParseStoredContractContent(contract.ContractContent);
        ApplyNannyDefaults(contract, request, currentValues);
        ValidateNannyFillRequest(request);

        var values = BuildNannyValues(request);
        MergeFieldValues(currentValues, values);
        contract.ContractContent = RenderTemplateWithValues(ContractTemplateDefaults.DefaultContent, currentValues);
        contract.Status = 2;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = userId;

        if (contract.HiringRecord != null)
        {
            contract.HiringRecord.NannyConfirmedAt = DateTime.UtcNow;
            contract.HiringRecord.UpdatedAt = DateTime.UtcNow;
            contract.HiringRecord.UpdatedBy = userId;
        }

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, role);
    }

    public async Task<ContractDetailDto> ParentFinalConfirmAsync(Guid contractId, Guid userId)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var role = ResolveCurrentUserRole(contract, userId);
        if (!string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ bố mẹ mới được phép hoàn tất hợp đồng.");

        if (contract.Status != 2)
            throw new InvalidOperationException("Hợp đồng chỉ được hoàn tất sau khi bảo mẫu đã xác nhận thông tin.");

        contract.Status = 3;
        contract.SignedByParent = false;
        contract.SignedByNanny = false;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = userId;

        if (contract.HiringRecord != null)
        {
            contract.HiringRecord.Status = (int)HiringRecordStatus.Active;
            contract.HiringRecord.UpdatedAt = DateTime.UtcNow;
            contract.HiringRecord.UpdatedBy = userId;
        }

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, role);
    }

    public async Task<ContractListItemDto> SaveContractStoragePdfAsync(
        Guid contractId,
        Guid userId,
        SaveContractStoragePdfRequestDto request)
    {
        if (request == null)
            throw new InvalidOperationException("Vui lòng cung cấp thông tin file hợp đồng.");

        if (string.IsNullOrWhiteSpace(request.PdfUrl))
            throw new InvalidOperationException("Vui lòng cung cấp đường dẫn file hợp đồng.");

        if (!Uri.TryCreate(request.PdfUrl.Trim(), UriKind.Absolute, out var pdfUri) ||
            !(string.Equals(pdfUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(pdfUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Đường dẫn file hợp đồng không hợp lệ.");
        }

        if (!string.Equals(Path.GetExtension(pdfUri.AbsolutePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ chấp nhận file hợp đồng định dạng PDF.");
        }

        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        var currentRole = ResolveCurrentUserRole(contract, userId);
        if (!string.Equals(currentRole, "Parent", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chỉ bố mẹ mới được phép tải lên file hợp đồng.");

        if (contract.Status != 3)
            throw new InvalidOperationException("Chỉ được tải lên file khi hợp đồng đã xác nhận hoàn tất.");

        var nowUtc = DateTime.UtcNow;
        var normalizedPdfUrl = request.PdfUrl.Trim();

        contract.PdfUrl = normalizedPdfUrl;
        if (string.IsNullOrWhiteSpace(contract.ContractContent))
            contract.ContractContent = "Hop dong da duoc luu duoi dang PDF.";
        contract.CreatedBy ??= userId;
        contract.UpdatedAt = nowUtc;
        contract.UpdatedBy = userId;

        await _repo.SaveChangesAsync();
        return MapToListItem(contract);
    }

    public async Task<(byte[] Content, string FileName)> DownloadContractPdfAsync(Guid contractId, Guid userId)
    {
        var contract = await _repo.GetContractDetailAsync(contractId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        ResolveCurrentUserRole(contract, userId);

        var parentName = GetDisplayName(contract.HiringRecord?.ParentProfile?.User);
        var nannyName = GetDisplayName(contract.HiringRecord?.NannyProfile?.User);
        var generatedText = contract.ContractContent ?? string.Empty;

        if (generatedText.Contains(FieldValuesMarker, StringComparison.Ordinal))
        {
            var (_, fieldValues) = ParseStoredContractContent(generatedText);
            EnsureFieldValue(fieldValues, "ParentName", parentName);
            EnsureFieldValue(fieldValues, "NannyName", nannyName);
            EnsureFieldValue(fieldValues, "ParentPhone", contract.HiringRecord?.ParentProfile?.User?.PhoneNumber);
            EnsureFieldValue(fieldValues, "ParentEmail", contract.HiringRecord?.ParentProfile?.User?.Email);
            EnsureFieldValue(fieldValues, "NannyPhone", contract.HiringRecord?.NannyProfile?.User?.PhoneNumber);
            EnsureFieldValue(fieldValues, "NannyEmail", contract.HiringRecord?.NannyProfile?.User?.Email);
            EnsureFieldValue(fieldValues, "StartDate", contract.HiringRecord?.StartDate.ToString("dd/MM/yyyy"));
            EnsureFieldValue(fieldValues, "EndDate", contract.HiringRecord?.EndDate?.ToString("dd/MM/yyyy"));
            EnsureFieldValue(fieldValues, "ContractDurationMonths", CalculateContractDurationMonths(contract.HiringRecord?.StartDate, contract.HiringRecord?.EndDate));
            EnsureFieldValue(fieldValues, "JobDescription", contract.HiringRecord?.JobApplication?.JobPosting?.Description);
            EnsureFieldValue(fieldValues, "WorkAddress", contract.HiringRecord?.JobApplication?.JobPosting?.Location);
            generatedText = RenderTemplateWithValues(ContractTemplateDefaults.DefaultContent, fieldValues);
        }
        if (string.IsNullOrWhiteSpace(generatedText))
            generatedText = "Hợp đồng.";
        var generatedPdfBytes = BuildSimplePdf(generatedText);
        var generatedFileName = $"HopDong_{SanitizeFileName(parentName)}_{SanitizeFileName(nannyName)}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        return (generatedPdfBytes, generatedFileName);

#if false
        if (string.IsNullOrWhiteSpace(contract.PdfUrl)) // temp
            throw new InvalidOperationException("Hợp đồng chưa có file PDF được tải lên.");

        if (!Uri.TryCreate(contract.PdfUrl, UriKind.Absolute, out var pdfUri))
            throw new InvalidOperationException("Đường dẫn file PDF hợp đồng không hợp lệ.");

        byte[] pdfBytes;
        using (var http = new HttpClient())
        {
            try
            {
                pdfBytes = await http.GetByteArrayAsync(pdfUri);
            }
            catch
            {
                throw new InvalidOperationException("Không thể tải file PDF hợp đồng từ kho lưu trữ.");
            }
        }

        var fileName = $"HopDong_{SanitizeFileName(parentName)}_{SanitizeFileName(nannyName)}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        return (pdfBytes, fileName);
#endif
    }

    private static ContractDetailDto MapToDetail(Contract contract, string currentUserRole)
    {
        var (_, fieldValues) = ParseStoredContractContent(contract.ContractContent);
        var status = contract.Status;
        var isParent = string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase);
        var isNanny = string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase);

        var canParentConfirmInfo = isParent && status is >= 0 and < 3;
        var canNannyConfirmInfo = isNanny && status is >= 1 and < 3;
        var canParentFinalConfirm = string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) && status == 2;
        var isReadOnly = status == 3 || (isNanny && status == 0);
        var parentUser = contract.HiringRecord?.ParentProfile?.User;
        var nannyUser = contract.HiringRecord?.NannyProfile?.User;
        var hiring = contract.HiringRecord;
        EnsureFieldValue(fieldValues, "ParentName", GetDisplayName(parentUser));
        EnsureFieldValue(fieldValues, "ParentPhone", parentUser?.PhoneNumber);
        EnsureFieldValue(fieldValues, "ParentEmail", parentUser?.Email);
        EnsureFieldValue(fieldValues, "NannyName", GetDisplayName(nannyUser));
        EnsureFieldValue(fieldValues, "NannyPhone", nannyUser?.PhoneNumber);
        EnsureFieldValue(fieldValues, "NannyEmail", nannyUser?.Email);
        EnsureFieldValue(fieldValues, "StartDate", hiring?.StartDate.ToString("dd/MM/yyyy"));
        EnsureFieldValue(fieldValues, "EndDate", hiring?.EndDate?.ToString("dd/MM/yyyy"));
        EnsureFieldValue(fieldValues, "ContractDurationMonths", CalculateContractDurationMonths(hiring?.StartDate, hiring?.EndDate));
        EnsureFieldValue(fieldValues, "JobDescription", hiring?.JobApplication?.JobPosting?.Description);
        EnsureFieldValue(fieldValues, "WorkAddress", hiring?.JobApplication?.JobPosting?.Location);

        return new ContractDetailDto
        {
            ContractId = contract.Id,
            HiringRecordId = contract.HiringRecordId,
            ContractTemplateId = contract.ContractTemplateId,
            ContractContent = ContractTemplateDefaults.DefaultContent,
            FieldValues = fieldValues,
            ContractStatus = contract.Status,
            SignedByParent = contract.SignedByParent,
            SignedByNanny = contract.SignedByNanny,
            StartDate = contract.HiringRecord?.StartDate ?? DateOnly.MinValue,
            EndDate = contract.HiringRecord?.EndDate,
            ParentName = GetDisplayName(parentUser),
            ParentPhone = parentUser?.PhoneNumber ?? string.Empty,
            ParentEmail = parentUser?.Email ?? string.Empty,
            NannyName = GetDisplayName(nannyUser),
            NannyPhone = nannyUser?.PhoneNumber ?? string.Empty,
            NannyEmail = nannyUser?.Email ?? string.Empty,
            CurrentUserRole = currentUserRole,
            CanParentConfirmInfo = canParentConfirmInfo,
            CanNannyConfirmInfo = canNannyConfirmInfo,
            CanParentFinalConfirm = canParentFinalConfirm,
            IsReadOnly = isReadOnly
        };
    }

    private static Dictionary<string, string?> BuildParentValues(ContractParentFillRequestDto request) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ParentName"] = request.ParentName,
            ["ParentDOB"] = request.ParentDob,
            ["ParentIdentityNumber"] = request.ParentIdentityNumber,
            ["ParentIdentityIssueDate"] = request.ParentIdentityIssueDate,
            ["ParentIdentityIssuePlace"] = request.ParentIdentityIssuePlace,
            ["ParentPermanentAddress"] = request.ParentPermanentAddress,
            ["ParentCurrentAddress"] = request.ParentCurrentAddress,
            ["ParentPhone"] = request.ParentPhone,
            ["ParentEmail"] = request.ParentEmail,
            ["ContractDurationMonths"] = request.ContractDurationMonths,
            ["ProbationStartDate"] = request.ProbationStartDate,
            ["ProbationEndDate"] = request.ProbationEndDate,
            ["WorkAddress"] = request.WorkAddress,
            ["SalaryAmount"] = request.SalaryAmount,
            ["ProbationSalaryAmount"] = request.ProbationSalaryAmount,
            ["AllowanceAmount"] = request.AllowanceAmount,
            ["BankAccountNumber"] = request.BankAccountNumber,
            ["BankName"] = request.BankName,
            ["SalaryReceivedDate"] = request.SalaryReceivedDate,
            ["MealPerDay"] = request.MealPerDay
        };

    private static Dictionary<string, string?> BuildNannyValues(ContractNannyFillRequestDto request) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["NannyName"] = request.NannyName,
            ["NannyDOB"] = request.NannyDob,
            ["NannyIdentityNumber"] = request.NannyIdentityNumber,
            ["NannyIdentityIssueDate"] = request.NannyIdentityIssueDate,
            ["NannyIdentityIssuePlace"] = request.NannyIdentityIssuePlace,
            ["NannyPermanentAddress"] = request.NannyPermanentAddress,
            ["NannyCurrentAddress"] = request.NannyCurrentAddress,
            ["NannyPhone"] = request.NannyPhone
        };

    private static (string TemplateContent, Dictionary<string, string> FieldValues) ParseStoredContractContent(string? storedContent)
    {
        var raw = storedContent ?? string.Empty;
        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var markerIndex = raw.LastIndexOf(FieldValuesMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return ParseRenderedContractContent(raw);

        var template = raw[..markerIndex].TrimEnd();
        var encoded = raw[(markerIndex + FieldValuesMarker.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
            return ParseRenderedContractContent(template);

        try
        {
            string json;
            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (FormatException)
            {
                json = encoded;
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed != null)
            {
                foreach (var pair in parsed)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;
                    fieldValues[pair.Key.Trim()] = pair.Value?.Trim() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Keep backward compatibility: if metadata is invalid, continue with empty field values.
        }

        if (fieldValues.Count == 0)
            return ParseRenderedContractContent(template);

        return (ContractTemplateDefaults.DefaultContent, fieldValues);
    }

    private static (string TemplateContent, Dictionary<string, string> FieldValues) ParseRenderedContractContent(string renderedContent)
    {
        var fieldValues = ExtractFieldValuesFromRenderedContent(renderedContent);
        return (ContractTemplateDefaults.DefaultContent, fieldValues);
    }

    private static Dictionary<string, string> ExtractFieldValuesFromRenderedContent(string? content)
    {
        var text = content ?? string.Empty;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var parentSection = ExtractBetween(text, "1. BÊN A", "2. BÊN B");
        var nannySection = ExtractBetween(text, "2. BÊN B", "Hai bên cùng nhau thỏa thuận");

        AssignIfFound(result, "ParentName", GetNthGroup(parentSection, @"Ông\/bà:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ParentDOB", GetNthGroup(parentSection, @"Sinh ngày:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ParentIdentityNumber", GetNthGroup(parentSection, @"Số CCCD\/CMND\/hộ chiếu:\s*(.*?)\.\s*Cấp ngày:", 1));
        AssignIfFound(result, "ParentIdentityIssueDate", GetNthGroup(parentSection, @"Cấp ngày:\s*(.*?)\.\s*Nơi cấp:", 1));
        AssignIfFound(result, "ParentIdentityIssuePlace", GetNthGroup(parentSection, @"Nơi cấp:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ParentPermanentAddress", GetNthGroup(parentSection, @"Địa chỉ thường trú:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ParentCurrentAddress", GetNthGroup(parentSection, @"Địa chỉ chỗ ở hiện tại:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ParentPhone", GetNthGroup(parentSection, @"Điện thoại liên hệ:\s*(.*?)\.\s*Email:", 1));
        AssignIfFound(result, "ParentEmail", GetNthGroup(parentSection, @"Email:\s*([^\r\n]*)", 1));

        AssignIfFound(result, "NannyName", GetNthGroup(nannySection, @"Ông\/bà:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "NannyDOB", GetNthGroup(nannySection, @"Sinh ngày:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "NannyIdentityNumber", GetNthGroup(nannySection, @"Số CCCD\/CMND\/hộ chiếu:\s*(.*?)\.\s*Cấp ngày:", 1));
        AssignIfFound(result, "NannyIdentityIssueDate", GetNthGroup(nannySection, @"Cấp ngày:\s*(.*?)\.\s*Nơi cấp:", 1));
        AssignIfFound(result, "NannyIdentityIssuePlace", GetNthGroup(nannySection, @"Nơi cấp:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "NannyPermanentAddress", GetNthGroup(nannySection, @"Địa chỉ thường trú:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "NannyCurrentAddress", GetNthGroup(nannySection, @"Địa chỉ chỗ ở hiện tại:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "NannyPhone", GetNthGroup(nannySection, @"Điện thoại liên hệ:\s*([^\r\n]*)", 1));

        AssignIfFound(result, "ContractDurationMonths", GetNthGroup(text, @"xác định thời hạn:\s*([^\r\n]*?)\s*tháng", 1));
        AssignIfFound(result, "StartDate", GetNthGroup(text, @"1\.2\.\s*Thời hạn:\s*Từ ngày\s*(.*?)\s*đến ngày\s*([^\r\n]*)", 1));
        AssignIfFound(result, "EndDate", GetNthGroup(text, @"1\.2\.\s*Thời hạn:\s*Từ ngày\s*(.*?)\s*đến ngày\s*([^\r\n]*)", 2));
        AssignIfFound(result, "ProbationStartDate", GetNthGroup(text, @"1\.3\.[\s\S]*?Từ ngày\s*(.*?)\s*đến ngày\s*([^\r\n]*)", 1));
        AssignIfFound(result, "ProbationEndDate", GetNthGroup(text, @"1\.3\.[\s\S]*?Từ ngày\s*(.*?)\s*đến ngày\s*([^\r\n]*)", 2));
        AssignIfFound(result, "WorkAddress", GetNthGroup(text, @"2\.1\.\s*Địa điểm làm việc:\s*Tại nhà của Bên A,\s*địa chỉ:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "JobDescription", GetNthGroup(text, @"2\.2\.\s*Mô tả công việc chi tiết:\s*([\s\S]*?)\r?\nĐiều 3\.", 1));
        AssignIfFound(result, "SalaryAmount", GetNthGroup(text, @"3\.1\.\s*Mức lương chính:\s*([^\r\n]*?)\s*VNĐ", 1));
        AssignIfFound(result, "ProbationSalaryAmount", GetNthGroup(text, @"Mức lương thử việc[^:]*:\s*([^\r\n]*?)\s*VNĐ", 1));
        AssignIfFound(result, "AllowanceAmount", GetNthGroup(text, @"Phụ cấp đi lại\/điện thoại\s*\(nếu có\):\s*([^\r\n]*?)\s*VNĐ\/tháng", 1));
        AssignIfFound(result, "BankAccountNumber", GetNthGroup(text, @"Số tài khoản:\s*(.*?)\s*Ngân hàng:", 1));
        AssignIfFound(result, "BankName", GetNthGroup(text, @"Ngân hàng:\s*([^\r\n]*)", 1));
        AssignIfFound(result, "SalaryReceivedDate", GetNthGroup(text, @"Thời hạn trả lương:\s*Trả vào ngày\s*([^\r\n]*?)\s*hàng tháng", 1));
        AssignIfFound(result, "MealPerDay", GetNthGroup(text, @"5\.2\.\s*Ăn uống:\s*Bên A phụ trách\s*([^\r\n]*?)\s*bữa ăn\/ngày", 1));

        return result;
    }

    private static string ExtractBetween(string text, string startMarker, string endMarker)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var start = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        var end = text.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = text.Length;

        return text[start..end];
    }

    private static string GetNthGroup(string text, string pattern, int groupIndex)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (!match.Success || groupIndex >= match.Groups.Count)
            return string.Empty;

        return NormalizeExtractedValue(match.Groups[groupIndex].Value);
    }

    private static void AssignIfFound(IDictionary<string, string> values, string key, string? value)
    {
        var normalized = NormalizeExtractedValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        values[key] = normalized;
    }

    private static string NormalizeExtractedValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var value = raw.Trim();
        if (string.Equals(value, "...", StringComparison.Ordinal))
            return string.Empty;

        return value;
    }

    private static void MergeFieldValues(IDictionary<string, string> target, IDictionary<string, string?> updates)
    {
        foreach (var pair in updates)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            target[pair.Key.Trim()] = pair.Value?.Trim() ?? string.Empty;
        }
    }

    private static void ApplyParentDefaults(Contract contract, ContractParentFillRequestDto request, IDictionary<string, string> existingValues)
    {
        var parentUser = contract.HiringRecord?.ParentProfile?.User;
        request.ParentName = FirstNonEmpty(request.ParentName, GetFieldValue(existingValues, "ParentName"), GetDisplayName(parentUser));
        request.ParentPhone = FirstNonEmpty(request.ParentPhone, GetFieldValue(existingValues, "ParentPhone"), parentUser?.PhoneNumber);
        request.ParentEmail = FirstNonEmpty(request.ParentEmail, GetFieldValue(existingValues, "ParentEmail"), parentUser?.Email);
        request.ContractDurationMonths = FirstNonEmpty(
            request.ContractDurationMonths,
            GetFieldValue(existingValues, "ContractDurationMonths"),
            CalculateContractDurationMonths(contract.HiringRecord?.StartDate, contract.HiringRecord?.EndDate));
        request.WorkAddress = FirstNonEmpty(
            request.WorkAddress,
            GetFieldValue(existingValues, "WorkAddress"),
            contract.HiringRecord?.JobApplication?.JobPosting?.Location);
    }

    private static void ApplyNannyDefaults(Contract contract, ContractNannyFillRequestDto request, IDictionary<string, string> existingValues)
    {
        var nannyUser = contract.HiringRecord?.NannyProfile?.User;
        request.NannyName = FirstNonEmpty(request.NannyName, GetFieldValue(existingValues, "NannyName"), GetDisplayName(nannyUser));
        request.NannyPhone = FirstNonEmpty(request.NannyPhone, GetFieldValue(existingValues, "NannyPhone"), nannyUser?.PhoneNumber);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static string GetFieldValue(IDictionary<string, string> values, string key)
    {
        if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        return string.Empty;
    }

    private static void EnsureFieldValue(IDictionary<string, string> values, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (values.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return;

        if (!string.IsNullOrWhiteSpace(value))
            values[key] = value.Trim();
    }

    private static string CalculateContractDurationMonths(DateOnly? startDate, DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue || endDate.Value <= startDate.Value)
            return string.Empty;

        var totalDays = (endDate.Value.ToDateTime(TimeOnly.MinValue) - startDate.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
        var months = Math.Max(1, (int)Math.Ceiling(totalDays / 30d));
        return months.ToString();
    }

    private static string RenderTemplateWithValues(string templateContent, IDictionary<string, string> fieldValues)
    {
        var template = templateContent ?? string.Empty;
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var rendered = Regex.Replace(template, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", match =>
        {
            var key = match.Groups[1].Value;
            if (fieldValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
            return "...";
        });

        return rendered.Replace("[[CENTER]]", string.Empty, StringComparison.Ordinal);
    }

    private static void ValidateParentFillRequest(ContractParentFillRequestDto request)
    {
        RequireField(request.ParentName, "ParentName");
        RequireField(request.ParentDob, "ParentDOB");
        RequireField(request.ParentIdentityNumber, "ParentIdentityNumber");
        RequireField(request.ParentIdentityIssueDate, "ParentIdentityIssueDate");
        RequireField(request.ParentIdentityIssuePlace, "ParentIdentityIssuePlace");
        RequireField(request.ParentPermanentAddress, "ParentPermanentAddress");
        RequireField(request.ParentCurrentAddress, "ParentCurrentAddress");
        RequireField(request.ParentPhone, "ParentPhone");
        RequireField(request.ParentEmail, "ParentEmail");
        RequireField(request.ProbationStartDate, "ProbationStartDate");
        RequireField(request.ProbationEndDate, "ProbationEndDate");
        RequireField(request.WorkAddress, "WorkAddress");
        RequireField(request.SalaryAmount, "SalaryAmount");
        RequireField(request.ProbationSalaryAmount, "ProbationSalaryAmount");
        RequireField(request.AllowanceAmount, "AllowanceAmount");
        RequireField(request.BankAccountNumber, "BankAccountNumber");
        RequireField(request.BankName, "BankName");
        RequireField(request.SalaryReceivedDate, "SalaryReceivedDate");
        RequireField(request.MealPerDay, "MealPerDay");

        ValidateDateField(request.ParentDob, "ParentDOB");
        ValidateDateField(request.ParentIdentityIssueDate, "ParentIdentityIssueDate");
        ValidateDateField(request.ProbationStartDate, "ProbationStartDate");
        ValidateDateField(request.ProbationEndDate, "ProbationEndDate");

        if (!TryParseDate(request.ProbationStartDate, out var probationStart) ||
            !TryParseDate(request.ProbationEndDate, out var probationEnd))
        {
            throw new InvalidOperationException("Ngày thử việc không hợp lệ.");
        }

        var probationDays = (probationEnd.ToDateTime(TimeOnly.MinValue) - probationStart.ToDateTime(TimeOnly.MinValue)).TotalDays;
        if (probationDays < 0 || probationDays > 6)
            throw new InvalidOperationException("Thời gian thử việc tối đa là 06 ngày.");

        ValidatePhoneField(request.ParentPhone, "ParentPhone");
        ValidateEmailField(request.ParentEmail, "ParentEmail");

        var salary = ValidatePositiveDecimalField(request.SalaryAmount, "SalaryAmount");
        var probationSalary = ValidatePositiveDecimalField(request.ProbationSalaryAmount, "ProbationSalaryAmount");
        ValidatePositiveDecimalField(request.AllowanceAmount, "AllowanceAmount");

        var expectedProbationSalary = salary * 0.85m;
        if (Math.Abs(probationSalary - expectedProbationSalary) > 0.01m)
            throw new InvalidOperationException("Mức lương thử việc phải bằng 85% mức lương chính.");

        ValidateIntegerRangeField(request.SalaryReceivedDate, "SalaryReceivedDate", 1, 31);
        ValidateNaturalNumberField(request.MealPerDay, "MealPerDay");
    }

    private static void ValidateNannyFillRequest(ContractNannyFillRequestDto request)
    {
        RequireField(request.NannyName, "NannyName");
        RequireField(request.NannyDob, "NannyDOB");
        RequireField(request.NannyIdentityNumber, "NannyIdentityNumber");
        RequireField(request.NannyIdentityIssueDate, "NannyIdentityIssueDate");
        RequireField(request.NannyIdentityIssuePlace, "NannyIdentityIssuePlace");
        RequireField(request.NannyPermanentAddress, "NannyPermanentAddress");
        RequireField(request.NannyCurrentAddress, "NannyCurrentAddress");
        RequireField(request.NannyPhone, "NannyPhone");

        ValidateDateField(request.NannyDob, "NannyDOB");
        ValidateDateField(request.NannyIdentityIssueDate, "NannyIdentityIssueDate");
        ValidatePhoneField(request.NannyPhone, "NannyPhone");
    }

    private static void ValidateDate(string value, string fieldName)
    {
        if (!TryParseDate(value, out _))
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} không hợp lệ.");
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "d-M-yyyy" };
        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static void ValidatePhone(string value, string fieldName)
    {
        if (!Regex.IsMatch(value.Trim(), @"^\d{10,11}$"))
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} phải là dãy số gồm 10-11 ký tự.");
    }

    private static void ValidateEmail(string value, string fieldName)
    {
        try
        {
            _ = new MailAddress(value.Trim());
        }
        catch
        {
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} không đúng định dạng email.");
        }
    }

    private static decimal ValidatePositiveDecimal(string value, string fieldName)
    {
        if (!TryParseDecimal(value, out var number) || number <= 0)
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} phải là số lớn hơn 0.");

        return number;
    }

    private static bool TryParseDecimal(string value, out decimal number)
    {
        var raw = (value ?? string.Empty).Trim();
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            return true;

        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out number))
            return true;

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out number);
    }

    private static void ValidateIntegerRange(string value, string fieldName, int min, int max)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} phải nằm trong khoảng {min}-{max}.");
        }
    }

    private static void ValidateNaturalNumber(string value, string fieldName)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} phải là số nguyên dương.");
    }

    private static void Require(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{GetFieldDisplayName(fieldName)} là bắt buộc.");
    }

    private static string GetFieldDisplayName(string fieldName) => fieldName switch
    {
        "ParentName" => "Họ tên bố mẹ",
        "ParentDOB" => "Ngày sinh bố mẹ",
        "ParentIdentityNumber" => "Số CCCD/CMND bố mẹ",
        "ParentIdentityIssueDate" => "Ngày cấp CCCD/CMND bố mẹ",
        "ParentIdentityIssuePlace" => "Nơi cấp CCCD/CMND bố mẹ",
        "ParentPermanentAddress" => "Địa chỉ thường trú của bố mẹ",
        "ParentCurrentAddress" => "Địa chỉ hiện tại của bố mẹ",
        "ParentPhone" => "Số điện thoại bố mẹ",
        "ParentEmail" => "Email bố mẹ",
        "ContractDurationMonths" => "Thời hạn hợp đồng",
        "ProbationStartDate" => "Ngày bắt đầu thử việc",
        "ProbationEndDate" => "Ngày kết thúc thử việc",
        "WorkAddress" => "Địa điểm làm việc",
        "SalaryAmount" => "Mức lương",
        "ProbationSalaryAmount" => "Lương thử việc",
        "AllowanceAmount" => "Phụ cấp",
        "BankAccountNumber" => "Số tài khoản",
        "BankName" => "Tên ngân hàng",
        "SalaryReceivedDate" => "Ngày nhận lương",
        "MealPerDay" => "Số bữa ăn mỗi ngày",
        "NannyName" => "Họ tên bảo mẫu",
        "NannyDOB" => "Ngày sinh bảo mẫu",
        "NannyIdentityNumber" => "Số CCCD/CMND bảo mẫu",
        "NannyIdentityIssueDate" => "Ngày cấp CCCD/CMND bảo mẫu",
        "NannyIdentityIssuePlace" => "Nơi cấp CCCD/CMND bảo mẫu",
        "NannyPermanentAddress" => "Địa chỉ thường trú của bảo mẫu",
        "NannyCurrentAddress" => "Địa chỉ hiện tại của bảo mẫu",
        "NannyPhone" => "Số điện thoại bảo mẫu",
        _ => fieldName
    };

    private static void RequireField(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} là bắt buộc.");
    }

    private static void ValidateDateField(string value, string fieldName)
    {
        if (!TryParseDate(value, out _))
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} không hợp lệ.");
    }

    private static void ValidatePhoneField(string value, string fieldName)
    {
        if (!Regex.IsMatch((value ?? string.Empty).Trim(), @"^\d{10,11}$"))
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} phải là dãy số gồm 10-11 ký tự.");
    }

    private static void ValidateEmailField(string value, string fieldName)
    {
        try
        {
            _ = new MailAddress((value ?? string.Empty).Trim());
        }
        catch
        {
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} không đúng định dạng email.");
        }
    }

    private static decimal ValidatePositiveDecimalField(string value, string fieldName)
    {
        if (!TryParseDecimal(value, out var number) || number <= 0)
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} phải là số lớn hơn 0.");

        return number;
    }

    private static void ValidateIntegerRangeField(string value, string fieldName, int min, int max)
    {
        if (!int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} phải nằm trong khoảng {min}-{max}.");
        }
    }

    private static void ValidateNaturalNumberField(string value, string fieldName)
    {
        if (!int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException($"{GetFieldLabel(fieldName)} phải là số nguyên dương.");
        }
    }

    private static string GetFieldLabel(string fieldName) => fieldName switch
    {
        "ParentName" => "Họ tên bố mẹ",
        "ParentDOB" => "Ngày sinh bố mẹ",
        "ParentIdentityNumber" => "Số CCCD/CMND/hộ chiếu bố mẹ",
        "ParentIdentityIssueDate" => "Ngày cấp CCCD/CMND/hộ chiếu bố mẹ",
        "ParentIdentityIssuePlace" => "Nơi cấp CCCD/CMND/hộ chiếu bố mẹ",
        "ParentPermanentAddress" => "Địa chỉ thường trú của bố mẹ",
        "ParentCurrentAddress" => "Địa chỉ chỗ ở hiện tại của bố mẹ",
        "ParentPhone" => "Điện thoại liên hệ của bố mẹ",
        "ParentEmail" => "Email của bố mẹ",
        "ContractDurationMonths" => "Thời hạn hợp đồng",
        "ProbationStartDate" => "Từ ngày thử việc",
        "ProbationEndDate" => "Đến ngày thử việc",
        "WorkAddress" => "Địa điểm làm việc",
        "SalaryAmount" => "Mức lương chính",
        "ProbationSalaryAmount" => "Mức lương thử việc",
        "AllowanceAmount" => "Phụ cấp đi lại/điện thoại",
        "BankAccountNumber" => "Số tài khoản",
        "BankName" => "Ngân hàng",
        "SalaryReceivedDate" => "Ngày trả lương",
        "MealPerDay" => "Số bữa ăn mỗi ngày",
        "NannyName" => "Họ tên bảo mẫu",
        "NannyDOB" => "Ngày sinh bảo mẫu",
        "NannyIdentityNumber" => "Số CCCD/CMND/hộ chiếu bảo mẫu",
        "NannyIdentityIssueDate" => "Ngày cấp CCCD/CMND/hộ chiếu bảo mẫu",
        "NannyIdentityIssuePlace" => "Nơi cấp CCCD/CMND/hộ chiếu bảo mẫu",
        "NannyPermanentAddress" => "Địa chỉ thường trú của bảo mẫu",
        "NannyCurrentAddress" => "Địa chỉ chỗ ở hiện tại của bảo mẫu",
        "NannyPhone" => "Điện thoại liên hệ của bảo mẫu",
        _ => fieldName
    };

    private static string ResolveCurrentUserRole(Contract contract, Guid userId)
    {
        var parentUserId = contract.HiringRecord?.ParentProfile?.UserId;
        var nannyUserId = contract.HiringRecord?.NannyProfile?.UserId;

        if (parentUserId.HasValue && parentUserId.Value == userId)
            return "Parent";
        if (nannyUserId.HasValue && nannyUserId.Value == userId)
            return "Nanny";

        throw new UnauthorizedAccessException("Bạn không có quyền truy cập hợp đồng này.");
    }

    private static ContractListItemDto MapToListItem(Contract contract)
    {
        var hiring = contract.HiringRecord;
        var posting = hiring?.JobApplication?.JobPosting;
        var parentUser = hiring?.ParentProfile?.User;
        var nannyUser = hiring?.NannyProfile?.User;

        return new ContractListItemDto
        {
            ContractId = contract.Id,
            HiringRecordId = contract.HiringRecordId,
            JobTitle = posting?.Title ?? "Cong viec",
            ParentName = GetDisplayName(parentUser),
            ParentAvatar = parentUser?.AvatarUrl,
            NannyName = GetDisplayName(nannyUser),
            NannyAvatar = nannyUser?.AvatarUrl,
            StartDate = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate = hiring?.EndDate,
            HiringStatus = hiring?.Status ?? contract.Status,
            ContractStatus = contract.Status,
            PdfUrl = contract.PdfUrl,
            CreatedAt = contract.CreatedAt
        };
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null)
            return "Nguoi dung";

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static string SanitizeFileName(string value)
    {
        var raw = ToAscii(value);
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (invalid.Contains(ch))
                continue;

            sb.Append(char.IsWhiteSpace(ch) ? '_' : ch);
        }

        return sb.Length == 0 ? "HopDong" : sb.ToString();
    }

    private static byte[] BuildSimplePdf(string rawText)
    {
        var pages = RenderTextToJpegPages(rawText);
        return BuildPdfFromJpegPages(pages);
    }

    private static List<PdfJpegPage> RenderTextToJpegPages(string rawText)
    {
        const int imageWidth = 1240;
        const int imageHeight = 1754;
        const int marginX = 72;
        const int marginY = 84;
        const int maxTextWidth = imageWidth - (marginX * 2);

        var normalized = (rawText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var sourceLines = normalized.Split('\n');

        var wrappedLines = new List<string>();
        using (var measureBitmap = new Bitmap(1, 1))
        using (var measureGraphics = Graphics.FromImage(measureBitmap))
        using (var font = new Font("Arial", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            foreach (var line in sourceLines)
            {
                wrappedLines.AddRange(WrapLineByWidth(line, measureGraphics, font, maxTextWidth));
            }
        }

        if (wrappedLines.Count == 0)
            wrappedLines.Add("Hợp đồng.");

        var pages = new List<PdfJpegPage>();
        using (var lineMeasureBitmap = new Bitmap(1, 1))
        using (var lineMeasureGraphics = Graphics.FromImage(lineMeasureBitmap))
        using (var font = new Font("Arial", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            var lineHeight = (int)Math.Ceiling(font.GetHeight(lineMeasureGraphics) * 1.35f);
            var linesPerPage = Math.Max(1, (imageHeight - (marginY * 2)) / lineHeight);

            for (var index = 0; index < wrappedLines.Count; index += linesPerPage)
            {
                var pageLines = wrappedLines.Skip(index).Take(linesPerPage).ToList();
                using var bitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.White);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                using var brush = new SolidBrush(Color.Black);
                var y = marginY;
                foreach (var line in pageLines)
                {
                    graphics.DrawString(line, font, brush, new PointF(marginX, y));
                    y += lineHeight;
                }

                using var stream = new MemoryStream();
                var encoder = GetJpegEncoder();
                if (encoder != null)
                {
                    using var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                    bitmap.Save(stream, encoder, encoderParameters);
                }
                else
                {
                    bitmap.Save(stream, ImageFormat.Jpeg);
                }

                pages.Add(new PdfJpegPage(stream.ToArray(), bitmap.Width, bitmap.Height));
            }
        }

        return pages;
    }

    private static byte[] BuildPdfFromJpegPages(List<PdfJpegPage> pages)
    {
        if (pages.Count == 0)
            pages.Add(new PdfJpegPage(Array.Empty<byte>(), 1, 1));

        var pageCount = pages.Count;
        var objectCount = 2 + (pageCount * 3); // catalog + pages + (image, content, page)*N

        var imageObjectNumbers = new int[pageCount];
        var contentObjectNumbers = new int[pageCount];
        var pageObjectNumbers = new int[pageCount];

        var nextObject = 3;
        for (var i = 0; i < pageCount; i++)
        {
            imageObjectNumbers[i] = nextObject++;
            contentObjectNumbers[i] = nextObject++;
            pageObjectNumbers[i] = nextObject++;
        }

        using var output = new MemoryStream();
        WritePdfAscii(output, "%PDF-1.4\n");
        var offsets = new long[objectCount + 1];

        for (var objectNumber = 1; objectNumber <= objectCount; objectNumber++)
        {
            offsets[objectNumber] = output.Position;
            WritePdfAscii(output, $"{objectNumber} 0 obj\n");

            if (objectNumber == 1)
            {
                WritePdfAscii(output, "<< /Type /Catalog /Pages 2 0 R >>\n");
            }
            else if (objectNumber == 2)
            {
                var kids = string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"));
                WritePdfAscii(output, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\n");
            }
            else
            {
                for (var index = 0; index < pageCount; index++)
                {
                    if (objectNumber == imageObjectNumbers[index])
                    {
                        var page = pages[index];
                        WritePdfAscii(output,
                            $"<< /Type /XObject /Subtype /Image /Width {page.Width} /Height {page.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {page.Bytes.Length} >>\nstream\n");
                        output.Write(page.Bytes, 0, page.Bytes.Length);
                        WritePdfAscii(output, "\nendstream\n");
                        break;
                    }

                    if (objectNumber == contentObjectNumbers[index])
                    {
                        const int pdfWidth = 595;
                        const int pdfHeight = 842;
                        var content = $"q\n{pdfWidth} 0 0 {pdfHeight} 0 0 cm\n/Im{index + 1} Do\nQ\n";
                        var contentBytes = Encoding.ASCII.GetBytes(content);
                        WritePdfAscii(output, $"<< /Length {contentBytes.Length} >>\nstream\n");
                        output.Write(contentBytes, 0, contentBytes.Length);
                        WritePdfAscii(output, "endstream\n");
                        break;
                    }

                    if (objectNumber == pageObjectNumbers[index])
                    {
                        WritePdfAscii(output,
                            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im{index + 1} {imageObjectNumbers[index]} 0 R >> >> /Contents {contentObjectNumbers[index]} 0 R >>\n");
                        break;
                    }
                }
            }

            WritePdfAscii(output, "endobj\n");
        }

        var xrefOffset = output.Position;
        WritePdfAscii(output, $"xref\n0 {objectCount + 1}\n");
        WritePdfAscii(output, "0000000000 65535 f \n");
        for (var i = 1; i <= objectCount; i++)
        {
            WritePdfAscii(output, $"{offsets[i]:D10} 00000 n \n");
        }

        WritePdfAscii(output, "trailer\n");
        WritePdfAscii(output, $"<< /Size {objectCount + 1} /Root 1 0 R >>\n");
        WritePdfAscii(output, "startxref\n");
        WritePdfAscii(output, $"{xrefOffset}\n");
        WritePdfAscii(output, "%%EOF");

        return output.ToArray();
    }

    private static IEnumerable<string> WrapLineByWidth(string line, Graphics graphics, Font font, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new[] { string.Empty };

        var words = line.Split(' ', StringSplitOptions.None);
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            var width = graphics.MeasureString(candidate, font).Width;
            if (width <= maxWidth || current.Length == 0)
            {
                current.Clear();
                current.Append(candidate);
            }
            else
            {
                result.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }

    private static ImageCodecInfo? GetJpegEncoder()
    {
        return ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(codec => string.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));
    }

    private static void WritePdfAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static IEnumerable<string> WrapLine(string line, int width)
    {
        if (string.IsNullOrEmpty(line))
            return new[] { string.Empty };

        var result = new List<string>();
        var remaining = line.TrimEnd();
        while (remaining.Length > width)
        {
            var breakIndex = remaining.LastIndexOf(' ', width);
            if (breakIndex <= 0)
                breakIndex = width;

            result.Add(remaining[..breakIndex].TrimEnd());
            remaining = remaining[breakIndex..].TrimStart();
        }

        result.Add(remaining);
        return result;
    }

    private static string ToAscii(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            var mapped = ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => ch
            };

            if (mapped <= 127)
                sb.Append(mapped);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record PdfJpegPage(byte[] Bytes, int Width, int Height);
}
