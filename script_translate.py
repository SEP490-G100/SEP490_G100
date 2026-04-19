import os

base_path = r"d:\Package SEP490_G100\SEP490_G100\UI\WebSite\WebSite\Controllers"

replacements = {
    # Review
    'TempData["Error"] = "Chi co the danh gia sau khi hop dong hoan thanh."': 'TempData["Error"] = "Chỉ có thể đánh giá sau khi hợp đồng hoàn thành."',

    # Profile
    'TempData["Error"] = "Session expired. Please log in again."': 'TempData["Error"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."',
    'TempData["Error"] = "Error loading profile: "': 'TempData["Error"] = "Lỗi tải thông tin: "',
    'TempData["Error"] = "BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Â n xem danh sÃƒÂ¡ch con em."': 'TempData["Error"] = "Bạn không có quyền xem danh sách trẻ em."',
    'TempData["Error"] = "LÃ¡Â»â€”i khi tÃ¡ÂºÂ£i danh sÃƒÂ¡ch con em: "': 'TempData["Error"] = "Lỗi khi tải danh sách trẻ em: "',
    'TempData["Error"] = "LÃ¡Â»â€”i khi thÃƒÂªm con em: "': 'TempData["Error"] = "Lỗi khi thêm trẻ em: "',
    'TempData["Error"] = "LÃ¡Â»â€”i khi tÃ¡ÂºÂ£i thÃƒÂ´ng tin con em: "': 'TempData["Error"] = "Lỗi khi tải thông tin trẻ em: "',
    'TempData["Error"] = "LÃ¡Â»â€”i khi cÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t: "': 'TempData["Error"] = "Lỗi khi cập nhật: "',
    'TempData["Error"] = "LÃ¡Â»â€”i khi xÃƒÂ³a: "': 'TempData["Error"] = "Lỗi khi xóa: "',
    'TempData["Error"] = "Loi khi cap nhat: "': 'TempData["Error"] = "Lỗi khi cập nhật: "',
    'TempData["Success"] = "Cap nhat thong tin thanh cong."': 'TempData["Success"] = "Cập nhật thông tin thành công."',
    'TempData["Success"] = "Ä Ã£ thÃªm chá»©ng chá»‰ thÃ nh cÃ´ng."': 'TempData["Success"] = "Đã thêm chứng chỉ thành công."',
    'TempData["Success"] = "ThÃƒÂªm con em thÃƒÂ nh cÃƒÂ´ng."': 'TempData["Success"] = "Thêm trẻ em thành công."',
    'TempData["Success"] = "CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t thÃƒÂ´ng tin con em thÃƒÂ nh cÃƒÂ´ng."': 'TempData["Success"] = "Cập nhật thông tin trẻ em thành công."',
    'TempData["Success"] = "XÃƒÂ³a con em thÃƒÂ nh cÃƒÂ´ng."': 'TempData["Success"] = "Xóa trẻ em thành công."',
    'ViewBag.Warning = "PhiÃƒÂªn Ã„â€˜Ã„Æ’ng nhÃ¡ÂºÂ­p Ã„â€˜ÃƒÂ£ hÃ¡ÂºÂ¿t hÃ¡ÂºÂ¡n, Ã„â€˜ang hiÃ¡Â»Æ’n thÃ¡Â»â€¹ thÃƒÂ´ng tin cÃ†Â¡ bÃ¡ÂºÂ£n tÃ¡Â»Â« cookie."': 'ViewBag.Warning = "Phiên đăng nhập đã hết hạn, đang hiển thị thông tin cơ bản từ cookie."',
    'ViewBag.Warning = "Could not load profile from API, showing basic info from cookie."': 'ViewBag.Warning = "Không thể tải hồ sơ từ API, đang hiển thị thông tin cơ bản từ cookie."',
    'ViewBag.Warning = "Could not load full profile, showing basic info from cookie."': 'ViewBag.Warning = "Không thể tải toàn bộ hồ sơ, đang hiển thị thông tin cơ bản từ cookie."',
    'KhÃ´ng thá»ƒ thÃªm chá»©ng chá»‰.': 'Không thể thêm chứng chỉ.',
    'ThÃƒÂªm con em thÃ¡ÂºÂ¥t bÃ¡ÂºÂ¡i.': 'Thêm trẻ em thất bại.',
    'CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t thÃ¡ÂºÂ¥t bÃ¡ÂºÂ¡i.': 'Cập nhật thất bại.',
    'XÃƒÂ³a thÃ¡ÂºÂ¥t bÃ¡ÂºÂ¡i.': 'Xóa thất bại.',
    'Khong the upload avatar: ': 'Không thể tải lên ảnh đại diện: ',
    'Luong khong hop le.': 'Lương không hợp lệ.',
    'Chi cho phep anh .jpg, .jpeg hoac .png.': 'Chỉ cho phép ảnh .jpg, .jpeg hoặc .png.',
    'Vui long chon ngay sinh.': 'Vui lòng chọn ngày sinh.',
    'Nanny phai lon hon 30 tuoi.': 'Bảo mẫu phải lớn hơn 30 tuổi.',

    # ModeratorVerificationController
    'TempData["Error"] = "Khong the tai danh sach xac minh."': 'TempData["Error"] = "Không thể tải danh sách xác minh."',
    'TempData["Error"] = "Khong tim thay yeu cau xac minh."': 'TempData["Error"] = "Không tìm thấy yêu cầu xác minh."',
    'TempData["Error"] = "Loi ket noi den API."': 'TempData["Error"] = "Lỗi kết nối đến API."',

    # ModeratorJobController
    'TempData["Error"] = "Could not fetch job postings."': 'TempData["Error"] = "Không thể tải danh sách tin đăng."',
    'TempData["Error"] = "Could not load the job posting detail."': 'TempData["Error"] = "Không thể tải chi tiết tin đăng."',
    'TempData["Error"] = "Review failed: "': 'TempData["Error"] = "Kiểm duyệt thất bại: "',

    # ModeratorFaqController
    'TempData["Error"] = "Khong the tai danh sach FAQ."': 'TempData["Error"] = "Không thể tải danh sách FAQ."',
    'TempData["Error"] = "Khong tim thay FAQ."': 'TempData["Error"] = "Không tìm thấy FAQ."',

    # ModeratorDashboardController
    'TempData["Error"] = "Khong the tai du lieu dashboard moderator."': 'TempData["Error"] = "Không thể tải dữ liệu bảng điều khiển."',

    # ModeratorComplainController
    'TempData["Error"] = "Cannot load complaint list."': 'TempData["Error"] = "Không thể tải danh sách khiếu nại."',
    'TempData["Error"] = "Complaint not found."': 'TempData["Error"] = "Không tìm thấy khiếu nại."',
    'TempData["Error"] = "Complaint already completed. Resolution cannot be edited."': 'TempData["Error"] = "Khiếu nại đã giải quyết. Biện pháp không thể chỉnh sửa."',

    # ModeratorAccountController
    'TempData["Error"] = "Khong the tai danh sach tai khoan. Vui long thu lai."': 'TempData["Error"] = "Không thể tải danh sách tài khoản. Vui lòng thử lại."',
    'TempData["Error"] = "Khong tim thay tai khoan."': 'TempData["Error"] = "Không tìm thấy tài khoản."',

    # BlogCategoryController
    'TempData["Error"] = "Khong the tai danh sach danh muc."': 'TempData["Error"] = "Không thể tải danh sách danh mục."',
    'TempData["Error"] = "Khong tim thay danh muc."': 'TempData["Error"] = "Không tìm thấy danh mục."',

    # AdminSubcriptionPlanController
    'TempData["Error"] = "Khong the tai danh sach subscription plan."': 'TempData["Error"] = "Không thể tải danh sách gói dịch vụ."',

    # AdminNotificationController
    'TempData["Error"] = "Khong the tai danh sach thong bao admin."': 'TempData["Error"] = "Không thể tải danh sách thông báo."',
}

updated_files = 0
for filename in os.listdir(base_path):
    if filename.endswith(".cs"):
        filepath = os.path.join(base_path, filename)
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
        
        original_content = content
        for eng, viet in replacements.items():
            content = content.replace(eng, viet)
            
        if content != original_content:
            with open(filepath, "w", encoding="utf-8") as f:
                f.write(content)
            updated_files += 1
            print(f"Updated: {filename}")

print(f"Done processing! Files modified: {updated_files}")
