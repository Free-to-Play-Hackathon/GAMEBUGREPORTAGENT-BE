# AWS EC2 Small Deployment Guide - GameBug Repro Agent

Mục tiêu của guide này là deploy bản **test/demo rẻ và dễ vận hành nhất** cho dự án hiện tại.

**Chiến lược khuyến nghị**: 1 máy AWS EC2 `t3.small` chạy Docker Compose, host Nginx reverse proxy, PostgreSQL/pgvector và MinIO chạy cùng máy, image được build trực tiếp trên server từ source code.

**Không dùng trong đường chính**: ECR, ECS, RDS, S3, Secrets Manager, GitHub Actions deploy tự động. Những thứ đó chuẩn hơn cho production nhưng làm tăng chi phí và độ phức tạp. Nếu cần, phần cuối có mục optional.

---

## 0. Kiến trúc demo

```text
Internet
   |
   | HTTP/HTTPS
   v
EC2 Public IPv4 / Elastic IP
   |
   v
Host Nginx
   |
   | proxy_pass http://127.0.0.1:8080
   v
Docker Compose
   |
   |-- gamebug-api      (.NET API, exposes 127.0.0.1:8080)
   |-- gamebug-worker   (.NET background worker)
   |-- gamebug-postgres (PostgreSQL + pgvector)
   |-- gamebug-minio    (attachment object storage)
   `-- gamebug-minio-setup
```

### Vì sao cách này rẻ và dễ nhất

- Chỉ cần 1 EC2 instance loại nhỏ.
- Không cần ECR, không cần AWS IAM cho CI/CD lúc demo.
- Không cần managed database hoặc object storage riêng.
- Không expose PostgreSQL/MinIO ra internet.
- API chỉ expose qua Nginx.
- Evaluation endpoint chạy được vì environment dùng `Demo`, không phải `Production`.

### Trade-off cần biết

- Không phải high availability. Instance chết thì app tạm dừng.
- DB và object storage nằm cùng EBS volume của EC2, nên cần snapshot/backup nếu muốn giữ lại.
- Build image trên máy 2 GB RAM có thể chậm. Guide có thêm swap để tránh OOM.
- Phù hợp demo/hackathon/test, không phải production dài hạn.
- Nếu chỉ **stop** EC2 sau demo, phí compute dừng nhưng EBS volume và Elastic IP/public IPv4 liên quan vẫn có thể phát sinh phí. Muốn dừng gần như toàn bộ chi phí thì snapshot nếu cần, rồi terminate instance và release Elastic IP.

---

## 1. Chi phí dự kiến

Khuyến nghị bắt đầu với **EC2 `t3.small` hoặc `t3a.small` On-Demand, Ubuntu 24.04, region `ap-southeast-1`**:

| Hạng mục | Chi phí ước tính |
|---|---:|
| EC2 `t3.small`/`t3a.small` Linux, 2 vCPU, 2 GiB RAM | tính theo giờ, khoảng vài chục USD/tháng nếu chạy 24/7 tùy region |
| EBS gp3 30 GB | tính theo GB-tháng |
| Public IPv4 / Elastic IP | AWS tính phí public IPv4 theo giờ |
| EBS snapshot backup | tùy dung lượng snapshot thực tế |
| Domain riêng | tùy nhà cung cấp, Route 53 khoảng $0.50/tháng/hosted zone nếu dùng |
| OpenAI API | tính riêng theo usage |
| ECR | $0 vì guide chính không dùng ECR |

Gợi ý để rẻ:

- Dùng `t3.small` trước. Nếu build/evaluation thiếu RAM, đổi instance type lên `t3.medium` tạm thời.
- Dùng EBS gp3 30 GB là đủ cho demo; tăng lên 50-60 GB nếu build Docker nhiều lần.
- Nếu chưa cần domain/IP cố định, dùng public IP tự cấp của EC2. Nếu cần domain, dùng Elastic IP nhưng nhớ release sau demo.
- Sau demo: stop instance để ngừng phí compute; nếu không dùng nữa thì snapshot dữ liệu cần giữ, terminate instance và delete/release tài nguyên còn lại.

Nguồn AWS cần nhớ: EC2 On-Demand tính theo thời gian instance chạy; public IPv4 bị tính phí theo giờ; EBS tính theo dung lượng provisioned.

---

## 2. Chuẩn bị trong repo

Các file sau nên được commit vào repo:

```text
src/GameBug.Api/Dockerfile
src/GameBug.Worker/Dockerfile
.dockerignore
deploy/docker-compose.demo.yml
deploy/nginx/gamebug.conf
Codex-Plans/aws-deployment-guide.md
```

Các file sau **không commit**:

```text
.env
deploy/.env
/opt/gamebug/.env
*.pem
```

Repo hiện tại đã có `.env.example` để người khác copy ra `.env` và tự điền key. Khi deploy server cũng làm tương tự: tạo `/opt/gamebug/repo/deploy/.env` thủ công.

---

## 3. Tạo Dockerfile cho API

Tạo file `src/GameBug.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Api/GameBug.Api.csproj src/GameBug.Api/

RUN dotnet restore src/GameBug.Api/GameBug.Api.csproj

COPY src/ src/
RUN dotnet publish src/GameBug.Api/GameBug.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/evaluation/artifacts

COPY --from=build /app/publish ./
COPY evaluation ./evaluation
RUN chown -R $APP_UID:0 /app

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameBug.Api.dll"]
```

Điểm quan trọng: API image phải copy thư mục `evaluation/`. Nếu thiếu thư mục này, `/api/v1/evaluations` sẽ không đọc được manifest, cases, ground truth và artifact output.

---

## 4. Tạo Dockerfile cho Worker

Tạo file `src/GameBug.Worker/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Worker/GameBug.Worker.csproj src/GameBug.Worker/

RUN dotnet restore src/GameBug.Worker/GameBug.Worker.csproj

COPY src/ src/
RUN dotnet publish src/GameBug.Worker/GameBug.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
RUN chown -R $APP_UID:0 /app

USER $APP_UID
ENTRYPOINT ["dotnet", "GameBug.Worker.dll"]
```

---

## 5. Tạo `.dockerignore`

Tạo file `.dockerignore` ở root:

```dockerignore
.git
.github
.vs
.vscode
**/bin
**/obj
**/TestResults
**/.pytest_cache

.env
.env.*
!.env.example

deploy/.env
deploy/.env.*
!deploy/.env.example

evaluation/artifacts/*
!evaluation/artifacts/.gitkeep

*.user
*.suo
*.pem
*.key
```

Không ignore `evaluation/manifests`, `evaluation/cases`, `evaluation/ground-truth` vì API cần chúng trong image.

---

## 6. Test Docker build local

Chạy từ root repo:

```powershell
docker build -t gamebug-api:local -f src/GameBug.Api/Dockerfile .
docker build -t gamebug-worker:local -f src/GameBug.Worker/Dockerfile .
```

Nếu build fail do thiếu Docker Desktop hoặc thiếu network restore NuGet, sửa xong local trước rồi mới deploy lên AWS.

---

## 7. Tạo Docker Compose demo

Tạo file `deploy/docker-compose.demo.yml`:

```yaml
name: gamebug-demo

services:
  postgres:
    image: pgvector/pgvector:0.8.4-pg16-bookworm
    container_name: gamebug-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 10s
      timeout: 5s
      retries: 10

  minio:
    image: minio/minio:RELEASE.2025-09-07T16-13-09Z-cpuv1
    container_name: gamebug-minio
    restart: unless-stopped
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    command: server /data --console-address ":9001"
    ports:
      - "127.0.0.1:9000:9000"
      - "127.0.0.1:9001:9001"
    volumes:
      - miniodata:/data

  createbuckets:
    image: minio/mc:RELEASE.2025-08-13T08-35-41Z-cpuv1
    container_name: gamebug-minio-setup
    depends_on:
      - minio
    entrypoint: >
      /bin/sh -c "
      until /usr/bin/mc alias set local http://minio:9000 ${MINIO_ROOT_USER} ${MINIO_ROOT_PASSWORD}; do
        echo 'waiting for minio...';
        sleep 2;
      done;
      /usr/bin/mc mb --ignore-existing local/${MINIO_BUCKET};
      exit 0;
      "

  api:
    build:
      context: ..
      dockerfile: src/GameBug.Api/Dockerfile
    image: gamebug-api:demo
    container_name: gamebug-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Demo
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Database: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      ObjectStorage__Endpoint: minio:9000
      ObjectStorage__AccessKey: ${MINIO_ROOT_USER}
      ObjectStorage__SecretKey: ${MINIO_ROOT_PASSWORD}
      ObjectStorage__BucketName: ${MINIO_BUCKET}
      ObjectStorage__UseSsl: "false"
      ObjectStorage__TimeoutSeconds: "30"
      Ai__OpenAI__ApiKey: ${OPENAI_API_KEY}
      Ai__OpenAI__BaseUrl: https://api.openai.com/v1
      Ai__Routes__ReportUnderstanding__Model: ${OPENAI_MODEL_REPORT}
      Ai__Routes__ReproSynthesis__Model: ${OPENAI_MODEL_REPRO}
      Evaluation__AllowlistedManifests__0: demo-v1
      Evaluation__PerCaseTimeoutSeconds: "180"
    ports:
      - "127.0.0.1:8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_started

  worker:
    build:
      context: ..
      dockerfile: src/GameBug.Worker/Dockerfile
    image: gamebug-worker:demo
    container_name: gamebug-worker
    restart: unless-stopped
    environment:
      DOTNET_ENVIRONMENT: Demo
      ConnectionStrings__Database: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      ObjectStorage__Endpoint: minio:9000
      ObjectStorage__AccessKey: ${MINIO_ROOT_USER}
      ObjectStorage__SecretKey: ${MINIO_ROOT_PASSWORD}
      ObjectStorage__BucketName: ${MINIO_BUCKET}
      ObjectStorage__UseSsl: "false"
      ObjectStorage__TimeoutSeconds: "30"
      Ai__OpenAI__ApiKey: ${OPENAI_API_KEY}
      Ai__OpenAI__BaseUrl: https://api.openai.com/v1
      Ai__Routes__ReportUnderstanding__Model: ${OPENAI_MODEL_REPORT}
      Ai__Routes__ReproSynthesis__Model: ${OPENAI_MODEL_REPRO}
      Evaluation__WorkerHeartbeatIntervalSeconds: "30"
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_started

volumes:
  pgdata:
  miniodata:
```

Lý do không dùng Docker network `web` external: Nginx chạy trên host sẽ không resolve được DNS container như `api:8080`. Vì vậy API publish vào `127.0.0.1:8080`, rồi Nginx proxy vào localhost.

---

## 8. Tạo cấu hình Nginx

Tạo file `deploy/nginx/gamebug.conf`:

```nginx
server {
    listen 80;
    server_name _;

    client_max_body_size 64m;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 180s;
        proxy_send_timeout 180s;
    }
}
```

Nếu có domain và muốn HTTPS, Certbot sẽ tự thêm SSL config vào file này ở bước sau.

---

## 9. Tạo EC2 instance small

1. Vào AWS Console.
2. Chọn region **Asia Pacific (Singapore) `ap-southeast-1`** nếu bạn đang dùng region đó.
3. Mở **EC2** -> **Instances** -> **Launch instances**.
4. Name: `gamebug-demo-ec2`.
5. AMI: **Ubuntu Server 24.04 LTS (HVM), SSD Volume Type**.
6. Architecture: **64-bit x86** để đơn giản nhất với Docker images hiện tại.
7. Instance type:
   - Khuyến nghị: **`t3.small`** hoặc **`t3a.small`** (2 vCPU, 2 GiB RAM).
   - Nếu demo/evaluation bị thiếu RAM: stop instance rồi đổi lên **`t3.medium`**.
8. Key pair:
   - Chọn key pair có sẵn, hoặc tạo mới `gamebug-demo-key`.
   - Tải file `.pem` và giữ cẩn thận. Không commit vào repo.
9. Network settings:
   - VPC: default VPC cũng được cho demo.
   - Subnet: subnet public bất kỳ trong `ap-southeast-1`.
   - Auto-assign public IP: **Enable** nếu chỉ demo nhanh.
   - Nếu cần IP cố định/domain: sau khi tạo instance, allocate **Elastic IP** và associate vào instance.
10. Security group: tạo mới `gamebug-demo-sg`.

Inbound rules:

| Port | Mục đích | Source |
|---:|---|---|
| 22 | SSH | IP của bạn, ví dụ `x.x.x.x/32` |
| 80 | HTTP/Nginx | Anywhere |
| 443 | HTTPS/Nginx | Anywhere |

Không mở port 5432, 9000, 9001 ra internet. Compose chỉ bind chúng vào `127.0.0.1`.

Storage:

- Root volume: **30 GB gp3**.
- Encryption: bật mặc định nếu account có.
- Delete on termination: bật nếu đây chỉ là demo và bạn có backup/snapshot riêng khi cần.

Launch instance xong, ghi lại:

- Public IPv4 DNS hoặc Public IPv4 address.
- Elastic IP nếu bạn có allocate.
- Key pair `.pem`.

Lưu ý chi phí:

- EC2 public IPv4 và Elastic IP đều có thể bị tính phí theo giờ.
- Nếu chỉ stop instance, EBS volume vẫn còn tính phí.
- Nếu terminate instance, nhớ release Elastic IP nếu đã tạo.

---

## 10. Setup server

SSH vào server:

```bash
chmod 400 <gamebug-demo-key.pem>
ssh -i <gamebug-demo-key.pem> ubuntu@<ec2-public-ip-or-dns>
```

Cập nhật OS:

```bash
sudo apt update
sudo apt upgrade -y
```

Cài Docker:

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
newgrp docker

docker --version
docker compose version
```

Cài Git, Nginx, Certbot:

```bash
sudo apt install -y git nginx certbot python3-certbot-nginx curl
sudo systemctl enable nginx
sudo systemctl status nginx
```

Tạo swap 2 GB để build .NET image đỡ bị OOM:

```bash
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
free -h
```

---

## 11. Clone repo lên server

```bash
sudo mkdir -p /opt/gamebug
sudo chown ubuntu:ubuntu /opt/gamebug
cd /opt/gamebug

git clone https://github.com/Free-to-Play-Hackathon/GAMEBUGREPORTAGENT-BE.git repo
cd repo
```

Nếu repo private, dùng SSH deploy key hoặc GitHub token theo cách bạn đang dùng. Với demo, clone bằng HTTPS + token tạm thời cũng được, miễn không lưu token vào repo.

---

## 12. Tạo `.env` trên server

Tạo file `/opt/gamebug/repo/deploy/.env`:

```bash
nano /opt/gamebug/repo/deploy/.env
```

Nội dung mẫu:

```dotenv

POSTGRES_USER=gamebug_demo
POSTGRES_PASSWORD=replace-with-strong-password
POSTGRES_DB=gamebug_db


MINIO_ROOT_USER=gamebug_minio
MINIO_ROOT_PASSWORD=replace-with-strong-password
MINIO_BUCKET=gamebug-attachments

OPENAI_API_KEY=sk
OPENAI_MODEL_REPORT=gpt-4.1
OPENAI_MODEL_REPRO=gpt-4.1
```

Quyền file:

```bash
chmod 600 /opt/gamebug/repo/deploy/.env
```

Lưu ý:

- Không commit `.env`.
- Người khác dùng repo thì copy `.env.example` rồi tự điền key của họ.
- Server dùng file riêng ở `deploy/.env`.
- `POSTGRES_DB` nên giữ là `gamebug_db` vì seed command hiện tại chỉ cho phép demo/local DB.

---

## 13. Build image trên server

Chạy từ root repo:

```bash
cd /opt/gamebug/repo
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env build
```

Nếu build chậm trên `t3.small`, chờ là bình thường. Nếu fail do memory:

```bash
free -h
docker system df
docker builder prune
```

Sau đó thử build lại. Nếu vẫn OOM, stop EC2 rồi đổi instance type lên `t3.medium`, start lại và build tiếp.

---

## 14. Khởi động PostgreSQL và MinIO

```bash
cd /opt/gamebug/repo
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env up -d postgres minio createbuckets
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env ps
```

Kiểm tra:

```bash
docker exec gamebug-postgres pg_isready -U gamebug_demo -d gamebug_db
curl http://127.0.0.1:9000/minio/health/live
```

---

## 15. Chạy EF migrations

Cách rẻ và sạch nhất cho demo: dùng SDK container một lần, không cần cài .NET SDK trực tiếp lên host.

```bash
cd /opt/gamebug/repo

docker run --rm \
  --network gamebug-demo_default \
  --env-file deploy/.env \
  -v "$PWD:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc '
    dotnet tool install --global dotnet-ef --version 10.* &&
    export PATH="$PATH:/root/.dotnet/tools" &&
    dotnet restore src/GameBug.Api/GameBug.Api.csproj &&
    ConnectionStrings__Database="Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}" \
    dotnet ef database update \
      --project src/GameBug.Infrastructure \
      --startup-project src/GameBug.Api \
      --connection "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  '
```

Guide đã đặt Compose project name là `gamebug-demo`, nên network mặc định là `gamebug-demo_default`. Nếu bạn đổi `name:` trong compose file, xem network thật bằng:

```bash
docker network ls
```

Thường network có dạng `<compose-name>_default`.

Kiểm tra migration:

```bash
docker exec -it gamebug-postgres psql -U gamebug_demo -d gamebug_db -c '\dt'
```

---

## 16. Seed demo data

Seed bằng chính API image, chạy ở mode `Demo`:

```bash
cd /opt/gamebug/repo

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env run --rm api \
  seed --dataset screenshots-v1 --confirm GAMEBUG_DEMO_RESET
```

Seed command sẽ:

- Import 12 historical tickets từ `screenshots/tickets.json` và ghép metadata ảnh từ `screenshots/labels.json`.
- Chạy idempotent: ticket đã tồn tại sẽ được cập nhật, không tạo record trùng.
- Chỉ chạy trong `Local`, `Demo`, `Test`.
- Từ chối nếu thiếu `--confirm GAMEBUG_DEMO_RESET`.
- Từ chối nếu connection string không giống demo/local DB.

---

## 17. Start API và Worker

```bash
cd /opt/gamebug/repo

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env up -d api worker
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env ps
```

Xem logs:

```bash
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env logs -f api
```

Ở terminal khác:

```bash
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env logs -f worker
```

Kiểm tra health local:

```bash
curl http://127.0.0.1:8080/health/live
curl http://127.0.0.1:8080/health/ready
```

`/health/ready` chỉ ready khi:

- DB connect được.
- MinIO bucket tồn tại.
- Worker đã ghi heartbeat gần đây.

Nếu vừa start xong mà ready trả `503`, đợi 30-60 giây rồi thử lại.

---

## 18. Cấu hình Nginx HTTP

Copy config:

```bash
sudo cp /opt/gamebug/repo/deploy/nginx/gamebug.conf /etc/nginx/sites-available/gamebug
sudo ln -sf /etc/nginx/sites-available/gamebug /etc/nginx/sites-enabled/gamebug
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
```

Test bằng IP:

```bash
curl http://<static-ip>/health/live
curl http://<static-ip>/health/ready
```

Nếu chưa có domain, demo bằng HTTP/IP là đủ. Nếu có domain, tiếp tục bước HTTPS.

---

## 19. Bật HTTPS nếu có domain

Trỏ DNS `api.yourdomain.com` về Elastic IP của EC2 trước. Nếu bạn không dùng Elastic IP mà dùng public IP tự cấp, IP có thể đổi sau mỗi lần stop/start instance.

Sửa server_name:

```bash
sudo nano /etc/nginx/sites-available/gamebug
```

Đổi:

```nginx
server_name _;
```

thành:

```nginx
server_name gamebugreport.duckdns.org;
```

Reload Nginx:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

Cấp certificate:

```bash
sudo certbot --nginx -d gamebugreport.duckdns.org
```

Test:

```bash
curl https://api.yourdomain.com/health/live
curl https://api.yourdomain.com/health/ready
```

---

## 20. Chạy evaluation demo

Evaluation endpoint chỉ mở trong `Local`, `Demo`, `Test`, `Development`. Compose đã dùng `Demo`, nên endpoint hoạt động.

Start evaluation:

```bash
RUN_ID=$(
  curl -s -X POST http://127.0.0.1:8080/api/v1/evaluations \
    -H "Content-Type: application/json" \
    -H "Idempotency-Key: demo-$(date +%s)" \
    -d '{"manifestId":"demo-v1","profile":"demo"}' \
  | sed -n 's/.*"runId":"\([^"]*\)".*/\1/p'
)

echo "$RUN_ID"
```

Xem kết quả:

```bash
curl http://127.0.0.1:8080/api/v1/evaluations/$RUN_ID
```

Download artifact:

```bash
curl -o evaluation-result-$RUN_ID.json \
  http://127.0.0.1:8080/api/v1/evaluations/$RUN_ID/artifact
```

Qua domain:

```bash
curl https://api.yourdomain.com/api/v1/evaluations/$RUN_ID
```

Lưu ý: Evaluation hiện gọi OpenAI thật. Nếu key sai, hết quota hoặc network lỗi, run có thể `CompletedWithErrors` hoặc case fail. Xem logs API/Worker để biết lỗi provider.

---

## 21. Script deploy lại khi code thay đổi

Tạo file `/opt/gamebug/deploy-gamebug.sh` trên server:

```bash
nano /opt/gamebug/deploy-gamebug.sh
```

Nội dung:

```bash
#!/usr/bin/env bash
set -euo pipefail

cd /opt/gamebug/repo

git pull

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env build api worker
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env up -d postgres minio createbuckets

docker run --rm \
  --network gamebug-demo_default \
  --env-file deploy/.env \
  -v "$PWD:/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc '
    dotnet tool install --global dotnet-ef --version 10.* &&
    export PATH="$PATH:/root/.dotnet/tools" &&
    dotnet restore src/GameBug.Api/GameBug.Api.csproj &&
    ConnectionStrings__Database="Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}" \
    dotnet ef database update \
      --project src/GameBug.Infrastructure \
      --startup-project src/GameBug.Api \
      --connection "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  '

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env up -d api worker

for i in {1..20}; do
  if curl -fsS http://127.0.0.1:8080/health/ready; then
    echo
    echo "Deploy OK"
    exit 0
  fi

  echo "Waiting for ready health..."
  sleep 5
done

echo "Deploy finished but ready health did not pass."
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env ps
exit 1
```

Cấp quyền:

```bash
chmod +x /opt/gamebug/deploy-gamebug.sh
```

Deploy lại:

```bash
/opt/gamebug/deploy-gamebug.sh
```

Nếu network không phải `gamebug-demo_default`, sửa script theo kết quả:

```bash
docker network ls
```

---

## 22. Backup và restore cho demo

### Cách đơn giản nhất: EBS snapshot hoặc EC2 AMI

Trước ngày demo:

1. EC2 -> Instances -> chọn `gamebug-demo-ec2`.
2. Actions -> Image and templates -> **Create image** nếu muốn backup nguyên máy.
3. Hoặc EC2 -> Volumes -> chọn root EBS volume -> Actions -> **Create snapshot** nếu chỉ cần backup disk.

Nên tạo AMI/snapshot sau khi:

- DB đã migrate.
- Seed demo xong.
- App health ready.
- Bạn đã chạy thử evaluation thành công.

Nếu chỉ demo trong thời gian ngắn, cách nhanh nhất là dùng `pg_dump` bên dưới và terminate EC2 sau khi xong.

### Backup DB thủ công

Tạo folder backup:

```bash
mkdir -p /opt/gamebug/backups
```

Dump DB:

```bash
docker exec gamebug-postgres pg_dump \
  -U gamebug_demo \
  -d gamebug_db \
  -Fc \
  -f /tmp/gamebug_db.dump

docker cp gamebug-postgres:/tmp/gamebug_db.dump /opt/gamebug/backups/gamebug_db-$(date +%Y%m%d-%H%M%S).dump
```

Restore DB:

```bash
docker compose -f /opt/gamebug/repo/deploy/docker-compose.demo.yml --env-file /opt/gamebug/repo/deploy/.env stop api worker

docker cp /opt/gamebug/backups/<dump-file>.dump gamebug-postgres:/tmp/restore.dump

docker exec gamebug-postgres pg_restore \
  -U gamebug_demo \
  -d gamebug_db \
  --clean \
  --if-exists \
  /tmp/restore.dump

docker compose -f /opt/gamebug/repo/deploy/docker-compose.demo.yml --env-file /opt/gamebug/repo/deploy/.env up -d api worker
```

### Backup MinIO đơn giản

Với demo, EBS snapshot/AMI thường đủ. Nếu muốn copy object ra ngoài:

```bash
mkdir -p /opt/gamebug/backups/minio-data
docker cp gamebug-minio:/data /opt/gamebug/backups/minio-data/data-$(date +%Y%m%d-%H%M%S)
```

---

## 23. Reset demo trước khi trình bày

Nếu muốn về trạng thái sạch:

```bash
cd /opt/gamebug/repo

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env run --rm api \
  seed --dataset screenshots-v1 --confirm GAMEBUG_DEMO_RESET

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env restart worker api
```

Chờ ready:

```bash
for i in {1..20}; do
  curl -fsS http://127.0.0.1:8080/health/ready && break
  sleep 5
done
```

---

## 24. Troubleshooting

| Vấn đề | Cách kiểm tra | Cách xử lý nhanh |
|---|---|---|
| API không start | `docker logs gamebug-api` | Kiểm tra `.env`, OpenAI key, DB connection |
| Worker không start | `docker logs gamebug-worker` | Kiểm tra DB, OpenAI key, MinIO |
| `/health/live` fail | `docker ps` | Restart API: `docker compose ... restart api` |
| `/health/ready` trả 503 | `docker logs gamebug-worker` | Đợi heartbeat 30-60 giây, kiểm tra bucket MinIO |
| Nginx 502 | `sudo nginx -t`, `curl http://127.0.0.1:8080/health/live` | API chưa chạy hoặc port 8080 chưa bind |
| Build OOM | `free -h`, `docker builder prune` | Tạo swap, hoặc stop EC2 rồi đổi lên `t3.medium` |
| Migration fail | Xem output `dotnet ef` | Kiểm tra network `gamebug-demo_default` và connection string |
| Seed bị từ chối | Xem lỗi seed | Environment phải là `Demo`, DB nên là `gamebug_db` |
| Evaluation fail provider | `docker logs gamebug-api`, `docker logs gamebug-worker` | Kiểm tra `OPENAI_API_KEY`, quota, model name |
| MinIO bucket missing | `curl http://127.0.0.1:9000/minio/health/live` | Chạy lại service `createbuckets` |
| Disk đầy | `df -h`, `docker system df` | `docker image prune`, xóa artifact/log không cần |
| Docker pull SDK báo `no space left on device` | `df -h`, `lsblk`, `docker system df` | Tăng EBS root volume lên 30GB+, grow filesystem, rồi prune Docker cache |

Lệnh hữu ích:

```bash
cd /opt/gamebug/repo

docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env ps
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env logs --tail=100 api
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env logs --tail=100 worker
docker stats
df -h
free -h
```

Nếu Docker pull/publish báo hết disk khi tải `mcr.microsoft.com/dotnet/sdk:10.0`, tăng root EBS volume rồi mở rộng filesystem:

```bash
df -h
lsblk
```

Trong AWS Console:

1. EC2 -> Instances -> chọn `gamebug-demo-ec2`.
2. Tab Storage -> click root Volume ID `vol-...`.
3. Actions -> Modify volume.
4. Size: `30 GiB` hoặc `40 GiB`, Type: `gp3`.
5. Chờ volume state thành `in-use - completed` hoặc `optimizing`.

Trên EC2, mở rộng partition/filesystem:

```bash
sudo growpart /dev/nvme0n1 1
findmnt -no FSTYPE /
```

Nếu filesystem là `ext4`:

```bash
sudo resize2fs /dev/nvme0n1p1
```

Nếu filesystem là `xfs`:

```bash
sudo xfs_growfs -d /
```

Dọn Docker cache bị pull dở rồi chạy lại:

```bash
docker system prune -af
df -h
docker compose -f deploy/docker-compose.demo.yml --env-file deploy/.env build
```

---

## 25. Checklist trước demo

- [ ] `curl http://127.0.0.1:8080/health/live` trả `Healthy`.
- [ ] `curl http://127.0.0.1:8080/health/ready` trả `Ready`.
- [ ] URL public qua Nginx trả health OK.
- [ ] OpenAI key thật đã cấu hình trong `deploy/.env`.
- [ ] Seed demo đã chạy thành công.
- [ ] Worker logs không có lỗi lặp liên tục.
- [ ] Chạy thử một evaluation `demo-v1` thành công.
- [ ] Đã tạo EBS snapshot/AMI hoặc `pg_dump` trước giờ trình bày.
- [ ] Không mở port 5432, 9000, 9001 ra internet.

---

## 26. Optional: deploy tự động rất đơn giản bằng SSH

Nếu muốn bấm merge/push rồi server tự deploy, cách rẻ nhất là GitHub Actions SSH vào server và chạy `/opt/gamebug/deploy-gamebug.sh`.

Không cần ECR. Không cần AWS access key.

GitHub secrets cần:

| Secret | Giá trị |
|---|---|
| `EC2_HOST` | Public IP, Elastic IP hoặc domain |
| `EC2_USER` | `ubuntu` |
| `EC2_SSH_KEY` | Private key được phép SSH vào server |

Workflow mẫu `.github/workflows/deploy-demo.yml`:

```yaml
name: Deploy Demo

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --logger "console;verbosity=normal"

  deploy:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - name: Deploy over SSH
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.EC2_HOST }}
          username: ${{ secrets.EC2_USER }}
          key: ${{ secrets.EC2_SSH_KEY }}
          script: /opt/gamebug/deploy-gamebug.sh
```

Lưu ý bảo mật:

- SSH key chỉ dùng cho deploy server này.
- Không đưa OpenAI key vào GitHub Actions nếu server đã có `deploy/.env`.
- Nếu repo private, server cần quyền `git pull`.

---

## 27. Optional: khi nào nên chuyển sang kiến trúc production

Chỉ cân nhắc sau hackathon/demo nếu app cần chạy thật lâu dài:

| Nhu cầu | Nên chuyển sang |
|---|---|
| Không muốn mất DB khi instance lỗi | RDS PostgreSQL |
| Attachment cần bền và rẻ hơn | S3 |
| Deploy image chuẩn hơn | ECR |
| Auto scaling hoặc zero downtime | ECS/Fargate hoặc App Runner |
| Secret rotation/audit | SSM Parameter Store hoặc Secrets Manager |
| CI/CD không dùng SSH key | GitHub Actions OIDC + IAM role |

Với mục tiêu hiện tại là test/demo rẻ trong lúc Lightsail chưa dùng được, EC2 `t3.small` + Docker Compose là hợp lý nhất.

---

## 28. Tóm tắt CV DevOps cho bản demo này

- Docker multi-stage build cho .NET API và Worker.
- Docker Compose orchestration cho API, Worker, PostgreSQL/pgvector, MinIO.
- EC2 small provisioning với Security Group, public IPv4/Elastic IP và EBS gp3.
- Host Nginx reverse proxy, optional HTTPS bằng Let's Encrypt.
- Environment secrets qua server `.env`, không commit secret.
- EF migrations bằng one-off SDK container.
- Guarded seed/reset cho demo dataset.
- Health checks `/health/live` và `/health/ready`.
- Worker heartbeat readiness.
- Backup bằng EBS snapshot/AMI và `pg_dump`.
- Optional GitHub Actions deploy qua SSH.
