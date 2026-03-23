# SEP490_G100 - Nền tảng tìm người giữ trẻ tại nhà

SEP490 Nhóm 100 – Thiết kế & Phát triển Hệ thống Kết nối Việc làm Người giữ trẻ tại nhà
Công nghệ sử dụng: ASP.NET Core Web API (.NET 8), ASP.NET MVC/Razor UI, SQL Server / Azure SQL, JWT, Swagger, GitHub Actions CI.

---

## Cấu trúc kho lưu trữ
- API Backend: `Backend/Nanny_BackEnd` (ASP.NET Core Web API, JWT, Swagger)

- Giao diện người dùng web: `UI/WebSite` (ASP.NET MVC/Razor)

---

## Điều kiện tiên quyết
- .NET SDK 8.x
- SQL Server (cục bộ) hoặc Azure SQL Database
- Visual Studio 2022

---

## Chạy Backend (Swagger)

Mở giải pháp:

- `Backend/Nanny_BackEnd/Nanny_BackEnd.sln`

Chạy và mở:

- https://localhost:5001/swagger

---

## Chạy UI (MVC/Razor)

Mở giải pháp:

- `UI/WebSite/WebSite.sln`

Giao diện người dùng sẽ gọi API Backend (cấu hình URL cơ sở trong cài đặt giao diện người dùng nếu cần).

---

## Cấu hình
### Phần Backend
- Chuỗi kết nối trong `appsettings.json` / `appsettings.Development.json`
- Cài đặt JWT (ví dụ):

- Issuer

- Audience

- SecretKey

- ExpireMinutes

> KHÔNG được phép đưa các thông tin bí mật thực sự (chuỗi kết nối sản xuất, khóa bí mật).

### Cấu hình cục bộ được đề xuất
- Sử dụng `appsettings.Development.json` cho cấu hình chỉ cục bộ
- Lưu trữ thông tin bí mật trong User Secrets / Environment Variables / GitHub Secrets

---

## Quy trình tạo nhánh và yêu cầu kéo (Pull Request)

- `main`: nhánh ổn định (được bảo vệ)

- `develop`: nhánh tích hợp

- Các nhánh làm việc:

- `feature/<short-desc>`

- `fix/<short-desc>`

- `chore/<short-desc>`

### Cách đóng góp

1. Tạo nhánh mới từ `develop`
2. Commit và đẩy nhánh của bạn lên
3. Mở yêu cầu kéo (PR) đến `develop`
4. CI phải vượt qua (`build`) + ít nhất 1 phê duyệt
5. Hợp nhất yêu cầu kéo (PR)

### Phát hành/Demo
- Mở yêu cầu kéo (PR) từ `develop` -> `main`
- CI phải vượt qua trước khi hợp nhất

---

## Quy trình CI (GitHub Actions): `.github/workflows/ci.yml`
CI chạy khi có push/PR vào thư mục `develop` và `main` và xây dựng cả Backend và UI.

---
## Tài liệu
- Hướng dẫn đóng góp: `CONTRIBUTING.md`
- Vấn đề: sử dụng mẫu vấn đề trong `.github/ISSUE_TEMPLATE/`

---