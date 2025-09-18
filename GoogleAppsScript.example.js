/**
 * Google Apps Script 예제 파일
 *
 * 사용법:
 * 1. 이 파일을 GoogleAppsScript.js로 복사
 * 2. YOUR_SPREADSHEET_ID를 실제 Google Sheets ID로 변경
 * 3. Google Apps Script 콘솔에 배포하여 URL 생성
 * 4. appsettings.json의 ChatApiUrl에 배포된 URL 설정
 */

// 설정
const CLAIM_SHEET_ID = 'YOUR_SPREADSHEET_ID'; // Google Sheets ID를 여기에 입력
const SHEETS = {
  CHAT_SESSIONS: 'ChatSessions',
  CHAT_MESSAGES: 'ChatMessages',
  CUSTOMERS: 'Customers',
  CLAIMS: 'Claims'
};

/**
 * HTTP 요청 처리 메인 함수
 */
function doPost(e) {
  try {
    const data = JSON.parse(e.postData.contents);
    const action = data.action;

    console.log(`Processing action: ${action}`);

    switch (action) {
      case 'startSession':
        return startSession(data);
      case 'endSession':
        return endSession(data);
      case 'sendMessage':
        return sendMessage(data);
      case 'getMessages':
        return getMessages(data);
      case 'updateSessionStatus':
        return updateSessionStatus(data);
      case 'getActiveSessions':
        return getActiveSessions(data);
      case 'assignStaffToSession':
        return assignStaffToSession(data);
      case 'releaseStaffFromSession':
        return releaseStaffFromSession(data);
      case 'checkSessionAssignment':
        return checkSessionAssignment(data);
      default:
        return createResponse(false, `Unknown action: ${action}`);
    }
  } catch (error) {
    console.error('doPost error:', error);
    return createResponse(false, `Server error: ${error.message}`);
  }
}

/**
 * 응답 객체 생성
 */
function createResponse(success, message, data = null) {
  const response = {
    success: success,
    message: message,
    timestamp: new Date().toISOString()
  };

  if (data !== null) {
    response.data = data;
  }

  return ContentService
    .createTextOutput(JSON.stringify(response))
    .setMimeType(ContentService.MimeType.JSON);
}

// 나머지 함수들은 실제 GoogleAppsScript.js 파일에서 구현되어 있습니다.
// 이 예제 파일은 구조와 설정 방법을 보여주기 위한 것입니다.