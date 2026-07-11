# AWS Lightsail Deployment Guide — GameBug Repro Agent

**Stack**: .NET 10 · PostgreSQL/pgvector · MinIO · Docker Compose · Nginx · GitHub Actions  
**Target**: AWS Lightsail $12/tháng (2 vCPU, 2 GB RAM, 60 GB SSD)  
**Điều kiện tiên quyết**: Phase 8 đã implement và `dotnet test` green

---

## Giai đoạn 1 — Tạo Dockerfiles

### Bước 1: Dockerfile cho API

Tạo file `src/GameBug.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj             src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj       src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj   src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Api/GameBug.Api.csproj                   src/GameBug.Api/
RUN dotnet restore src/GameBug.Api/GameBug.Api.csproj

COPY src/ src/
RUN dotnet publish src/GameBug.Api/GameBug.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameBug.Api.dll"]
```

### Bước 2: Dockerfile cho Worker

Tạo file `src/GameBug.Worker/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj             src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj       src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj   src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Worker/GameBug.Worker.csproj              src/GameBug.Worker/
RUN dotnet restore src/GameBug.Worker/GameBug.Worker.csproj

COPY src/ src/
RUN dotnet publish src/GameBug.Worker/GameBug.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "GameBug.Worker.dll"]
```

### Bước 3: Test build local

```powershell
# Chạy từ root dự án
docker build -t gamebug-api:local -f src/GameBug.Api/Dockerfile .
docker build -t gamebug-worker:local -f src/GameBug.Worker/Dockerfile .
```

Phải build thành công mới tiếp tục.

---

## Giai đoạn 2 — Docker Compose Production

### Bước 4: Tạo `deploy/docker-compose.prod.yml`

```yaml
services:
  postgres:
    image: pgvector/pgvector:0.8.4-pg16-bookworm
    container_name: gamebug-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  minio:
    image: minio/minio:RELEASE.2025-09-07T16-13-09Z-cpuv1
    container_name: gamebug-minio
    restart: unless-stopped
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - miniodata:/data
    command: server /data --console-address ":9001"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:9000/minio/health/live || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  createbuckets:
    image: minio/mc:RELEASE.2025-08-13T08-35-41Z-cpuv1
    depends_on:
      minio:
        condition: service_healthy
    entrypoint: >
      /bin/sh -c "
      /usr/bin/mc alias set local http://minio:9000 ${MINIO_ROOT_USER} ${MINIO_ROOT_PASSWORD};
      /usr/bin/mc mb --ignore-existing local/${MINIO_BUCKET};
      exit 0;
      "
    networks:
      - internal

  api:
    image: ${ECR_REGISTRY}/gamebug-api:${IMAGE_TAG:-latest}
    container_name: gamebug-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Database: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ObjectStorage__Endpoint: minio:9000
      ObjectStorage__AccessKey: ${MINIO_ROOT_USER}
      ObjectStorage__SecretKey: ${MINIO_ROOT_PASSWORD}
      ObjectStorage__BucketName: ${MINIO_BUCKET}
      ObjectStorage__UseSsl: "false"
      ObjectStorage__TimeoutSeconds: "30"
      Ai__OpenAI__ApiKey: ${OPENAI_API_KEY}
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 5
    networks:
      - internal
      - web

  worker:
    image: ${ECR_REGISTRY}/gamebug-worker:${IMAGE_TAG:-latest}
    container_name: gamebug-worker
    restart: unless-stopped
    environment:
      DOTNET_ENVIRONMENT: Production
      ConnectionStrings__Database: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ObjectStorage__Endpoint: minio:9000
      ObjectStorage__AccessKey: ${MINIO_ROOT_USER}
      ObjectStorage__SecretKey: ${MINIO_ROOT_PASSWORD}
      ObjectStorage__BucketName: ${MINIO_BUCKET}
      ObjectStorage__UseSsl: "false"
      ObjectStorage__TimeoutSeconds: "30"
      Ai__OpenAI__ApiKey: ${OPENAI_API_KEY}
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - internal

networks:
  internal:
  web:
    external: true

volumes:
  pgdata:
  miniodata:
```

### Bước 5: Tạo `deploy/nginx/nginx.conf`

```nginx
events { worker_connections 1024; }

http {
    upstream gamebug_api {
        server api:8080;
    }

    # HTTP -> HTTPS redirect
    server {
        listen 80;
        server_name _;
        location /.well-known/acme-challenge/ { root /var/www/certbot; }
        location / { return 301 https://$host$request_uri; }
    }

    # HTTPS
    server {
        listen 443 ssl;
        server_name api.yourdomain.com;   # <-- đổi thành domain thật

        ssl_certificate     /etc/letsencrypt/live/api.yourdomain.com/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/api.yourdomain.com/privkey.pem;
        ssl_protocols       TLSv1.2 TLSv1.3;

        client_max_body_size 64m;

        location / {
            proxy_pass         http://gamebug_api;
            proxy_http_version 1.1;
            proxy_set_header   Host $host;
            proxy_set_header   X-Real-IP $remote_addr;
            proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto $scheme;
            proxy_read_timeout 120s;
        }
    }
}
```

### Bước 6: Tạo `.env.production` trên server (KHÔNG commit)

```bash
# PostgreSQL
POSTGRES_USER=gamebug_prod
POSTGRES_PASSWORD=<strong-random-password>
POSTGRES_DB=gamebug_production

# MinIO
MINIO_ROOT_USER=gamebug_minio
MINIO_ROOT_PASSWORD=<strong-random-password>
MINIO_BUCKET=gamebug-attachments

# OpenAI
OPENAI_API_KEY=sk-...

# ECR (điền sau khi tạo ECR)
ECR_REGISTRY=<account-id>.dkr.ecr.<region>.amazonaws.com
IMAGE_TAG=latest
```

---

## Giai đoạn 3 — Tạo Lightsail Instance

### Bước 7: Tạo instance trên AWS Console

1. Vào **AWS Console** → **Lightsail** → **Create instance**
2. Platform: **Linux/Unix**
3. Blueprint: **OS Only** → **Ubuntu 24.04 LTS**
4. Bundle: **$12/month** (2 GB RAM, 2 vCPU, 60 GB SSD)
5. Instance name: `gamebug-prod`
6. Click **Create instance**

### Bước 8: Gắn Static IP

1. Lightsail → **Networking** → **Create static IP**
2. Attach tới instance `gamebug-prod`
3. Ghi lại địa chỉ IP (ví dụ: `13.251.100.1`)

### Bước 9: Mở Firewall ports

Lightsail → instance → **Networking** tab → **Add rule**:
- **Custom TCP 443** (HTTPS)
- **Custom TCP 80** (HTTP → redirect HTTPS)
- **SSH 22** đã có sẵn (giới hạn IP của bạn nếu muốn)

---

## Giai đoạn 4 — Setup Server

SSH vào server:
```bash
ssh -i <lightsail-key.pem> ubuntu@<static-ip>
```

### Bước 10: Cài Docker

```bash
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
newgrp docker

# Verify
docker --version
docker compose version
```

### Bước 11: Cài Nginx + Certbot

```bash
sudo apt install -y nginx certbot python3-certbot-nginx

# Test nginx
sudo systemctl status nginx
```

### Bước 12: Tạo Docker network

```bash
docker network create web
```

### Bước 13: Clone repo và setup files

```bash
mkdir -p /opt/gamebug && cd /opt/gamebug

# Copy docker-compose.prod.yml và nginx.conf lên server
# (dùng scp hoặc rsync)
scp -i <key.pem> deploy/docker-compose.prod.yml ubuntu@<ip>:/opt/gamebug/
scp -i <key.pem> deploy/nginx/nginx.conf ubuntu@<ip>:/opt/gamebug/

# Tạo .env production (nhập thủ công, KHÔNG copy file có secret)
nano /opt/gamebug/.env
# (Điền nội dung từ Bước 6)
```

---

## Giai đoạn 5 — Tạo ECR & Push Images

### Bước 14: Tạo ECR repositories

```bash
# Chạy trên máy local (cần AWS CLI đã cấu hình)
aws ecr create-repository --repository-name gamebug-api    --region ap-southeast-1
aws ecr create-repository --repository-name gamebug-worker --region ap-southeast-1
```

Ghi lại ECR registry URL: `<account-id>.dkr.ecr.ap-southeast-1.amazonaws.com`

### Bước 15: Push images lần đầu (thủ công)

```powershell
# Trên máy local
$REGION = "ap-southeast-1"
$ACCOUNT = "<your-account-id>"
$ECR = "$ACCOUNT.dkr.ecr.$REGION.amazonaws.com"

# Login ECR
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ECR

# Build và push API
docker build -t gamebug-api:latest -f src/GameBug.Api/Dockerfile .
docker tag gamebug-api:latest "$ECR/gamebug-api:latest"
docker push "$ECR/gamebug-api:latest"

# Build và push Worker
docker build -t gamebug-worker:latest -f src/GameBug.Worker/Dockerfile .
docker tag gamebug-worker:latest "$ECR/gamebug-worker:latest"
docker push "$ECR/gamebug-worker:latest"
```

---

## Giai đoạn 6 — Deploy lần đầu

### Bước 16: Cấu hình Nginx và cấp SSL

Trên server:

```bash
# Cấu hình domain DNS trỏ về Static IP trước (chờ propagate ~5-10 phút)
# Sau đó:
sudo certbot --nginx -d api.yourdomain.com

# Copy nginx.conf đã chỉnh domain
sudo cp /opt/gamebug/nginx.conf /etc/nginx/nginx.conf
sudo nginx -t && sudo systemctl reload nginx
```

> **Nếu chưa có domain**: Tạm thời dùng HTTP, bỏ SSL block trong nginx.conf.

### Bước 17: Pull images và khởi động infra

```bash
cd /opt/gamebug

# Đặt ECR vào env
export ECR_REGISTRY="<account-id>.dkr.ecr.ap-southeast-1.amazonaws.com"

# Login ECR trên server
aws ecr get-login-password --region ap-southeast-1 | \
  docker login --username AWS --password-stdin $ECR_REGISTRY

# Khởi động infra (postgres + minio + createbuckets)
docker compose -f docker-compose.prod.yml --env-file .env up -d postgres minio createbuckets

# Chờ healthy
docker compose -f docker-compose.prod.yml ps
```

### Bước 18: Chạy EF migrations trên server

```bash
# Cài .NET SDK 10 trên server (chỉ cần lần đầu)
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 10.0
export PATH="$HOME/.dotnet:$PATH"

# Clone repo (hoặc copy source)
cd ~ && git clone <your-repo-url> gamebug-src
cd gamebug-src

# Chạy migration
ConnectionStrings__Database="Host=localhost;Port=5432;Database=gamebug_production;Username=gamebug_prod;Password=<pass>" \
dotnet ef database update \
  --project src/GameBug.Infrastructure \
  --startup-project src/GameBug.Api
```

> **Cách khác**: Thêm migration tự động khi API khởi động (thêm `app.MigrateDatabase()` vào Program.cs).

### Bước 19: Khởi động API + Worker

```bash
cd /opt/gamebug

docker compose -f docker-compose.prod.yml --env-file .env up -d api worker

# Xem logs
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f worker
```

### Bước 20: Kiểm tra health

```bash
curl http://localhost:8080/health/live
# -> {"Status":"Healthy"}

curl http://localhost:8080/health/ready
# -> {"Status":"Ready"}

# Qua Nginx/HTTPS:
curl https://api.yourdomain.com/health/ready
```

---

## Giai đoạn 7 — GitHub Actions CI/CD

### Bước 21: Tạo IAM User cho GitHub Actions

**AWS Console** → **IAM** → **Users** → **Create user**:
- Username: `github-actions-gamebug`
- Permissions: attach policy inline:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecr:GetAuthorizationToken",
        "ecr:BatchCheckLayerAvailability",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload"
      ],
      "Resource": "*"
    }
  ]
}
```

Tạo **Access Key** → ghi lại `AWS_ACCESS_KEY_ID` và `AWS_SECRET_ACCESS_KEY`.

### Bước 22: Tạo SSH key cho GitHub Actions deploy

```bash
# Trên server
ssh-keygen -t ed25519 -f ~/.ssh/github_actions -N ""
cat ~/.ssh/github_actions.pub >> ~/.ssh/authorized_keys
cat ~/.ssh/github_actions   # Copy private key này vào GitHub Secret
```

### Bước 23: Thêm GitHub Secrets

**GitHub repo** → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret Name | Giá trị |
|---|---|
| `AWS_ACCESS_KEY_ID` | IAM Access Key |
| `AWS_SECRET_ACCESS_KEY` | IAM Secret Key |
| `AWS_REGION` | `ap-southeast-1` |
| `ECR_REGISTRY` | `<account-id>.dkr.ecr.ap-southeast-1.amazonaws.com` |
| `LIGHTSAIL_HOST` | Static IP của server |
| `LIGHTSAIL_SSH_KEY` | Nội dung file `~/.ssh/github_actions` |
| `LIGHTSAIL_USER` | `ubuntu` |

### Bước 24: Tạo `.github/workflows/deploy.yml`

```yaml
name: CI/CD Deploy

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '10.0.x'

jobs:
  test:
    name: Build & Test
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
    name: Deploy to AWS Lightsail
    needs: test
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ secrets.AWS_REGION }}

      - name: Login to Amazon ECR
        id: login-ecr
        uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push API image
        env:
          ECR_REGISTRY: ${{ secrets.ECR_REGISTRY }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/gamebug-api:$IMAGE_TAG \
            -t $ECR_REGISTRY/gamebug-api:latest \
            -f src/GameBug.Api/Dockerfile .
          docker push $ECR_REGISTRY/gamebug-api:$IMAGE_TAG
          docker push $ECR_REGISTRY/gamebug-api:latest

      - name: Build and push Worker image
        env:
          ECR_REGISTRY: ${{ secrets.ECR_REGISTRY }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/gamebug-worker:$IMAGE_TAG \
            -t $ECR_REGISTRY/gamebug-worker:latest \
            -f src/GameBug.Worker/Dockerfile .
          docker push $ECR_REGISTRY/gamebug-worker:$IMAGE_TAG
          docker push $ECR_REGISTRY/gamebug-worker:latest

      - name: Deploy to server
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.LIGHTSAIL_HOST }}
          username: ${{ secrets.LIGHTSAIL_USER }}
          key: ${{ secrets.LIGHTSAIL_SSH_KEY }}
          script: |
            export ECR_REGISTRY="${{ secrets.ECR_REGISTRY }}"
            export IMAGE_TAG="${{ github.sha }}"
            
            cd /opt/gamebug
            
            # Login ECR
            aws ecr get-login-password --region ${{ secrets.AWS_REGION }} | \
              docker login --username AWS --password-stdin $ECR_REGISTRY
            
            # Pull new images
            docker compose -f docker-compose.prod.yml pull api worker
            
            # Rolling restart (zero-downtime nếu có 2+ replicas, ở đây đơn giản restart)
            docker compose -f docker-compose.prod.yml --env-file .env up -d --no-deps api worker
            
            # Health check
            sleep 15
            curl -f http://localhost:8080/health/ready || exit 1
            
            echo "Deploy successful: $IMAGE_TAG"

      - name: Health gate
        run: |
          sleep 10
          curl -f https://${{ secrets.LIGHTSAIL_HOST }}/health/ready || \
          echo "Warning: health check from outside failed (may need domain)"
```

### Bước 25: Cập nhật `.github/workflows/phase1-ci.yml`

Đổi tên file cũ thành `ci.yml` hoặc xóa đi (đã được thay bởi `deploy.yml`).

---

## Giai đoạn 8 — Seed Demo Data & Verify

### Bước 26: Seed historical tickets

```bash
# Trên server, gọi API endpoint import:
curl -X POST http://localhost:8080/api/v1/historical-tickets/import \
  -H "Content-Type: application/json" \
  -d @/opt/gamebug/seed/demo-v1-tickets.json

# Hoặc dùng script:
cd ~/gamebug-src && ./scripts/seed-demo.ps1
```

### Bước 27: Chạy golden E2E script

```powershell
# Trên máy local, trỏ vào production API:
$env:GAMEBUG_API_BASE = "https://api.yourdomain.com"
./scripts/demo-e2e.ps1
# Phải exit code 0 và in ra artifact path + metrics
```

### Bước 28: Kiểm tra final

```bash
# API root
curl https://api.yourdomain.com/
# -> {"Name":"Game Bug Repro Agent API","Status":"Running",...}

# Live health
curl https://api.yourdomain.com/health/live
# -> {"Status":"Healthy"}

# Ready health (DB + MinIO + Worker heartbeat)
curl https://api.yourdomain.com/health/ready
# -> {"Status":"Ready"}
```

---

## Troubleshooting

| Vấn đề | Lệnh kiểm tra |
|---|---|
| Container không start | `docker compose -f docker-compose.prod.yml logs api` |
| DB connection failed | `docker exec gamebug-postgres pg_isready` |
| MinIO không healthy | `curl http://localhost:9000/minio/health/live` |
| Out of memory | `free -h` và `docker stats` |
| ECR login fail | `aws sts get-caller-identity` (check credentials) |
| Migration chưa chạy | `docker exec gamebug-api dotnet-ef database update` |

---

## Tóm tắt chi phí

| Dịch vụ | Giá |
|---|---|
| Lightsail $12 plan | $12/tháng |
| ECR storage (~1GB) | ~$0.10/tháng |
| Static IP (instance đang chạy) | Miễn phí |
| Route 53 (nếu dùng) | $0.50/tháng |
| **Tổng** | **~$12.60/tháng** |

---

## CV DevOps — Những gì bạn đã làm

- **Docker** multi-stage build (.NET 10, non-root user, production-optimized)
- **Docker Compose** multi-service orchestration với health checks và dependency ordering
- **AWS Lightsail** VPS provisioning và management
- **AWS ECR** Docker image registry với versioned tags (`sha` + `latest`)
- **AWS IAM** least-privilege policy cho CI/CD
- **GitHub Actions** CI/CD pipeline: build → test → ECR push → SSH deploy → health gate
- **Nginx** reverse proxy với SSL/TLS termination (Let's Encrypt)
- **PostgreSQL/pgvector** production deployment với persistent volumes
- **Environment secrets** management (GitHub Secrets, server `.env`, no secrets in source)
- **Health checks** (`/health/live` + `/health/ready`) với dependency validation
