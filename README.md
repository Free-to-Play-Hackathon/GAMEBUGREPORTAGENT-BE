# Game Bug Repro Agent - Backend (Phase 1)

Dự án Game Bug Repro Agent Backend được thiết kế theo mô hình Clean Architecture sử dụng .NET 10 và C#. Giai đoạn 1 (Phase 1) tập trung vào việc xây dựng hạ tầng Intake vững chắc cho việc tiếp nhận báo cáo lỗi game từ người chơi.

## 🚀 Các Tính Năng Đã Hoàn Thành (Phase 1)
- **Cấu trúc Clean Architecture**: Chia tách rõ ràng giữa các tầng Domain, Application, Infrastructure, Api, Worker và các dự án Test tương ứng.
- **Idempotency (Độ tin cậy cao)**: Cơ chế chống gửi trùng báo cáo bằng việc băm trường thông tin canonical cùng checksum của file đính kèm. Tránh lỗi trùng lặp khi mất kết nối mạng.
- **Bảo mật & Xác thực file**: Kiểm tra định dạng đuôi mở rộng và magic bytes ảnh (PNG/JPEG), chặn các file độc hại, giới hạn số lượng và kích thước tệp tin khi upload trực tiếp.
- **Hạ tầng Docker**: Cung cấp sẵn file `docker-compose.yml` chạy PostgreSQL 16 (hỗ trợ pgvector extension) và MinIO (Object Storage) tự động tạo bucket khi khởi động.
- **Hệ thống Kiểm thử tự động**: Đã viết đầy đủ Unit Tests kiểm thử nghiệp vụ Domain, Application, và Architecture Tests xác thực tính cô lập giữa các Layer trong Clean Architecture.

---

## 🛠️ Yêu Cầu Hệ Thống
- **.NET SDK**: `10.0.301` hoặc mới hơn.
- **Docker Desktop** (hoặc Docker Engine + Compose).

---

## ⚙️ Hướng Dẫn Chạy Dự Án

### 1. Khởi động Hạ tầng cục bộ (Database & Storage)
Chạy lệnh sau để khởi động PostgreSQL và MinIO:
```bash
docker compose -f deploy/docker-compose.yml up -d
```
*Lưu ý: Đảm bảo Docker Desktop đã được bật trước khi chạy.*

### 2. Cập nhật Database (EF Core Migrations)
Để áp dụng cấu trúc bảng cơ sở dữ liệu mới nhất:
```bash
dotnet ef database update --project src/GameBug.Infrastructure --startup-project src/GameBug.Api
```

### 3. Build & Chạy API Server
Để chạy API server lắng nghe các request từ client:
```bash
dotnet run --project src/GameBug.Api
```
API sẽ có sẵn tại các URL hiển thị trên terminal (ví dụ: `https://localhost:7001` hoặc `http://localhost:5001`). Bạn có thể truy cập tài liệu Swagger tại đường dẫn: `https://localhost:7001/openapi/v1.json` (hoặc `/swagger` tùy cấu hình).

---

## 🧪 Chạy Kiểm Thử (Testing)
Dự án sử dụng xUnit, FluentAssertions và NSubstitute cho việc kiểm thử tự động.
Để chạy toàn bộ các bài kiểm thử:
```bash
dotnet test
```

Chạy riêng từng tầng:

```bash
dotnet test tests/GameBug.Domain.UnitTests
dotnet test tests/GameBug.Application.UnitTests
dotnet test tests/GameBug.ArchitectureTests
dotnet test tests/GameBug.IntegrationTests
dotnet test tests/GameBug.Api.FunctionalTests
```

---

## 🎬 Kịch Bản Demo Tự Động (Runbook / Automated Demo)
Để kiểm tra trực tiếp luồng hoạt động tiếp nhận lỗi game cùng cơ chế Idempotency, bạn có thể sử dụng file script PowerShell `demo.ps1` có sẵn:

1. Chạy API Server trước:
   ```bash
   dotnet run --project src/GameBug.Api
   ```
2. Mở một terminal mới và chạy:
   ```powershell
   ./demo.ps1
   ```
Script sẽ tự động:
- Kiểm tra trạng thái API.
- Tạo tệp đính kèm giả lập (với PNG magic bytes hợp lệ).
- Gửi báo cáo lỗi game lần đầu (mong đợi `201 Created`).
- Gửi lại chính xác request cũ để chứng minh cơ chế **Replay** (mong đợi trùng `Report ID`).
- Gửi request mới sử dụng lại Idempotency Key nhưng đổi nội dung để chứng minh cơ chế **Conflict** (mong đợi `409 Conflict`).
- Truy vấn lấy thông tin chi tiết của Bug Report vừa tạo.

## Cấu hình local và reset dữ liệu

Tạo file môi trường local trước khi khởi động container:

```powershell
Copy-Item deploy/.env.example deploy/.env
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d postgres minio createbuckets
docker compose --env-file deploy/.env -f deploy/docker-compose.yml ps
```

Chạy Worker bằng `dotnet run --project src/GameBug.Worker`.

Để reset dữ liệu local, dừng dịch vụ trước và chỉ xóa named volumes khi chắc chắn không cần dữ liệu:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml down --volumes
```

Không dùng thông tin xác thực trong `.env.example` cho production.

---

## 📌 Danh Sách API Endpoints

### 1. Tạo Báo Cáo Lỗi (Submit Bug Report)
- **Method**: `POST`
- **Path**: `/api/v1/bug-reports`
- **Content-Type**: `multipart/form-data`
- **Headers**:
  - `Idempotency-Key` (bắt buộc, độ dài từ 16 - 128 ký tự)
- **Form Data**:
  - `description` (bắt buộc, text, từ 10 - 10000 ký tự)
  - `buildVersion` (tùy chọn, tối đa 64 ký tự)
  - `platform` (tùy chọn, tối đa 128 ký tự)
  - `device` (tùy chọn, tối đa 128 ký tự)
  - `locale` (tùy chọn, tối đa 32 ký tự)
  - `sessionReference` (tùy chọn, tối đa 256 ký tự)
  - `attachments` (danh sách tệp đính kèm tối đa 5 file: PNG/JPEG max 8MB, TXT/LOG max 10MB)

### 2. Lấy Thông Tin Báo Cáo Lỗi
- **Method**: `GET`
- **Path**: `/api/v1/bug-reports/{reportId}`
- **Headers**:
  - `Authorization` (hoặc định danh người dùng mô phỏng qua Header/Context)
