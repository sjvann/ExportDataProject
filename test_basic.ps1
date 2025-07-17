# 基本功能測試腳本

Write-Host "=== 資料庫匯出工具測試 ===" -ForegroundColor Green

# 測試 CLI 工具建置
Write-Host "`n1. 測試 CLI 工具建置..." -ForegroundColor Yellow
Set-Location "ExportData"
$buildResult = dotnet build --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ CLI 工具建置成功" -ForegroundColor Green
} else {
    Write-Host "❌ CLI 工具建置失敗" -ForegroundColor Red
    exit 1
}

# 測試 Web 工具建置
Write-Host "`n2. 測試 Web 工具建置..." -ForegroundColor Yellow
Set-Location "../ExportDataWeb"
$buildResult = dotnet build --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Web 工具建置成功" -ForegroundColor Green
} else {
    Write-Host "❌ Web 工具建置失敗" -ForegroundColor Red
    exit 1
}

# 回到根目錄
Set-Location ".."

Write-Host "`n=== 測試完成 ===" -ForegroundColor Green
Write-Host "`n使用方式:" -ForegroundColor Cyan
Write-Host "1. CLI 互動模式: cd ExportData && dotnet run --interactive" -ForegroundColor White
Write-Host "2. CLI 設定檔模式: cd ExportData && dotnet run --config appsettings.json" -ForegroundColor White
Write-Host "3. Web 介面模式: cd ExportDataWeb && dotnet run" -ForegroundColor White
Write-Host "4. 測試模式: cd ExportData && dotnet run --test" -ForegroundColor White
