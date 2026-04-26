using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class ContractService : IContractService
{
    private readonly IContractRepository _repo;

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

        if (contract.Status != 0)
            throw new InvalidOperationException("Hợp đồng không ở trạng thái chờ bố mẹ xác nhận thông tin.");

        ValidateParentFillRequest(request);

        var values = BuildParentValues(request);
        contract.ContractContent = ApplyTemplateValues(contract.ContractContent ?? string.Empty, values);
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

        if (contract.Status != 1)
            throw new InvalidOperationException("Hợp đồng không ở trạng thái chờ bảo mẫu xác nhận thông tin.");

        ValidateNannyFillRequest(request);

        var values = BuildNannyValues(request);
        contract.ContractContent = ApplyTemplateValues(contract.ContractContent ?? string.Empty, values);
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
        var generatedText = string.IsNullOrWhiteSpace(contract.ContractContent) ? "Hop dong." : contract.ContractContent!;
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
        var status = contract.Status;
        var canParentConfirmInfo = string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) && status == 0;
        var canNannyConfirmInfo = string.Equals(currentUserRole, "Nanny", StringComparison.OrdinalIgnoreCase) && status == 1;
        var canParentFinalConfirm = string.Equals(currentUserRole, "Parent", StringComparison.OrdinalIgnoreCase) && status == 2;
        var isReadOnly = status == 3 || (!canParentConfirmInfo && !canNannyConfirmInfo && !canParentFinalConfirm);
        var parentUser = contract.HiringRecord?.ParentProfile?.User;
        var nannyUser = contract.HiringRecord?.NannyProfile?.User;

        return new ContractDetailDto
        {
            ContractId = contract.Id,
            HiringRecordId = contract.HiringRecordId,
            ContractTemplateId = contract.ContractTemplateId,
            ContractContent = contract.ContractContent ?? string.Empty,
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

    private static void ValidateParentFillRequest(ContractParentFillRequestDto request)
    {
        Require(request.ParentName, "ParentName");
        Require(request.ParentDob, "ParentDOB");
        Require(request.ParentIdentityNumber, "ParentIdentityNumber");
        Require(request.ParentIdentityIssueDate, "ParentIdentityIssueDate");
        Require(request.ParentIdentityIssuePlace, "ParentIdentityIssuePlace");
        Require(request.ParentPermanentAddress, "ParentPermanentAddress");
        Require(request.ParentCurrentAddress, "ParentCurrentAddress");
        Require(request.ParentPhone, "ParentPhone");
        Require(request.ParentEmail, "ParentEmail");
        Require(request.WorkAddress, "WorkAddress");
        Require(request.SalaryAmount, "SalaryAmount");
        Require(request.SalaryReceivedDate, "SalaryReceivedDate");
    }

    private static void ValidateNannyFillRequest(ContractNannyFillRequestDto request)
    {
        Require(request.NannyName, "NannyName");
        Require(request.NannyDob, "NannyDOB");
        Require(request.NannyIdentityNumber, "NannyIdentityNumber");
        Require(request.NannyIdentityIssueDate, "NannyIdentityIssueDate");
        Require(request.NannyIdentityIssuePlace, "NannyIdentityIssuePlace");
        Require(request.NannyPermanentAddress, "NannyPermanentAddress");
        Require(request.NannyCurrentAddress, "NannyCurrentAddress");
        Require(request.NannyPhone, "NannyPhone");
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

    private static string ApplyTemplateValues(string content, IDictionary<string, string?> values)
    {
        var result = content ?? string.Empty;
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            var token = $"{{{{{pair.Key}}}}}";
            if (!result.Contains(token, StringComparison.OrdinalIgnoreCase))
                continue;

            var replacement = string.IsNullOrWhiteSpace(pair.Value) ? "..." : pair.Value!.Trim();
            result = result.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

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
