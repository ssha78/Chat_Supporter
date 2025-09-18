# Chat Reporter

고객 지원을 위한 채팅 시스템

## 설정 방법

### 1. 설정 파일 준비

#### appsettings.json
```bash
cp appsettings.example.json appsettings.json
```

`appsettings.json` 파일에서 다음 항목을 수정하세요:
- `GoogleAppsScript.ChatApiUrl`: Google Apps Script 배포 URL

#### GoogleAppsScript.js
```bash
cp GoogleAppsScript.example.js GoogleAppsScript.js
```

`GoogleAppsScript.js` 파일에서 다음 항목을 수정하세요:
- `CLAIM_SHEET_ID`: Google Sheets 스프레드시트 ID

### 2. Google Apps Script 설정

1. [Google Apps Script](https://script.google.com/) 접속
2. 새 프로젝트 생성
3. `GoogleAppsScript.js` 파일 내용을 복사하여 붙여넣기
4. Google Sheets 스프레드시트 생성 및 ID 확인
5. 웹앱으로 배포하여 URL 생성
6. `appsettings.json`에 배포된 URL 설정

### 3. 실행

```bash
dotnet run
```

## 주요 기능

- ✅ 실시간 채팅 시스템
- ✅ 다중 사용자 동시성 제어
- ✅ 고객 세션 관리
- ✅ 직원 할당 시스템
- ✅ Google Sheets 백엔드 연동

## 보안 주의사항

- `appsettings.json`과 `GoogleAppsScript.js` 파일은 개인 정보가 포함되어 있어 Git에서 제외됩니다
- 실제 사용 시 이 파일들을 안전하게 관리하세요