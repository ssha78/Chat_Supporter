# ChatReporter 디버그 로그 분석 스크립트

$logDir = "$env:USERPROFILE\Desktop\ChatReporter_Logs"

Write-Host "=== 채팅 애플리케이션 디버그 로그 분석 ===" -ForegroundColor Cyan

if (-not (Test-Path $logDir)) {
    Write-Host "로그 디렉터리가 없습니다: $logDir" -ForegroundColor Red
    return
}

$logFiles = Get-ChildItem -Path $logDir -Filter "*.log" | Sort-Object Name

if ($logFiles.Count -eq 0) {
    Write-Host "로그 파일이 없습니다." -ForegroundColor Yellow
    return
}

Write-Host "로그 파일 수: $($logFiles.Count)" -ForegroundColor Green
Write-Host ""

$customerEvents = @()
$staffEvents = @()

foreach ($logFile in $logFiles) {
    Write-Host "📁 파일: $($logFile.Name)" -ForegroundColor White

    $content = Get-Content -Path $logFile.FullName -Encoding UTF8
    Write-Host "   라인 수: $($content.Length)"

    # 모드별 분류
    if ($logFile.Name -like "*Customer*") {
        $customerEvents += $content
    } elseif ($logFile.Name -like "*Staff*") {
        $staffEvents += $content
    }

    # 이벤트 타입별 카운트
    $sessionEvents = $content | Where-Object { $_ -like "*[SESSION]*" }
    $messageEvents = $content | Where-Object { $_ -like "*[MESSAGE]*" }
    $apiEvents = $content | Where-Object { $_ -like "*[API]*" }
    $uiEvents = $content | Where-Object { $_ -like "*[UI]*" }
    $errorEvents = $content | Where-Object { $_ -like "*[ERROR]*" }

    Write-Host "   세션 이벤트: $($sessionEvents.Count)"
    Write-Host "   메시지 이벤트: $($messageEvents.Count)"
    Write-Host "   API 이벤트: $($apiEvents.Count)"
    Write-Host "   UI 이벤트: $($uiEvents.Count)"
    Write-Host "   오류 이벤트: $($errorEvents.Count)"

    if ($errorEvents.Count -gt 0) {
        Write-Host "   🚨 오류 발견:" -ForegroundColor Red
        foreach ($error in $errorEvents) {
            Write-Host "      $error" -ForegroundColor Red
        }
    }

    Write-Host ""
}

Write-Host "=== 모드별 분석 ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "🟦 고객 모드 로그 ($($customerEvents.Count)개 이벤트):" -ForegroundColor Blue

if ($customerEvents.Count -gt 0) {
    $customerSessions = $customerEvents | Where-Object { $_ -like "*[SESSION]*" }
    $customerMessages = $customerEvents | Where-Object { $_ -like "*[MESSAGE]*" }
    $customerAPI = $customerEvents | Where-Object { $_ -like "*[API]*" }
    $customerUI = $customerEvents | Where-Object { $_ -like "*[UI]*" }
    $customerErrors = $customerEvents | Where-Object { $_ -like "*[ERROR]*" }

    Write-Host "   📊 SESSION: $($customerSessions.Count)개"
    Write-Host "   📊 MESSAGE: $($customerMessages.Count)개"
    Write-Host "   📊 API: $($customerAPI.Count)개"
    Write-Host "   📊 UI: $($customerUI.Count)개"
    Write-Host "   📊 ERROR: $($customerErrors.Count)개"

    if ($customerErrors.Count -gt 0) {
        Write-Host "   최근 오류:" -ForegroundColor Yellow
        $customerErrors | Select-Object -Last 3 | ForEach-Object {
            Write-Host "      $_" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "🟨 직원 모드 로그 ($($staffEvents.Count)개 이벤트):" -ForegroundColor Yellow

if ($staffEvents.Count -gt 0) {
    $staffSessions = $staffEvents | Where-Object { $_ -like "*[SESSION]*" }
    $staffMessages = $staffEvents | Where-Object { $_ -like "*[MESSAGE]*" }
    $staffAPI = $staffEvents | Where-Object { $_ -like "*[API]*" }
    $staffUI = $staffEvents | Where-Object { $_ -like "*[UI]*" }
    $staffErrors = $staffEvents | Where-Object { $_ -like "*[ERROR]*" }

    Write-Host "   📊 SESSION: $($staffSessions.Count)개"
    Write-Host "   📊 MESSAGE: $($staffMessages.Count)개"
    Write-Host "   📊 API: $($staffAPI.Count)개"
    Write-Host "   📊 UI: $($staffUI.Count)개"
    Write-Host "   📊 ERROR: $($staffErrors.Count)개"

    if ($staffErrors.Count -gt 0) {
        Write-Host "   최근 오류:" -ForegroundColor Yellow
        $staffErrors | Select-Object -Last 3 | ForEach-Object {
            Write-Host "      $_" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "=== 실시간 디버깅 권장사항 ===" -ForegroundColor Cyan
Write-Host "1. 첫 번째 exe: 고객 모드 → 테스트 세션 생성" -ForegroundColor Green
Write-Host "2. 두 번째 exe: 직원 모드 → 세션 선택" -ForegroundColor Green
Write-Host "3. 로그 파일을 실시간 모니터링: Get-Content -Wait -Path '$logDir\*.log'" -ForegroundColor Green
Write-Host "4. 스크립트 재실행: ./analyze_logs.ps1" -ForegroundColor Green