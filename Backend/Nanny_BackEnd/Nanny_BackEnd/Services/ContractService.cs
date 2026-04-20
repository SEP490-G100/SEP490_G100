using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ContractService
{
    private static readonly HashSet<string> ParentEditableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParentName",
        "ParentPhone",
        "ParentEmail",
        "ParentDOB",
        "ParentIdentityNumber",
        "ParentIdentityIssueDate",
        "ParentIdentityIssuePlace",
        "ParentPermanentAddress",
        "ParentCurrentAddress",
        "ChildName",
        "ChildDOB",
        "ContractDurationMonths",
        "ProbationStartDate",
        "ProbationEndDate",
        "WorkAddress",
        "SalaryAmount",
        "ProbationSalaryAmount",
        "SecurityAmount",
        "TotalSalaryAmount",
        "AllowanceAmount",
        "SalaryReceivedDate",
        "MealPerDay",
        "BankAccountNumber",
        "BankName",
        "PaymentMethod",
        "PaymentMethodCashChecked",
        "PaymentMethodBankChecked"
    };

    private static readonly HashSet<string> NannyEditableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "NannyName",
        "NannyPhone",
        "NannyEmail",
        "NannyDOB",
        "NannyIdentityNumber",
        "NannyIdentityIssueDate",
        "NannyIdentityIssuePlace",
        "NannyPermanentAddress",
        "NannyCurrentAddress"
    };

    private static readonly HashSet<string> StrictPositiveIntegerFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "SalaryAmount",
        "ProbationSalaryAmount",
        "SecurityAmount",
        "TotalSalaryAmount",
        "AllowanceAmount",
        "MealPerDay"
    };

    private static readonly HashSet<string> NoSpecialCharFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ChildName",
        "BankName"
    };

    private static readonly HashSet<string> AlphaNumericFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParentIdentityNumber",
        "NannyIdentityNumber",
        "BankAccountNumber"
    };

    private static readonly HashSet<string> DobFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParentDOB",
        "ChildDOB",
        "NannyDOB"
    };

    private static readonly HashSet<string> IssueDateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParentIdentityIssueDate",
        "NannyIdentityIssueDate"
    };

    private static readonly HashSet<string> PaymentManagedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PaymentMethod",
        "PaymentMethodCashChecked",
        "PaymentMethodBankChecked",
        "BankAccountNumber",
        "BankName"
    };

    private static readonly string[] WorkingDayNames = { "Thu 2", "Thu 3", "Thu 4", "Thu 5", "Thu 6", "Thu 7", "Chu nhat" };
    private static readonly string[] WorkingSlotNames = { "Sang", "Chieu", "Toi", "Dem" };
    private const string WorkingTimeSlotsField = "WorkingTimeSlots";
    private const string PaymentMethodCash = "Cash";
    private const string PaymentMethodBankTransfer = "BankTransfer";

    private readonly ContractRepository _repo;

    public ContractService(ContractRepository repo) => _repo = repo;

    public async Task<ContractListResponseDto> GetMyContractsAsync(Guid userId)
    {
        var contracts = await _repo.GetContractsByUserIdAsync(userId);
        var result = new ContractListResponseDto();

        foreach (var contract in contracts)
        {
            var item = MapToListItem(contract);
            switch (contract.HiringRecord?.Status)
            {
                case 1:
                    result.Active.Add(item);
                    break;
                case 0:
                    result.Pending.Add(item);
                    break;
                default:
                    result.History.Add(item);
                    break;
            }
        }

        return result;
    }

    public async Task<ContractDetailDto> GetContractDetailAsync(Guid contractId, Guid userId)
    {
        var contract = await _repo.GetContractDetailAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");

        var currentUserRole = ResolveCurrentUserRole(contract, userId);
        return MapToDetail(contract, currentUserRole);
    }

    public async Task<ContractDetailDto> SaveDraftByParentAsync(Guid contractId, Guid userId, ContractUpsertRequestDto request)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");
        var currentUserRole = ResolveCurrentUserRole(contract, userId);

        if (!string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chi Parent moi duoc phep cap nhat ban nhap.");
        if (contract.Status == 3)
            throw new InvalidOperationException("Hop dong da hoan thanh. Khong the cap nhat thong tin.");
        if (contract.Status is < 0 or > 3)
            throw new InvalidOperationException("Trang thai hop dong khong hop le.");

        var templateContent = contract.ContractTemplate?.Content ?? contract.ContractContent ?? string.Empty;
        var editableTemplateContent = EnsureEditableContactTokens(templateContent);
        var placeholders = ExtractPlaceholderKeys(editableTemplateContent);
        var parentFieldsInTemplate = placeholders.Where(ParentEditableFields.Contains).ToList();
        var parentTextFieldsInTemplate = parentFieldsInTemplate
            .Where(field => !PaymentManagedFields.Contains(field))
            .ToList();

        ValidateRequestValues(request, parentTextFieldsInTemplate);
        ValidateProbationDateRange(request);
        var existingValues = ExtractDraftFieldValues(editableTemplateContent, contract.ContractContent);
        var mergedFieldValues = MergeFieldValues(existingValues, request.FieldValues);
        var updatedContent = ApplyFieldValues(editableTemplateContent, mergedFieldValues, placeholders);
        updatedContent = ApplyJobDescription(updatedContent, contract.HiringRecord?.JobApplication?.JobPosting?.Description);
        updatedContent = ApplyHiringDates(
            updatedContent,
            contract.HiringRecord?.StartDate ?? DateOnly.MinValue,
            contract.HiringRecord?.EndDate);
        updatedContent = ApplyPaymentMethod(updatedContent, request, isParentEditing: true);
        updatedContent = ApplyWorkingTimeSection(updatedContent, mergedFieldValues);
        EnsureNoPendingOwnedPlaceholders(updatedContent, parentFieldsInTemplate, "Parent");

        contract.ContractContent = updatedContent;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = userId;
        var hiringForParentDraft = contract.HiringRecord
            ?? throw new InvalidOperationException("Khong tim thay hiring record lien ket.");
        var contractDurationMonths = GetContractDurationMonthsFromRequest(request);
        if (contractDurationMonths.HasValue)
            hiringForParentDraft.ContractDuration = contractDurationMonths.Value;
        hiringForParentDraft.UpdatedAt = DateTime.UtcNow;
        hiringForParentDraft.UpdatedBy = userId;
        if (request.IsSignedByActor)
            contract.SignedByParent = true;
        if (!contract.SignedByParent)
            throw new InvalidOperationException("Parent can tick ky hop dong truoc khi xac nhan thong tin.");
        if (contract.Status == 0)
            contract.Status = 1; // ParentSaved

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, currentUserRole);
    }

    public async Task<ContractDetailDto> FillByNannyAsync(Guid contractId, Guid userId, ContractUpsertRequestDto request)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");
        var currentUserRole = ResolveCurrentUserRole(contract, userId);

        if (!string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chi Nanny moi duoc phep submit thong tin.");
        if (contract.Status == 3)
            throw new InvalidOperationException("Hop dong da hoan thanh. Khong the cap nhat thong tin.");
        if (contract.Status is not 1 and not 2)
            throw new InvalidOperationException("Hop dong khong o trang thai cho Nanny dien thong tin.");

        var templateContent = contract.ContractTemplate?.Content ?? contract.ContractContent ?? string.Empty;
        var editableTemplateContent = EnsureEditableContactTokens(templateContent);
        var currentContent = contract.ContractContent ?? string.Empty;
        var currentPlaceholders = ExtractPlaceholderKeys(currentContent);
        var placeholders = ExtractPlaceholderKeys(editableTemplateContent);
        var parentFieldsStillPending = currentPlaceholders.Where(ParentEditableFields.Contains).ToList();
        if (parentFieldsStillPending.Count > 0)
            throw new InvalidOperationException("Parent chua hoan tat thong tin. Vui long cho Parent cap nhat truoc.");

        var nannyFieldsInTemplate = placeholders.Where(NannyEditableFields.Contains).ToList();
        ValidateRequestValues(request, nannyFieldsInTemplate);

        var existingValues = ExtractDraftFieldValues(editableTemplateContent, currentContent);
        var mergedFieldValues = MergeFieldValues(existingValues, request.FieldValues);
        var existingWorkingTimeSlots = BuildWorkingTimeSlotsFieldValueFromContent(currentContent);
        if (!string.IsNullOrWhiteSpace(existingWorkingTimeSlots) &&
            !mergedFieldValues.ContainsKey(WorkingTimeSlotsField))
        {
            mergedFieldValues[WorkingTimeSlotsField] = existingWorkingTimeSlots;
        }

        var updatedContent = ApplyFieldValues(editableTemplateContent, mergedFieldValues, placeholders);
        updatedContent = ApplyJobDescription(updatedContent, contract.HiringRecord?.JobApplication?.JobPosting?.Description);
        updatedContent = ApplyHiringDates(
            updatedContent,
            contract.HiringRecord?.StartDate ?? DateOnly.MinValue,
            contract.HiringRecord?.EndDate);
        updatedContent = ApplyWorkingTimeSection(updatedContent, mergedFieldValues);
        EnsureNoPendingOwnedPlaceholders(updatedContent, nannyFieldsInTemplate, "Nanny");

        contract.ContractContent = updatedContent;
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = userId;
        var hiringForNannyConfirm = contract.HiringRecord
            ?? throw new InvalidOperationException("Khong tim thay hiring record lien ket.");
        var nowUtc = DateTime.UtcNow;
        hiringForNannyConfirm.NannyConfirmedAt = nowUtc;
        hiringForNannyConfirm.UpdatedAt = nowUtc;
        hiringForNannyConfirm.UpdatedBy = userId;
        if (request.IsSignedByActor)
            contract.SignedByNanny = true;
        if (!contract.SignedByNanny)
            throw new InvalidOperationException("Nanny can tick ky hop dong truoc khi xac nhan thong tin.");
        contract.Status = 2; // NannyFilled

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, currentUserRole);
    }

    public async Task<ContractDetailDto> FinalConfirmByParentAsync(Guid contractId, Guid userId)
    {
        var contract = await _repo.GetContractForUpdateAsync(contractId)
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");
        var currentUserRole = ResolveCurrentUserRole(contract, userId);

        if (!string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Chi Parent moi duoc phep xac nhan cuoi.");
        if (contract.Status != 2)
            throw new InvalidOperationException("Hop dong chua o trang thai Nanny da submit.");

        var pendingTokens = ExtractPlaceholderKeys(contract.ContractContent);
        var requiredTokens = pendingTokens.Where(token =>
            ParentEditableFields.Contains(token) || NannyEditableFields.Contains(token)).ToList();
        if (requiredTokens.Count > 0)
            throw new InvalidOperationException("Hop dong van con truong chua duoc dien day du.");

        var hiring = contract.HiringRecord
            ?? throw new InvalidOperationException("Khong tim thay hiring record lien ket.");

        var now = DateTime.UtcNow;
        if (!contract.SignedByParent || !contract.SignedByNanny)
            throw new InvalidOperationException("Can ca Parent va Nanny da ky hop dong truoc khi xac nhan hoan thanh.");
        contract.SignedAt = now;
        contract.Status = 3; // Active
        contract.UpdatedAt = now;
        contract.UpdatedBy = userId;

        hiring.Status = (int)HiringRecordStatus.Active;
        hiring.ParentConfirmedAt = now;
        hiring.UpdatedAt = now;
        hiring.UpdatedBy = userId;

        await _repo.SaveChangesAsync();
        return MapToDetail(contract, currentUserRole);
    }

    private static void ValidateRequestValues(ContractUpsertRequestDto request, List<string> ownedFieldsInTemplate)
    {
        foreach (var field in ownedFieldsInTemplate)
        {
            if (!request.FieldValues.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Truong {field} khong duoc de trong.");

            ValidateFieldBusinessRule(field, value!.Trim());
        }
    }

    private static int? GetContractDurationMonthsFromRequest(ContractUpsertRequestDto request)
    {
        if (!request.FieldValues.TryGetValue("ContractDurationMonths", out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        return int.TryParse(raw.Trim(), out var duration) ? duration : null;
    }

    private static Dictionary<string, string?> MergeFieldValues(
        IReadOnlyDictionary<string, string> existingValues,
        IReadOnlyDictionary<string, string?> incomingValues)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in existingValues)
            merged[pair.Key] = pair.Value;

        foreach (var pair in incomingValues)
            merged[pair.Key] = pair.Value;

        return merged;
    }

    private static string? BuildWorkingTimeSlotsFieldValueFromContent(string? content)
    {
        var slots = ExtractWorkingTimeSlotsFromContent(content);
        if (slots.Count == 0)
            return null;

        return string.Join(",",
            slots.OrderBy(slot => slot.DayOfWeek)
                .ThenBy(slot => slot.TimeSlot)
                .Select(slot => $"{slot.DayOfWeek}-{slot.TimeSlot}"));
    }

    private static void ValidateFieldBusinessRule(string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Truong {field} khong duoc de trong.");

        if (string.Equals(field, "ParentName", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsLettersAndSpacesOnly(value))
                throw new InvalidOperationException("ParentName chi duoc chua chu cai va khoang trang, khong duoc chua so.");
            return;
        }

        if (string.Equals(field, "NannyName", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsLettersAndSpacesOnly(value))
                throw new InvalidOperationException("NannyName chi duoc chua chu cai va khoang trang.");
            return;
        }

        if (string.Equals(field, "ParentPhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "NannyPhone", StringComparison.OrdinalIgnoreCase))
        {
            if (!Regex.IsMatch(value, @"^\d{10,11}$"))
                throw new InvalidOperationException($"{field} phai la chuoi so co 10 den 11 ky tu.");
            return;
        }

        if (string.Equals(field, "ParentEmail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "NannyEmail", StringComparison.OrdinalIgnoreCase))
        {
            var emailValidator = new EmailAddressAttribute();
            if (!emailValidator.IsValid(value))
                throw new InvalidOperationException($"{field} khong dung dinh dang email hop le.");
            return;
        }

        if (AlphaNumericFields.Contains(field))
        {
            if (!Regex.IsMatch(value, @"^[\p{L}\p{N}]+$"))
                throw new InvalidOperationException($"{field} khong duoc chua ky tu dac biet.");
            return;
        }

        if (NoSpecialCharFields.Contains(field))
        {
            if (!Regex.IsMatch(value, @"^[\p{L}\p{N}\s]+$"))
                throw new InvalidOperationException($"{field} khong duoc chua ky tu dac biet.");
            return;
        }

        if (string.Equals(field, "ContractDurationMonths", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var duration) || duration < 1 || duration > 12)
                throw new InvalidOperationException("ContractDurationMonths phai la so tu 1 den 12.");
            return;
        }

        if (string.Equals(field, "SalaryReceivedDate", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, out var salaryDay) || salaryDay < 1 || salaryDay > 31)
                throw new InvalidOperationException("SalaryReceivedDate chi chap nhan gia tri tu 1 den 31.");
            return;
        }

        if (StrictPositiveIntegerFields.Contains(field))
        {
            if (!Regex.IsMatch(value, @"^\d+$"))
                throw new InvalidOperationException($"{field} phai la so duong, khong duoc chua ky tu dac biet.");
            if (!long.TryParse(value, out var number) || number <= 0)
                throw new InvalidOperationException($"{field} phai lon hon 0.");
            return;
        }

        if (DobFields.Contains(field))
        {
            var date = ParseContractDate(value, field);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (date > today)
                throw new InvalidOperationException($"{field} khong duoc la ngay trong tuong lai.");
            return;
        }

        if (IssueDateFields.Contains(field))
        {
            var date = ParseContractDate(value, field);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (date > today)
                throw new InvalidOperationException($"{field} khong duoc la ngay trong tuong lai.");
            return;
        }
    }

    private static bool IsLettersAndSpacesOnly(string value) =>
        Regex.IsMatch(value, @"^[\p{L}\s]+$");

    private static string ApplyFieldValues(
        string content,
        Dictionary<string, string?> values,
        List<string> allowedFields)
    {
        var updated = content;
        foreach (var field in allowedFields)
        {
            if (!values.TryGetValue(field, out var raw))
                continue;

            var normalized = NormalizeFieldValue(raw);
            updated = ReplaceToken(updated, field, normalized);
        }

        return updated;
    }

    private static string ApplyJobDescription(string content, string? description)
    {
        var normalized = NormalizeFieldValue(description);
        if (normalized.StartsWith("2.2.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex.Replace(
                normalized,
                @"^\s*2\.2\.\s*[^:]*:\s*",
                string.Empty,
                RegexOptions.IgnoreCase).Trim();
        }

        return ReplaceToken(content, "JobDescription", normalized);
    }

    private static string ApplyHiringDates(string content, DateOnly startDate, DateOnly? endDate)
    {
        var updated = content;
        var startDateText = startDate == DateOnly.MinValue ? string.Empty : startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var endDateText = endDate.HasValue ? endDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;

        updated = ReplaceToken(updated, "StartDate", startDateText);
        updated = ReplaceToken(updated, "EndDate", endDateText);
        return updated;
    }

    private static void ValidateProbationDateRange(ContractUpsertRequestDto request)
    {
        if (!request.FieldValues.TryGetValue("ProbationStartDate", out var probationStartRaw) ||
            !request.FieldValues.TryGetValue("ProbationEndDate", out var probationEndRaw))
        {
            return;
        }

        var startDate = ParseContractDate(probationRaw: probationStartRaw, fieldName: "ProbationStartDate");
        var endDate = ParseContractDate(probationRaw: probationEndRaw, fieldName: "ProbationEndDate");

        if (endDate < startDate)
            throw new InvalidOperationException("ProbationEndDate phai lon hon hoac bang ProbationStartDate.");

        var gap = endDate.DayNumber - startDate.DayNumber;
        if (gap > 6)
            throw new InvalidOperationException("Thoi gian thu viec chi duoc cach nhau toi da 6 ngay.");
    }

    private static DateOnly ParseContractDate(string? probationRaw, string fieldName)
    {
        var raw = probationRaw?.Trim() ?? string.Empty;
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            return isoDate;
        if (DateOnly.TryParseExact(raw, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var vnDate))
            return vnDate;
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return fallback;

        throw new InvalidOperationException($"{fieldName} khong hop le.");
    }

    private static string ApplyWorkingTimeSection(string content, Dictionary<string, string?> fieldValues)
    {
        if (!fieldValues.TryGetValue(WorkingTimeSlotsField, out var rawSchedule) || string.IsNullOrWhiteSpace(rawSchedule))
            return content;

        var slots = ParseWorkingTimeSlots(rawSchedule)
            .OrderBy(slot => slot.DayOfWeek)
            .ThenBy(slot => slot.TimeSlot)
            .ToList();
        if (slots.Count == 0)
            return content;

        var groupedLines = slots
            .GroupBy(slot => slot.DayOfWeek)
            .Select(group =>
            {
                var slotText = string.Join(", ", group
                    .OrderBy(s => s.TimeSlot)
                    .Select(s => WorkingSlotNames[s.TimeSlot]));
                return $"- {WorkingDayNames[group.Key]}: {slotText}";
            })
            .ToList();

        var sectionBody = string.Join(Environment.NewLine, groupedLines);
        var sectionPattern = @"(?is)(4\.1\.[^\r\n]*\r?\n)(.*?)(\r?\n\s*4\.2\.)";
        if (Regex.IsMatch(content, sectionPattern))
        {
            return Regex.Replace(
                content,
                sectionPattern,
                match => $"{match.Groups[1].Value}{sectionBody}{match.Groups[3].Value}");
        }

        var section41Pattern = @"(?im)^(?<head>\s*4\.1\.[^\r\n]*)(?<tail>\r?\n|$)";
        if (Regex.IsMatch(content, section41Pattern))
        {
            return Regex.Replace(
                content,
                section41Pattern,
                match => $"{match.Groups["head"].Value}{Environment.NewLine}{sectionBody}{match.Groups["tail"].Value}");
        }

        return content;
    }

    private static List<(int DayOfWeek, int TimeSlot)> ParseWorkingTimeSlots(string rawSchedule)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(int DayOfWeek, int TimeSlot)>();
        var segments = rawSchedule
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;
            if (!int.TryParse(parts[0], out var dayOfWeek) || dayOfWeek < 0 || dayOfWeek > 6)
                continue;
            if (!int.TryParse(parts[1], out var timeSlot) || timeSlot < 0 || timeSlot > 3)
                continue;

            var key = $"{dayOfWeek}-{timeSlot}";
            if (!unique.Add(key))
                continue;

            result.Add((dayOfWeek, timeSlot));
        }

        return result;
    }

    private static string ApplyPaymentMethod(string content, ContractUpsertRequestDto request, bool isParentEditing)
    {
        if (!isParentEditing)
            return content;

        var requiresPaymentSelection =
            content.Contains("{{PaymentMethod", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("(cái checkbox của option này ở đây)", StringComparison.OrdinalIgnoreCase) ||
            HasPaymentClause(content);

        var method = string.IsNullOrWhiteSpace(request.PaymentMethod)
            ? string.Empty
            : request.PaymentMethod.Trim();
        var isCash = string.Equals(method, PaymentMethodCash, StringComparison.OrdinalIgnoreCase);
        var isBank = string.Equals(method, PaymentMethodBankTransfer, StringComparison.OrdinalIgnoreCase);

        if (requiresPaymentSelection && string.IsNullOrWhiteSpace(method))
            throw new InvalidOperationException("Vui long chon PaymentMethod (Cash hoac BankTransfer).");

        if (!string.IsNullOrWhiteSpace(method) && !isCash && !isBank)
            throw new InvalidOperationException("PaymentMethod khong hop le. Chi chap nhan Cash hoac BankTransfer.");

        if (isBank)
        {
            if (string.IsNullOrWhiteSpace(request.BankAccountNumber))
                throw new InvalidOperationException("BankAccountNumber khong duoc de trong khi chon BankTransfer.");
            if (string.IsNullOrWhiteSpace(request.BankName))
                throw new InvalidOperationException("BankName khong duoc de trong khi chon BankTransfer.");

            ValidateFieldBusinessRule("BankAccountNumber", request.BankAccountNumber.Trim());
            ValidateFieldBusinessRule("BankName", request.BankName.Trim());
        }

        var updated = content;
        updated = ReplaceToken(updated, "PaymentMethod", isCash ? "Tien mat" : isBank ? "Chuyen khoan" : string.Empty);
        updated = ReplaceToken(updated, "PaymentMethodCashChecked", isCash ? "[x]" : "[ ]");
        updated = ReplaceToken(updated, "PaymentMethodBankChecked", isBank ? "[x]" : "[ ]");
        updated = ReplaceToken(updated, "BankAccountNumber", isBank ? request.BankAccountNumber?.Trim() ?? string.Empty : string.Empty);
        updated = ReplaceToken(updated, "BankName", isBank ? request.BankName?.Trim() ?? string.Empty : string.Empty);
        updated = ApplyPaymentBankFallback(
            updated,
            isBank ? request.BankAccountNumber?.Trim() ?? string.Empty : string.Empty,
            isBank ? request.BankName?.Trim() ?? string.Empty : string.Empty);
        if (isBank)
        {
            updated = NormalizeBankTransferSentence(
                updated,
                request.BankAccountNumber?.Trim() ?? string.Empty,
                request.BankName?.Trim() ?? string.Empty);
        }

        updated = ReplaceLegacyPaymentCheckboxMarkers(updated, isCash, isBank);
        updated = ReplacePaymentClauseCheckboxes(updated, isCash, isBank);
        return updated;
    }

    private static string ApplyPaymentBankFallback(string content, string bankAccountNumber, string bankName)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var updated = content;

        if (!updated.Contains("{{BankAccountNumber}}", StringComparison.OrdinalIgnoreCase))
        {
            updated = Regex.Replace(
                updated,
                @"(?is)(S[ốo]\s*t[aà]i\s*kho[aả]n\s*:\s*)(?<val>.*?)(\s*(?:\.\s*Ng[aâ]n\s*h[aà]ng\s*:|Ng[aâ]n\s*h[aà]ng\s*:))",
                m => $"{m.Groups[1].Value}{bankAccountNumber}{m.Groups[3].Value}",
                RegexOptions.IgnoreCase);
        }

        if (!updated.Contains("{{BankName}}", StringComparison.OrdinalIgnoreCase))
        {
            updated = Regex.Replace(
                updated,
                @"(?is)(Ng[aâ]n\s*h[aà]ng\s*:\s*)(?<val>.*?)(?=(\.\s*)?(Th[oơ]i\s*h[aạ]n\s*tr[aả]\s*l[uư][oơ]ng\s*:)|\r?\n|$)",
                m => $"{m.Groups[1].Value}{bankName}",
                RegexOptions.IgnoreCase);
        }

        return updated;
    }

    private static string NormalizeBankTransferSentence(string content, string bankAccountNumber, string bankName)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var normalizedAccount = bankAccountNumber.Trim();
        var normalizedBank = bankName.Trim();

        var updated = ReplaceFirstRegex(
            content,
            @"(?is)(S[ốo]\s*t[aà]i\s*kho[aả]n\s*:\s*)(?<acc>[^.\r\n]*)(?:\.\s*Ng[aâ]n\s*h[aà]ng\s*:\s*(?<bank>[^\r\n;]*))?",
            match => $"{match.Groups[1].Value}{normalizedAccount}. Ngân hàng: {normalizedBank}",
            RegexOptions.IgnoreCase);

        if (!string.Equals(updated, content, StringComparison.Ordinal))
            return updated;

        return ReplaceFirstRegex(
            content,
            @"(?is)(So\s*tai\s*khoan\s*:\s*)(?<acc>[^.\r\n]*)(?:\.\s*Ngan\s*hang\s*:\s*(?<bank>[^\r\n;]*))?",
            match => $"{match.Groups[1].Value}{normalizedAccount}. Ngan hang: {normalizedBank}",
            RegexOptions.IgnoreCase);
    }

    private static string ReplaceFirstRegex(
        string input,
        string pattern,
        Func<Match, string> replacementEvaluator,
        RegexOptions options)
    {
        var match = Regex.Match(input, pattern, options);
        if (!match.Success)
            return input;

        var replacement = replacementEvaluator(match);
        return input.Remove(match.Index, match.Length).Insert(match.Index, replacement);
    }

    private static string ReplaceLegacyPaymentCheckboxMarkers(string content, bool isCash, bool isBank)
    {
        const string marker = "(cái checkbox của option này ở đây)";
        var first = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (first < 0)
            return content;

        var updated = content.Remove(first, marker.Length).Insert(first, isCash ? "[x]" : "[ ]");
        var second = updated.IndexOf(marker, first + 1, StringComparison.OrdinalIgnoreCase);
        if (second < 0)
            return updated;

        return updated.Remove(second, marker.Length).Insert(second, isBank ? "[x]" : "[ ]");
    }

    private static bool HasPaymentClause(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        return Regex.IsMatch(content, @"Trả\s*lương\s*bằng\s*:", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(content, @"Tra\s*luong\s*bang\s*:", RegexOptions.IgnoreCase);
    }

    private static string SyncPaymentMarkersFromPersistedContent(string templateContent, string persistedContent)
    {
        if (string.IsNullOrWhiteSpace(templateContent) || string.IsNullOrWhiteSpace(persistedContent))
            return templateContent;

        var paymentMethod = ResolvePaymentMethodFromContent(persistedContent);
        if (paymentMethod == null)
            return templateContent;

        var isCash = string.Equals(paymentMethod, PaymentMethodCash, StringComparison.OrdinalIgnoreCase);
        var isBank = string.Equals(paymentMethod, PaymentMethodBankTransfer, StringComparison.OrdinalIgnoreCase);
        var updated = ReplaceLegacyPaymentCheckboxMarkers(templateContent, isCash, isBank);
        updated = ReplacePaymentClauseCheckboxes(updated, isCash, isBank);
        return updated;
    }

    private static string? ResolvePaymentMethodFromContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var cashChecked =
            Regex.IsMatch(content, @"Trả\s*lương\s*bằng\s*:\s*\[\s*x\s*\]\s*Tiền\s*mặt", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(content, @"Tra\s*luong\s*bang\s*:\s*\[\s*x\s*\]\s*Tien\s*mat", RegexOptions.IgnoreCase);

        var bankChecked =
            Regex.IsMatch(content, @"Tiền\s*mặt\s*\.\s*\[\s*x\s*\]\s*Chuyển\s*khoản\s*:", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(content, @"Tien\s*mat\s*\.\s*\[\s*x\s*\]\s*Chuyen\s*khoan\s*:", RegexOptions.IgnoreCase);

        if (cashChecked)
            return PaymentMethodCash;
        if (bankChecked)
            return PaymentMethodBankTransfer;
        return null;
    }

    private static string ReplacePaymentClauseCheckboxes(string content, bool isCash, bool isBank)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var cashMarker = isCash ? "[x]" : "[ ]";
        var bankMarker = isBank ? "[x]" : "[ ]";

        var updated = Regex.Replace(
            content,
            @"Trả\s*lương\s*bằng\s*:\s*(?:\[[xX ]\]\s*)?Tiền\s*mặt\s*\.\s*(?:\[[xX ]\]\s*)?Chuyển\s*khoản\s*:",
            $"Trả lương bằng: {cashMarker} Tiền mặt. {bankMarker} Chuyển khoản:",
            RegexOptions.IgnoreCase);

        if (!ReferenceEquals(updated, content) && !string.Equals(updated, content, StringComparison.Ordinal))
            return updated;

        return Regex.Replace(
            content,
            @"Tra\s*luong\s*bang\s*:\s*(?:\[[xX ]\]\s*)?Tien\s*mat\s*\.\s*(?:\[[xX ]\]\s*)?Chuyen\s*khoan\s*:",
            $"Tra luong bang: {cashMarker} Tien mat. {bankMarker} Chuyen khoan:",
            RegexOptions.IgnoreCase);
    }

    private static void EnsureNoPendingOwnedPlaceholders(string content, List<string> ownedFields, string roleName)
    {
        var pending = ExtractPlaceholderKeys(content)
            .Where(token => ownedFields.Contains(token, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (pending.Count > 0)
            throw new InvalidOperationException($"{roleName} chua dien day du thong tin: {string.Join(", ", pending)}.");
    }

    private static string ReplaceToken(string content, string token, string replacement)
    {
        var pattern = @"\{\{\s*" + Regex.Escape(token) + @"\s*\}\}";
        var safeReplacement = replacement?.Replace("$", "$$") ?? string.Empty;
        return Regex.Replace(content, pattern, safeReplacement, RegexOptions.IgnoreCase);
    }

    private static List<string> ExtractPlaceholderKeys(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var matches = Regex.Matches(content, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}");
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                continue;
            result.Add(key);
        }

        return result;
    }

    private static string NormalizeFieldValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim();
    }

    private static string ResolveCurrentUserRole(Contract contract, Guid userId)
    {
        var parentUserId = contract.HiringRecord?.ParentProfile?.UserId;
        var nannyUserId = contract.HiringRecord?.NannyProfile?.UserId;

        if (parentUserId == userId)
            return "Parent";
        if (nannyUserId == userId)
            return "Nanny";

        throw new UnauthorizedAccessException("Ban khong co quyen xem hop dong nay.");
    }

    private static ContractListItemDto MapToListItem(Contract contract)
    {
        var hiring = contract.HiringRecord;
        var parent = hiring?.ParentProfile?.User;
        var nanny = hiring?.NannyProfile?.User;
        var posting = hiring?.JobApplication?.JobPosting;

        return new ContractListItemDto
        {
            ContractId = contract.Id,
            HiringRecordId = hiring?.Id ?? Guid.Empty,
            JobTitle = posting?.Title ?? "-",
            ParentName = GetDisplayName(parent),
            ParentAvatar = parent?.AvatarUrl,
            NannyName = GetDisplayName(nanny),
            NannyAvatar = nanny?.AvatarUrl,
            StartDate = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate = hiring?.EndDate,
            HiringStatus = hiring?.Status ?? 0,
            ContractStatus = contract.Status,
            SignedByParent = contract.SignedByParent,
            SignedByNanny = contract.SignedByNanny,
            CreatedAt = contract.CreatedAt
        };
    }

    private static ContractDetailDto MapToDetail(Contract contract, string currentUserRole)
    {
        var hiring = contract.HiringRecord;
        var parent = hiring?.ParentProfile?.User;
        var nanny = hiring?.NannyProfile?.User;
        var posting = hiring?.JobApplication?.JobPosting;
        var effectiveContent = string.IsNullOrWhiteSpace(contract.ContractContent)
            ? contract.ContractTemplate?.Content ?? string.Empty
            : contract.ContractContent;
        var templateContent = contract.ContractTemplate?.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateContent))
            templateContent = effectiveContent;
        var editableTemplateContent = EnsureEditableContactTokens(templateContent);

        var isEditableByCurrentRole =
            (string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) && contract.Status is 0 or 1 or 2) ||
            (string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase) && contract.Status is 1 or 2);
        var normalizedContent = isEditableByCurrentRole
            ? editableTemplateContent
            : effectiveContent;
        if (isEditableByCurrentRole)
            normalizedContent = SyncPaymentMarkersFromPersistedContent(normalizedContent, effectiveContent);
        var placeholders = ExtractPlaceholderKeys(normalizedContent);
        var editableFields = BuildEditableFields(currentUserRole, contract.Status, placeholders, normalizedContent);
        var persistedPlaceholders = ExtractPlaceholderKeys(contract.ContractContent ?? string.Empty);
        var parentPending = persistedPlaceholders.Any(token => ParentEditableFields.Contains(token));
        var nannyPending = persistedPlaceholders.Any(token => NannyEditableFields.Contains(token));
        var scheduleFromContract = ExtractWorkingTimeSlotsFromContent(contract.ContractContent);
        var scheduleFromPosting = posting?.JobScheduleRequirements?
            .Where(s => !s.IsDeleted && s.IsRequired)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.TimeSlot)
            .Select(s => new ContractScheduleSlotDto
            {
                DayOfWeek = s.DayOfWeek,
                TimeSlot = s.TimeSlot
            })
            .ToList() ?? [];
        var scheduleSlots = scheduleFromContract.Count > 0 ? scheduleFromContract : scheduleFromPosting;
        var resolvedContractContent = ApplyJobDescription(normalizedContent, posting?.Description);
        resolvedContractContent = ApplyHiringDates(
            resolvedContractContent,
            hiring?.StartDate ?? DateOnly.MinValue,
            hiring?.EndDate);
        var draftFieldValues = ExtractDraftFieldValues(editableTemplateContent, effectiveContent);

        return new ContractDetailDto
        {
            ContractId = contract.Id,
            HiringRecordId = hiring?.Id ?? Guid.Empty,
            TemplateName = contract.ContractTemplate?.Name ?? string.Empty,
            JobTitle = posting?.Title ?? "-",
            StartDate = hiring?.StartDate ?? DateOnly.MinValue,
            EndDate = hiring?.EndDate,
            ContractDuration = hiring?.ContractDuration,
            ParentName = GetDisplayName(parent),
            ParentAvatar = parent?.AvatarUrl,
            ParentPhone = parent?.PhoneNumber,
            ParentEmail = parent?.Email,
            ParentAddress = parent?.Address,
            NannyName = GetDisplayName(nanny),
            NannyAvatar = nanny?.AvatarUrl,
            NannyPhone = nanny?.PhoneNumber,
            NannyEmail = nanny?.Email,
            NannyAddress = nanny?.Address,
            ContractContent = resolvedContractContent,
            SignedByParent = contract.SignedByParent,
            SignedByNanny = contract.SignedByNanny,
            SignedAt = contract.SignedAt,
            HiringStatus = hiring?.Status ?? 0,
            ContractStatus = contract.Status,
            CreatedAt = contract.CreatedAt,
            CurrentUserRole = currentUserRole,
            EditableFields = editableFields,
            DraftFieldValues = draftFieldValues,
            CanSubmitByNanny = string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase) &&
                               contract.Status is 1 or 2 &&
                               !parentPending,
            CanFinalConfirmByParent = string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) &&
                                      contract.Status == 2 &&
                                      contract.SignedByParent &&
                                      contract.SignedByNanny &&
                                      !parentPending &&
                                      !nannyPending,
            ScheduleSlots = scheduleSlots
        };
    }

    private static List<ContractScheduleSlotDto> ExtractWorkingTimeSlotsFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var sectionMatch = Regex.Match(content, @"(?is)4\.1\.[^\r\n]*\r?\n(?<body>.*?)(?=\r?\n\s*4\.2\.)");
        if (!sectionMatch.Success)
            return [];

        var body = sectionMatch.Groups["body"].Value;
        if (string.IsNullOrWhiteSpace(body))
            return [];

        var dayIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Thu 2"] = 0,
            ["Thu 3"] = 1,
            ["Thu 4"] = 2,
            ["Thu 5"] = 3,
            ["Thu 6"] = 4,
            ["Thu 7"] = 5,
            ["Chu nhat"] = 6
        };
        var slotIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sang"] = 0,
            ["Chieu"] = 1,
            ["Toi"] = 2,
            ["Dem"] = 3
        };

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ContractScheduleSlotDto>();
        var lines = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var normalized = line.StartsWith("-") ? line[1..].Trim() : line.Trim();
            var colonIndex = normalized.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var dayText = normalized[..colonIndex].Trim();
            if (!dayIndex.TryGetValue(dayText, out var dayOfWeek))
                continue;

            var slotText = normalized[(colonIndex + 1)..];
            var slotParts = slotText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var slotPart in slotParts)
            {
                if (!slotIndex.TryGetValue(slotPart.Trim(), out var timeSlot))
                    continue;

                var key = $"{dayOfWeek}-{timeSlot}";
                if (!unique.Add(key))
                    continue;

                result.Add(new ContractScheduleSlotDto
                {
                    DayOfWeek = dayOfWeek,
                    TimeSlot = timeSlot
                });
            }
        }

        return result
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.TimeSlot)
            .ToList();
    }

    private static string EnsureEditableContactTokens(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var updated = content;
        updated = EnsureNthLineToken(
            updated,
            @"(?im)^(?<prefix>\s*Ông\/bà\s*:\s*).*$",
            1,
            "{{ParentName}}");
        updated = EnsureNthLineToken(
            updated,
            @"(?im)^(?<prefix>\s*Ông\/bà\s*:\s*).*$",
            2,
            "{{NannyName}}");

        updated = EnsureNthLineToken(
            updated,
            @"(?im)^(?<prefix>\s*(?:Điện thoại liên hệ|Dien thoai lien he)\s*:\s*).*$",
            1,
            "{{ParentPhone}}. Email: {{ParentEmail}}");
        updated = EnsureNthLineToken(
            updated,
            @"(?im)^(?<prefix>\s*(?:Điện thoại liên hệ|Dien thoai lien he)\s*:\s*).*$",
            2,
            "{{NannyPhone}}. Email: {{NannyEmail}}");

        return updated;
    }

    private static string EnsureNthLineToken(string source, string linePattern, int nth, string replacementValue)
    {
        if (source.Contains(replacementValue, StringComparison.OrdinalIgnoreCase))
            return source;

        var matches = Regex.Matches(source, linePattern);
        if (matches.Count < nth || nth <= 0)
            return source;

        var target = matches[nth - 1];
        var prefix = target.Groups["prefix"].Success ? target.Groups["prefix"].Value : string.Empty;
        var replacement = $"{prefix}{replacementValue}";
        return source.Remove(target.Index, target.Length).Insert(target.Index, replacement);
    }

    private static Dictionary<string, string> ExtractDraftFieldValues(string? templateContentRaw, string? storedContentRaw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(templateContentRaw) || string.IsNullOrWhiteSpace(storedContentRaw))
            return result;

        var templateContent = templateContentRaw.Replace("\r\n", "\n");
        var storedContent = storedContentRaw.Replace("\r\n", "\n");
        var templateLines = templateContent.Split('\n');
        var storedLines = storedContent.Split('\n');
        var searchStartLineIndex = 0;

        foreach (var templateLine in templateLines)
        {
            var placeholderMatches = Regex.Matches(templateLine, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}");
            if (placeholderMatches.Count == 0)
                continue;

            var linePatternBuilder = new System.Text.StringBuilder();
            var keys = new List<string>();
            var lastIndex = 0;

            for (var i = 0; i < placeholderMatches.Count; i++)
            {
                var match = placeholderMatches[i];
                keys.Add(match.Groups[1].Value.Trim());

                var literal = templateLine[lastIndex..match.Index];
                linePatternBuilder.Append(BuildFlexibleLiteralPattern(literal));
                linePatternBuilder.Append($"(?<G{i}>.*?)");
                lastIndex = match.Index + match.Length;
            }

            linePatternBuilder.Append(BuildFlexibleLiteralPattern(templateLine[lastIndex..]));
            var linePattern = $"^{linePatternBuilder}$";

            Match? matchedLine = null;
            var matchedLineIndex = -1;
            for (var i = searchStartLineIndex; i < storedLines.Length; i++)
            {
                var currentMatch = Regex.Match(storedLines[i], linePattern, RegexOptions.Singleline);
                if (!currentMatch.Success)
                    continue;

                matchedLine = currentMatch;
                matchedLineIndex = i;
                break;
            }

            if (matchedLine == null || matchedLineIndex < 0)
                continue;

            searchStartLineIndex = matchedLineIndex + 1;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (!IsDraftExtractableField(key))
                    continue;

                var value = SanitizeExtractedFieldValue(key, matchedLine.Groups[$"G{i}"].Value);
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (!result.ContainsKey(key))
                    result[key] = value;
            }
        }

        if (!result.ContainsKey("PaymentMethod"))
        {
            var cashChecked = result.TryGetValue("PaymentMethodCashChecked", out var cashMarker) &&
                              cashMarker.Contains("[x]", StringComparison.OrdinalIgnoreCase);
            var bankChecked = result.TryGetValue("PaymentMethodBankChecked", out var bankMarker) &&
                              bankMarker.Contains("[x]", StringComparison.OrdinalIgnoreCase);

            if (cashChecked) result["PaymentMethod"] = PaymentMethodCash;
            else if (bankChecked) result["PaymentMethod"] = PaymentMethodBankTransfer;
        }

        PopulateBankFallbackValues(result, storedContent);

        return result;
    }

    private static void PopulateBankFallbackValues(Dictionary<string, string> result, string storedContent)
    {
        if (string.IsNullOrWhiteSpace(storedContent))
            return;

        if (result.ContainsKey("BankAccountNumber") && result.ContainsKey("BankName"))
            return;

        var bankLine = Regex.Match(
            storedContent,
            @"Số\s*tài\s*khoản\s*:\s*(?<acc>[^.\r\n]+?)\s*\.\s*Ngân\s*hàng\s*:\s*(?<bank>[^\r\n.]+)",
            RegexOptions.IgnoreCase);

        if (!bankLine.Success)
        {
            bankLine = Regex.Match(
                storedContent,
                @"So\s*tai\s*khoan\s*:\s*(?<acc>[^.\r\n]+?)\s*\.\s*Ngan\s*hang\s*:\s*(?<bank>[^\r\n.]+)",
                RegexOptions.IgnoreCase);
        }

        if (!bankLine.Success)
            return;

        var account = SanitizeExtractedFieldValue("BankAccountNumber", bankLine.Groups["acc"].Value);
        var bank = SanitizeExtractedFieldValue("BankName", bankLine.Groups["bank"].Value);
        if (!string.IsNullOrWhiteSpace(account) && !result.ContainsKey("BankAccountNumber"))
            result["BankAccountNumber"] = account;
        if (!string.IsNullOrWhiteSpace(bank) && !result.ContainsKey("BankName"))
            result["BankName"] = bank;
    }

    private static bool IsDraftExtractableField(string key) =>
        ParentEditableFields.Contains(key) ||
        NannyEditableFields.Contains(key) ||
        string.Equals(key, WorkingTimeSlotsField, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeExtractedFieldValue(string key, string? rawValue)
    {
        var value = (rawValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (Regex.IsMatch(value, @"^\{\{\s*[A-Za-z0-9_]+\s*\}\}$"))
            return string.Empty;

        if (string.Equals(key, "BankAccountNumber", StringComparison.OrdinalIgnoreCase))
        {
            value = Regex.Replace(
                value,
                @"(?is)\s*(Ng[aâ]n\s*h[aà]ng|Ngan\s*hang)\s*:.*$",
                string.Empty).Trim();
            return value.Trim('.', ',', ';');
        }

        if (string.Equals(key, "BankName", StringComparison.OrdinalIgnoreCase))
        {
            value = Regex.Replace(
                value,
                @"(?is)\s*(Th[oơ]i\s*h[aạ]n\s*tr[aả]\s*l[uư][oơ]ng|Thoi\s*han\s*tra\s*luong)\s*:.*$",
                string.Empty).Trim();
            return value.Trim('.', ',', ';');
        }

        return value;
    }

    private static string BuildFlexibleLiteralPattern(string literal)
    {
        if (string.IsNullOrEmpty(literal))
            return string.Empty;

        var builder = new System.Text.StringBuilder();
        foreach (var ch in literal)
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append(@"\s*");
                continue;
            }

            builder.Append(Regex.Escape(ch.ToString()));
        }

        return builder.ToString();
    }

    private static List<string> BuildEditableFields(string currentUserRole, int contractStatus, List<string> placeholders, string content)
    {
        if (string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) && contractStatus is 0 or 1 or 2)
        {
            var fields = placeholders.Where(token => ParentEditableFields.Contains(token)).ToList();
            var hasPaymentSection =
                content.Contains("(cái checkbox của option này ở đây)", StringComparison.OrdinalIgnoreCase) ||
                HasPaymentClause(content) ||
                placeholders.Contains("BankAccountNumber", StringComparer.OrdinalIgnoreCase) ||
                placeholders.Contains("BankName", StringComparer.OrdinalIgnoreCase) ||
                placeholders.Contains("PaymentMethod", StringComparer.OrdinalIgnoreCase) ||
                placeholders.Contains("PaymentMethodCashChecked", StringComparer.OrdinalIgnoreCase) ||
                placeholders.Contains("PaymentMethodBankChecked", StringComparer.OrdinalIgnoreCase);

            if (hasPaymentSection)
            {
                if (!fields.Contains("PaymentMethod", StringComparer.OrdinalIgnoreCase))
                    fields.Add("PaymentMethod");
                if (!fields.Contains("BankAccountNumber", StringComparer.OrdinalIgnoreCase))
                    fields.Add("BankAccountNumber");
                if (!fields.Contains("BankName", StringComparer.OrdinalIgnoreCase))
                    fields.Add("BankName");
            }

            return fields;
        }

        if (string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase) && contractStatus is 1 or 2)
            return placeholders.Where(token => NannyEditableFields.Contains(token)).ToList();

        return [];
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null)
            return "Nguoi dung";

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
