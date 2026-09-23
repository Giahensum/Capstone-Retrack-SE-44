[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay Docker. Hay cai Docker Desktop, mo Docker, sau do chay lai script."
}

Push-Location $projectRoot
try {
    if ($Reset) {
        Write-Host "Dang xoa database development cu..." -ForegroundColor Yellow
        docker compose down --volumes
    }

    Write-Host "Dang khoi dong PostgreSQL..." -ForegroundColor Cyan
    docker compose up -d postgres

    $databaseReady = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        docker compose exec -T postgres pg_isready -U retrack -d retrack *> $null
        if ($LASTEXITCODE -eq 0) {
            $databaseReady = $true
            break
        }
        Start-Sleep -Seconds 2
    }

    if (-not $databaseReady) {
        throw "PostgreSQL khong san sang sau 60 giay. Chay 'docker compose logs postgres' de xem log."
    }

    $tableCount = docker compose exec -T postgres psql -U retrack -d retrack -tAc `
        "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';"
    if ($LASTEXITCODE -ne 0 -or [int]$tableCount -lt 1) {
        throw "Database da chay nhung schema chua duoc tao. Chay script voi -Reset de nap lai schema."
    }

    Write-Host "Database RETRACK da san sang tai localhost:5432." -ForegroundColor Green
    Write-Host "So bang da khoi tao: $tableCount"
    Write-Host "Database: retrack | User: retrack | Password: retrack_dev"
}
finally {
    Pop-Location
}
