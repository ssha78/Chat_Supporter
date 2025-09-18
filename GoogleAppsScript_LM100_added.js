/**
 * Chat Supporter - Google Apps Script Backend
 * AI 클레임 접수 시스템을 위한 백엔드 API
 */

// 스프레드시트 ID들 (실제 사용시 변경 필요)
const CHAT_SHEET_ID = '15z3xgq6WEAs2ZLRWG06XjfBJ0acPcamvYbBT31IQ5JU';
const CLAIM_SHEET_ID = '1Nx437Sc5FA1o-2OaSxdiUNb___jKq8xVvJIOg2BuWLE'; 
const LEARNING_SHEET_ID = '1L3rxT1_v-Xc9XKwzVDE8EsSEvTFaeCD9fUh1Yb6wPDc';

// 시트 이름들
const SHEETS = {
  CHAT_MESSAGES: 'ChatMessages',
  CHAT_SESSIONS: 'ChatSessions',
  CLAIMS: 'Claims',
  CUSTOMERS: 'Customers',
  LEARNING_DATA: 'LearningData',
  AI_RESPONSES: 'AIResponses'
};

/**
 * HTTP POST 요청 처리 (웹 앱 엔트리 포인트)
 */
function doPost(e) {
  try {
    const requestData = JSON.parse(e.postData.contents);
    const { action, data, timestamp } = requestData;
    
    console.log(`API 호출: ${action} at ${timestamp}`);
    
    switch (action) {
      case 'sendMessage':
        return sendMessage(data);
      case 'getChatHistory':
        return getChatHistory(data);
      case 'createClaim':
        return createClaim(data);
      case 'getClaim':
        return getClaim(data);
      case 'saveLearningData':
        return saveLearningData(data);
      case 'getAIResponse':
        return getAIResponse(data);
      case 'getActiveSessions':
        return getActiveSessions(data);
      case 'getLearningData':
        return getLearningData(data);
      case 'createSession':
        return createSession(data);
      case 'updateSession':
        return updateSession(data);
      case 'updateSessionStatus':
        return updateSessionStatus(data);
      default:
        return createResponse(false, `Unknown action: ${action}`);
    }
  } catch (error) {
    console.error('API 오류:', error);
    return createResponse(false, `서버 오류: ${error.message}`);
  }
}

/**
 * HTTP GET 요청 처리 (테스트용)
 */
function doGet(e) {
  return HtmlService.createHtmlOutput(`
    <h1>Chat Supporter API</h1>
    <p>이 API는 POST 요청만 지원합니다.</p>
    <p>현재 시간: ${new Date().toLocaleString('ko-KR')}</p>
  `);
}

/**
 * 채팅 메시지 전송 처리
 */
function sendMessage(messageData) {
  try {
    const sheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_MESSAGES);
    
    // 헤더가 없으면 생성
    if (sheet.getLastRow() === 0) {
      sheet.getRange(1, 1, 1, 10).setValues([[
        'ID', 'SessionID', 'Content', 'Timestamp', 'Type', 'SenderName', 
        'IsFromAI', 'ConfidenceScore', 'Attachments', 'ParentMessageID'
      ]]);
    }
    
    // 메시지 데이터 안전하게 처리
    const messageId = messageData.Id || messageData.id || Utilities.getUuid();
    const sessionId = messageData.SessionId || messageData.sessionId || '';
    const content = messageData.Content || messageData.content || '';
    const timestamp = messageData.Timestamp || messageData.timestamp || new Date();
    const type = messageData.Type || messageData.type || 'Customer';
    const senderName = messageData.SenderName || messageData.senderName || '';
    
    console.log('메시지 데이터 확인:', {
      messageId,
      sessionId,
      content,
      type,
      senderName
    });
    
    // 메시지 데이터 저장
    const row = [
      messageId,
      sessionId,
      content,
      new Date(timestamp),
      type,
      senderName,
      messageData.IsFromAI || messageData.isFromAI || false,
      messageData.ConfidenceScore || messageData.confidenceScore || null,
      JSON.stringify(messageData.Attachments || messageData.attachments || []),
      messageData.ParentMessageId || messageData.parentMessageId || null
    ];

    sheet.appendRow(row);

    // 세션 정보 생성/업데이트 (ChatSessions 시트)
    createOrUpdateChatSession(sessionId, senderName, type);

    // AI 자동 응답 확인 (고객 메시지인 경우)
    if (messageData.Type === 'Customer') {
      const aiResponse = tryGenerateAIResponse(messageData);
      if (aiResponse) {
        // AI 응답을 별도로 저장
        const aiRow = [
          Utilities.getUuid(),
          sessionId,
          aiResponse.content,
          new Date(),
          'AI',
          'AI Assistant',
          true,
          aiResponse.confidence,
          '[]',
          messageId
        ];
        sheet.appendRow(aiRow);
      }
    }
    
    return createResponse(true, '메시지가 저장되었습니다.', {
      Id: messageId,
      Timestamp: new Date(timestamp)
    });
    
  } catch (error) {
    console.error('메시지 저장 오류:', error);
    return createResponse(false, `메시지 저장 실패: ${error.message}`);
  }
}

/**
 * 채팅 히스토리 조회
 */
function getChatHistory(queryData) {
  try {
    const { sessionId } = queryData;
    const sheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_MESSAGES);
    
    if (sheet.getLastRow() <= 1) {
      return createResponse(true, '채팅 히스토리가 없습니다.', []);
    }
    
    const data = sheet.getDataRange().getValues();
    const headers = data[0];
    const messages = [];
    
    // 해당 세션의 메시지만 필터링 (최신 데이터부터 역순 검색으로 성능 향상)
    for (let i = data.length - 1; i >= 1; i--) {
      const row = data[i];
      if (row[1] === sessionId) { // SessionID 컬럼
        messages.push({
          Id: row[0],
          SessionId: row[1],
          Content: row[2],
          Timestamp: row[3],
          Type: row[4],
          SenderName: row[5],
          IsFromAI: row[6],
          ConfidenceScore: row[7],
          Attachments: JSON.parse(row[8] || '[]'),
          ParentMessageId: row[9]
        });
      }
    }
    
    // 시간순 정렬
    messages.sort((a, b) => new Date(a.Timestamp) - new Date(b.Timestamp));
    
    return createResponse(true, `${messages.length}개 메시지 조회`, messages);
    
  } catch (error) {
    console.error('채팅 히스토리 조회 오류:', error);
    return createResponse(false, `히스토리 조회 실패: ${error.message}`);
  }
}

/**
 * 클레임 생성 처리
 */
function createClaim(claimData) {
  try {
    const claimSheet = getSheet(CLAIM_SHEET_ID, SHEETS.CLAIMS);
    const customerSheet = getSheet(CLAIM_SHEET_ID, SHEETS.CUSTOMERS);
    
    // 클레임 시트 헤더 확인
    if (claimSheet.getLastRow() === 0) {
      claimSheet.getRange(1, 1, 1, 12).setValues([[
        'ID', 'ChatSessionID', 'CreatedAt', 'UpdatedAt', 'Status', 'Category', 
        'Priority', 'Title', 'Description', 'CustomerID', 'AssignedStaff', 'Resolution'
      ]]);
    }
    
    // 고객 정보 저장 또는 업데이트
    const customerId = saveCustomer(customerSheet, claimData.Customer);
    
    // 클레임 데이터 저장
    const claimId = Utilities.getUuid();
    const claimRow = [
      claimId,
      claimData.ChatSessionId,
      new Date(claimData.CreatedAt),
      null,
      claimData.Status || 'Open',
      claimData.Category,
      claimData.Priority,
      claimData.Title,
      claimData.Description,
      customerId,
      claimData.AssignedStaff || null,
      null
    ];
    
    claimSheet.appendRow(claimRow);
    
    // 클레임 생성 알림 이메일 발송 (옵션)
    sendClaimNotification(claimData, claimId);
    
    return createResponse(true, '클레임이 생성되었습니다.', {
      Id: claimId,
      Status: 'Open',
      CreatedAt: new Date()
    });
    
  } catch (error) {
    console.error('클레임 생성 오류:', error);
    return createResponse(false, `클레임 생성 실패: ${error.message}`);
  }
}

/**
 * 학습 데이터 저장
 */
function saveLearningData(learningData) {
  try {
    const sheet = getSheet(LEARNING_SHEET_ID, SHEETS.LEARNING_DATA);
    
    // 헤더 확인
    if (sheet.getLastRow() === 0) {
      sheet.getRange(1, 1, 1, 11).setValues([[
        'ID', 'Question', 'Answer', 'Category', 'CreatedAt', 'StaffID', 
        'QualityScore', 'Keywords', 'IsApproved', 'UsageCount', 'OriginalMessageId'
      ]]);
    }
    
    // 키워드 자동 추출
    const keywords = extractKeywords(learningData.Question + ' ' + learningData.Answer);
    
    const row = [
      learningData.Id || Utilities.getUuid(),
      learningData.Question || '',
      learningData.Answer || '',
      learningData.Category || '',
      learningData.CreatedAt ? new Date(learningData.CreatedAt) : new Date(),
      learningData.StaffId || '',
      learningData.QualityScore || 0,
      Array.isArray(learningData.Keywords) ? learningData.Keywords.join(', ') : JSON.stringify(keywords),
      learningData.IsApproved !== undefined ? learningData.IsApproved : false,
      learningData.UsageCount || 0,
      learningData.OriginalMessageId || ''
    ];
    
    sheet.appendRow(row);
    
    return createResponse(true, '학습 데이터가 저장되었습니다.', {
      Id: row[0],
      Keywords: keywords
    });
    
  } catch (error) {
    console.error('학습 데이터 저장 오류:', error);
    return createResponse(false, `학습 데이터 저장 실패: ${error.message}`);
  }
}

/**
 * AI 응답 생성 (학습 데이터 기반 키워드 매칭)
 */
function getAIResponse(queryData) {
  try {
    const { question, sessionId, customerInfo } = queryData;
    
    console.log(`AI 응답 요청: ${question} (세션: ${sessionId})`);
    
    // 학습 데이터에서 유사한 질문 검색
    const learningSheet = getSheet(LEARNING_SHEET_ID, SHEETS.LEARNING_DATA);
    const bestMatch = findBestMatch(learningSheet, question);
    
    if (bestMatch && bestMatch.confidence > 0.3) {
      // 응답 생성
      const responseData = {
        response: bestMatch.answer,
        confidence: bestMatch.confidence,
        originalQuestion: bestMatch.originalQuestion,
        source: 'learning_data'
      };
      
      // 사용량 증가 (학습 데이터)
      incrementUsageCount(learningSheet, bestMatch.rowIndex);
      
      return createResponse(true, 'AI 응답 생성 완료', responseData);
    } else {
      // 기본 응답들 - 다양한 상황별 확장
      const defaultResponses = [
        // === 인사 및 시작 ===
        {
          keywords: ['안녕', '시작', '문의', '처음', '시작해볼게', '시작할게'],
          response: '안녕하세요! L-CAM 고객지원 AI입니다. 어떤 도움이 필요하신지 편하게 말씀해 주세요.',
          confidence: 0.6
        },
        
        // === 질문 의도 ===
        {
          keywords: ['질문', '궁금', '물어볼게', '도움', '알려주세요', '문의드려요', '여쭤볼게요', '질문이요'],
          response: '네, 궁금한 점이 있으시군요! 어떤 내용에 대해 질문하고 싶으신가요? 구체적으로 말씀해 주시면 더 정확한 답변을 드릴 수 있습니다.',
          confidence: 0.7
        },
        
        // === 제품 문제 및 고장 ===
        {
          keywords: ['고장', '문제', '오류', '작동안함', '에러', '안돼', '안되네', '이상해', '멈춤', '멈췄어', '먹통'],
          response: '제품에 문제가 발생하셨군요. 구체적으로 어떤 증상이 나타나는지 자세히 설명해주시면 더 정확한 도움을 드릴 수 있습니다. (예: 전원이 안 켜져요, 화면이 깜빡여요 등)',
          confidence: 0.8
        },
        
        // === 급한 상황 ===
        {
          keywords: ['급해', '빨리', '긴급', '응급', '지금당장', '당장', '시급', '바로'],
          response: '긴급한 상황이시군요! 빠른 도움을 위해 직원 연결을 권장합니다. 우선 어떤 문제가 발생했는지 간단히 말씀해 주시겠어요?',
          confidence: 0.9
        },
        
        // === AS 및 서비스 ===
        {
          keywords: ['AS', 'a/s', '수리', '점검', '서비스', '정비', '체크', '검사'],
          response: 'A/S 서비스 문의이시군요. 제품 모델명과 구매일자를 확인해 주시면 더 정확한 안내를 도와드릴 수 있습니다. 보증서나 영수증을 준비해 주세요.',
          confidence: 0.7
        },
        
        // === 구매 및 교체 ===
        // 키워드 제외
        // {
        //   keywords: ['교체', '반품', '환불', '구매', '주문', '취소', '변경'],
        //   response: '제품 교체나 반품에 대한 문의이시군요. 구매 날짜와 제품 상태를 확인해야 합니다. 직원 연결을 통해 정확한 안내를 받으시기 바랍니다.',
        //   confidence: 0.6
        // },
        
        // === 사용법 및 설명 ===
        {
          keywords: ['사용법', '어떻게', '방법', '설명', '매뉴얼', '가이드', '설치', '설정', '조작법'],
          response: '사용법에 대한 문의이시군요! 구체적으로 어떤 기능이나 설정에 대해 알고 싶으신가요? 제품 매뉴얼을 참고하시거나, 상세한 설명이 필요하시면 직원에게 연결해 드릴 수 있습니다.',
          confidence: 0.7
        },
        
        // === 긍정 반응 ===
        {
          keywords: ['고마워', '감사', '좋아', '괜찮', '잘됐', '해결됐', '고맙다', '도움됐'],
          response: '도움이 되셨다니 다행입니다! 😊 다른 궁금한 점이 있으시면 언제든 말씀해 주세요. L-CAM 제품을 이용해 주셔서 감사합니다.',
          confidence: 0.8
        },
        
        // === 동의 표현 ===
        {
          keywords: ['네', '응', '그래', '맞아', '알겠어', '오케이', '확인', '이해했어'],
          response: '네, 이해하셨군요! 추가로 궁금한 점이나 도움이 필요한 부분이 있으시면 말씀해 주세요.',
          confidence: 0.6
        },
        
        // === 부정 반응 ===
        {
          keywords: ['안돼', '안되네', '아니야', '틀렸어', '잘못됐', '다시', '아닌것같아'],
          response: '제가 잘못 이해한 것 같네요. 다시 한 번 자세히 설명해 주시거나, 직원에게 연결해 드릴까요?',
          confidence: 0.7
        },
        
        // === 기다림 및 시간 ===
        {
          keywords: ['기다려', '잠깐', '잠시', '시간', '언제', '얼마나', '며칠', '빨리'],
          response: '처리 시간에 대한 문의이시군요. 일반적으로 A/S는 1-3일, 부품 교체는 3-7일 정도 소요됩니다. 정확한 시간은 상황에 따라 달라질 수 있어 직원 상담을 권해드립니다.',
          confidence: 0.6
        },
        
        // === 비용 및 가격 ===
        {
          keywords: ['비용', '가격', '얼마', '돈', '무료', '유료', '요금', '비싸'],
          response: '비용에 대한 문의이시군요. 보증 기간 내라면 무료로 처리되는 경우가 많습니다. 정확한 비용은 증상과 보증 상태에 따라 달라지니 직원 상담을 받아보시기 바랍니다.',
          confidence: 0.7
        },
        
        // === 예약 및 방문 ===
        {
          keywords: ['예약', '방문', '출장', '직접', '가져가', '픽업', '배송'],
          response: '방문이나 픽업 서비스에 대한 문의이시군요. 지역과 제품 상태에 따라 서비스 방식이 달라집니다. 구체적인 안내를 위해 직원과 상담해 주시기 바랍니다.',
          confidence: 0.7
        },
        
        // === 화가 남/불만 ===
        {
          keywords: ['화나', '짜증', '불만', '이상하', '이해안돼', '답답', '속상', '열받'],
          response: '불편을 끼쳐드려 정말 죄송합니다. 고객님의 문제를 신속히 해결하기 위해 직원에게 바로 연결해 드리겠습니다. 조금만 기다려 주세요.',
          confidence: 0.9
        },
        
        // === 칭찬/만족 ===
        {
          keywords: ['만족', '좋네', '훌륭', '최고', '완벽', '잘해', '친절', '빠르'],
          response: '좋은 평가를 해주셔서 감사합니다! 😊 앞으로도 더 나은 서비스로 보답하겠습니다. 추가로 도움이 필요한 것이 있으시면 언제든 말씀해 주세요.',
          confidence: 0.8
        }
      ];
      
      // 기본 응답 매칭
      for (const defaultResponse of defaultResponses) {
        if (defaultResponse.keywords.some(keyword => question.includes(keyword))) {
          const responseData = {
            response: defaultResponse.response,
            confidence: defaultResponse.confidence,
            source: 'default_responses'
          };
          return createResponse(true, '기본 AI 응답 생성', responseData);
        }
      }
      
      // 매칭되는 응답이 없는 경우
      const responseData = {
        response: '죄송합니다. 해당 문의에 대한 적절한 답변을 찾지 못했습니다. 직원 연결을 통해 정확한 도움을 받으시기 바랍니다.',
        confidence: 0.1,
        source: 'no_match'
      };
      
      return createResponse(true, '기본 응답 제공', responseData);
    }
    
  } catch (error) {
    console.error('AI 응답 생성 오류:', error);
    return createResponse(false, `AI 응답 생성 실패: ${error.message}`);
  }
}

/**
 * 활성 세션 목록 조회 (ChatSessions 시트 기반)
 */
function getActiveSessions(queryData) {
  try {
    const sessionSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_SESSIONS);
    const chatSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_MESSAGES);

    if (sessionSheet.getLastRow() <= 1) {
      // ChatSessions 시트가 비어있으면 기존 방식으로 폴백
      return getActiveSessionsFromMessages(chatSheet);
    }

    const sessionData = sessionSheet.getDataRange().getValues();
    const activeSessions = [];

    // 최근 24시간 이내의 세션만 고려
    const oneDayAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);

    for (let i = 1; i < sessionData.length; i++) {
      const row = sessionData[i];
      const sessionId = row[0]; // Id 컬럼
      const lastActivity = new Date(row[7]); // LastActivity 컬럼
      const status = row[3]; // Status 컬럼

      // 24시간 이내이고 완료되지 않은 세션만
      if (lastActivity >= oneDayAgo && status !== 'Completed' && status !== 'Disconnected') {
        // 해당 세션의 메시지 수 계산
        const messageCount = getMessageCount(chatSheet, sessionId);

        const session = {
          Id: row[0],
          Customer: {
            SerialNumber: row[1],
            DeviceModel: row[2],
            Name: row[1], // SerialNumber를 Name으로도 사용
            Email: '',
            Phone: '',
            Company: ''
          },
          Status: row[3],
          StartedAt: row[4],
          EndedAt: row[5],
          AssignedStaff: row[6] || '',
          LastActivity: row[7],
          AttachmentRequested: row[8] || false,
          CurrentClaimId: row[9] || '',
          Messages: [] // 빈 배열로 설정 (필요시 별도 로드)
        };

        activeSessions.push(session);
      }
    }

    // 최근 활동 순으로 정렬
    activeSessions.sort((a, b) => new Date(b.LastActivity) - new Date(a.LastActivity));

    console.log(`활성 세션 ${activeSessions.length}개 조회 (ChatSessions 시트 기반)`);

    return createResponse(true, `${activeSessions.length}개 활성 세션 조회`, activeSessions);

  } catch (error) {
    console.error('활성 세션 조회 오류:', error);
    return createResponse(false, `활성 세션 조회 실패: ${error.message}`);
  }
}

/**
 * ChatMessages에서 활성 세션 조회 (폴백 함수)
 */
function getActiveSessionsFromMessages(chatSheet) {
  try {
    if (chatSheet.getLastRow() <= 1) {
      return createResponse(true, '활성 세션이 없습니다.', []);
    }

    const data = chatSheet.getDataRange().getValues();
    const sessions = new Map(); // sessionId -> session info

    // 최근 24시간 이내의 메시지만 고려
    const oneDayAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);

    for (let i = 1; i < data.length; i++) {
      const row = data[i];
      const sessionId = row[1]; // SessionID 컬럼
      const timestamp = new Date(row[3]); // Timestamp 컬럼
      const type = row[4]; // Type 컬럼
      const senderName = row[5]; // SenderName 컬럼

      // 24시간 이내의 메시지만 처리
      if (timestamp < oneDayAgo) continue;

      if (!sessions.has(sessionId)) {
        sessions.set(sessionId, {
          Id: sessionId,
          StartedAt: timestamp,
          LastActivity: timestamp,
          Status: 'Online',
          Customer: {
            SerialNumber: sessionId.split('_')[0] || 'Unknown',
            DeviceModel: 'L-CAM',
            Name: '고객',
            Email: '',
            Phone: '',
            Company: ''
          },
          AssignedStaff: '',
          AttachmentRequested: false,
          CurrentClaimId: '',
          Messages: []
        });
      }

      const session = sessions.get(sessionId);
      if (timestamp > session.LastActivity) {
        session.LastActivity = timestamp;
      }
      if (timestamp < session.StartedAt) {
        session.StartedAt = timestamp;
      }

      // 고객 메시지에서 실제 고객명 추출
      if (type === 'Customer' && senderName && senderName !== '고객' && senderName.trim() !== '') {
        session.Customer.Name = senderName;
        session.Customer.SerialNumber = senderName;
      }
    }

    // Map을 배열로 변환하고 최근 활동 순으로 정렬
    const activeSessions = Array.from(sessions.values())
      .sort((a, b) => new Date(b.LastActivity) - new Date(a.LastActivity));

    console.log(`활성 세션 ${activeSessions.length}개 조회 (ChatMessages 폴백)`);

    return createResponse(true, `${activeSessions.length}개 활성 세션 조회`, activeSessions);

  } catch (error) {
    console.error('활성 세션 조회 오류 (폴백):', error);
    return createResponse(false, `활성 세션 조회 실패: ${error.message}`);
  }
}

/**
 * 특정 세션의 메시지 수 계산
 */
function getMessageCount(chatSheet, sessionId) {
  try {
    if (chatSheet.getLastRow() <= 1) return 0;

    const data = chatSheet.getDataRange().getValues();
    let count = 0;

    for (let i = 1; i < data.length; i++) {
      if (data[i][1] === sessionId) { // SessionID 컬럼
        count++;
      }
    }

    return count;
  } catch (error) {
    console.error('메시지 수 계산 오류:', error);
    return 0;
  }
}

/**
 * 유틸리티 함수들
 */

function getSheet(spreadsheetId, sheetName) {
  try {
    console.log('스프레드시트 ID:', spreadsheetId);
    console.log('시트 이름:', sheetName);
    
    // 여기가 중요! openById 철자 확인
    const spreadsheet = SpreadsheetApp.openById(spreadsheetId);
    let sheet = spreadsheet.getSheetByName(sheetName);
    
    if (!sheet) {
      console.log('시트가 없어서 새로 생성:', sheetName);
      sheet = spreadsheet.insertSheet(sheetName);
    }
    
    return sheet;
  } catch (error) {
    console.error('getSheet 함수 오류:', error.message);
    console.error('스프레드시트 ID:', spreadsheetId);
    throw new Error(`스프레드시트 접근 실패: ${error.message}`);
  }
}

function createResponse(success, message, data = null) {
  const response = {
    Success: success,
    Message: message,
    Data: data,
    Timestamp: new Date().toISOString()
  };
  
  return ContentService
    .createTextOutput(JSON.stringify(response))
    .setMimeType(ContentService.MimeType.JSON);
}

function saveCustomer(customerSheet, customerData) {
  // 헤더 확인
  if (customerSheet.getLastRow() === 0) {
    customerSheet.getRange(1, 1, 1, 5).setValues([[
      'ID', 'Name', 'Email', 'Phone', 'Company'
    ]]);
  }
  
  const customerId = Utilities.getUuid();
  const row = [
    customerId,
    customerData.Name,
    customerData.Email,
    customerData.Phone,
    customerData.Company
  ];
  
  customerSheet.appendRow(row);
  return customerId;
}

function tryGenerateAIResponse(customerMessage) {
  const content = (customerMessage.Content || customerMessage.content || '').toLowerCase();
  
  // NLP 기반 의도 분석 (향후 WinForms에서 전송받을 수 있음)
  const nlpResult = analyzeMessageIntent(content);
  
  // 의도 기반 응답 생성
  const intentResponses = {
    // 키워드 제외
    // '환불': {
    //   content: '환불 관련 문의이시군요. 환불 사유와 주문 정보를 알려주시면 확인해드리겠습니다.',
    //   confidence: nlpResult.confidence || 0.9
    // },
    '배송문의': {
      content: '배송 관련 문의이시네요. 주문번호나 배송 주소 정보를 확인해주세요.',
      confidence: nlpResult.confidence || 0.8
    },
    '제품불량': {
      content: '제품에 문제가 있으시군요. 구체적인 증상을 알려주시면 해결 방안을 안내해드리겠습니다.',
      confidence: nlpResult.confidence || 0.85
    },
    '인사': {
      content: '안녕하세요! 무엇을 도와드릴까요?',
      confidence: nlpResult.confidence || 0.95
    },
    '일반문의': {
      content: '무엇이든 도와드리겠습니다. 궁금한 점을 자세히 말씀해주세요.',
      confidence: nlpResult.confidence || 0.7
    }
  };
  
  // 의도가 식별된 경우 해당 응답 반환
  if (nlpResult.intent && intentResponses[nlpResult.intent]) {
    return intentResponses[nlpResult.intent];
  }
  
  // 기존 키워드 기반 백업
  const keywordResponses = {
    '안녕': { content: '안녕하세요! 무엇을 도와드릴까요?', confidence: 0.9 },
    '문제': { content: '어떤 문제가 발생하셨나요? 자세히 알려주시면 도와드리겠습니다.', confidence: 0.8 }
    //'환불': { content: '환불 관련 문의이시군요. 환불 사유와 주문 정보를 알려주시면 확인해드리겠습니다.', confidence: 0.9 },
    //'배송': { content: '배송 관련 문의이시네요. 주문번호나 배송 주소 정보를 확인해주세요.', confidence: 0.8 }
  };
  
  for (const [keyword, response] of Object.entries(keywordResponses)) {
    if (content.includes(keyword)) {
      return response;
    }
  }
  
  return null;
}

// 간단한 의도 분석 함수 (나중에 더 정교하게 개선 가능)
function analyzeMessageIntent(content) {
  const intentPatterns = {
    // '환불': ['환불', '돈', '돌려', '취소', '반품'],
    // '배송문의': ['배송', '언제', '도착', '받을', '언제까지'],
    '제품불량': ['고장', '안돼', '작동', '문제', '이상'],
    '인사': ['안녕', '처음', '시작'],
    '일반문의': ['문의', '질문', '궁금', '도움']
  };
  
  for (const [intent, patterns] of Object.entries(intentPatterns)) {
    const matchCount = patterns.filter(pattern => content.includes(pattern)).length;
    if (matchCount > 0) {
      return {
        intent: intent,
        confidence: Math.min(0.7 + (matchCount * 0.1), 0.95)
      };
    }
  }
  
  return { intent: null, confidence: 0 };
}

function findBestMatch(learningSheet, question, category = null) {
  if (learningSheet.getLastRow() <= 1) return null;
  
  const data = learningSheet.getDataRange().getValues();
  let bestMatch = null;
  let bestScore = 0;
  let bestRowIndex = -1;
  
  for (let i = 1; i < data.length; i++) {
    const row = data[i];
    const savedQuestion = row[1]; // Question 컬럼
    const savedAnswer = row[2];   // Answer 컬럼
    const savedCategory = row[3]; // Category 컬럼
    const isApproved = row[8];    // IsApproved 컬럼 (승인된 답변만 사용)
    
    // 승인된 답변만 사용
    if (!isApproved) continue;
    
    // 간단한 유사도 계산 (키워드 매칭)
    const similarity = calculateSimilarity(question, savedQuestion);
    const categoryBonus = category && category === savedCategory ? 0.2 : 0;
    const totalScore = similarity + categoryBonus;
    
    if (totalScore > bestScore) {
      bestScore = totalScore;
      bestRowIndex = i + 1; // 스프레드시트 행 번호 (1부터 시작)
      bestMatch = {
        answer: savedAnswer,
        confidence: totalScore,
        originalQuestion: savedQuestion,
        category: savedCategory,
        rowIndex: bestRowIndex
      };
    }
  }
  
  return bestMatch;
}

function calculateSimilarity(text1, text2) {
  const words1 = text1.toLowerCase().split(/\s+/);
  const words2 = text2.toLowerCase().split(/\s+/);
  
  let commonWords = 0;
  for (const word of words1) {
    if (words2.includes(word)) {
      commonWords++;
    }
  }
  
  return commonWords / Math.max(words1.length, words2.length);
}

function extractKeywords(text) {
  const commonWords = ['은', '는', '이', '가', '을', '를', '에', '의', '와', '과', '에서'];
  const words = text.toLowerCase()
    .replace(/[^\w\s가-힣]/g, ' ')
    .split(/\s+/)
    .filter(word => word.length > 1 && !commonWords.includes(word))
    .slice(0, 10);
  
  return [...new Set(words)]; // 중복 제거
}

/**
 * 학습 데이터 사용량 증가
 */
function incrementUsageCount(learningSheet, rowIndex) {
  try {
    if (rowIndex > 0 && rowIndex <= learningSheet.getLastRow()) {
      const currentCount = learningSheet.getRange(rowIndex, 9).getValue() || 0; // UsageCount 컬럼
      learningSheet.getRange(rowIndex, 9).setValue(currentCount + 1);
      console.log(`사용량 증가: 행 ${rowIndex}, 새 카운트: ${currentCount + 1}`);
    }
  } catch (error) {
    console.error('사용량 증가 오류:', error);
  }
}

function sendClaimNotification(claimData, claimId) {
  try {
    const subject = `[클레임 알림] ${claimData.Title}`;
    const body = `
새로운 클레임이 생성되었습니다.

클레임 ID: ${claimId}
제목: ${claimData.Title}
카테고리: ${claimData.Category}
우선순위: ${claimData.Priority}
고객: ${claimData.Customer.Name}
생성시간: ${new Date().toLocaleString('ko-KR')}

상세 내용:
${claimData.Description}

관리자 페이지에서 확인해주세요.
    `;
    
    // 실제 사용시 수신자 이메일 설정 필요
    // MailApp.sendEmail('admin@company.com', subject, body);
    
  } catch (error) {
    console.error('클레임 알림 발송 오류:', error);
  }
}

/**
 * 학습 데이터 목록 조회 (ML 모델 학습용)
 */
function getLearningData(requestData = {}) {
  try {
    const limit = requestData.limit || 100;
    const learningSheet = SpreadsheetApp.openById(LEARNING_SHEET_ID).getSheetByName(SHEETS.LEARNING_DATA);
    
    if (!learningSheet) {
      return createResponse(false, '학습 데이터 시트를 찾을 수 없습니다');
    }
    
    const lastRow = learningSheet.getLastRow();
    if (lastRow <= 1) {
      return createResponse(true, '학습 데이터가 없습니다', []);
    }
    
    // 헤더 제외하고 데이터 읽기 (최대 limit개)
    const startRow = 2;
    const numRows = Math.min(lastRow - 1, limit);
    const dataRange = learningSheet.getRange(startRow, 1, numRows, 10); // 10개 컬럼
    const values = dataRange.getValues();
    
    const learningDataList = values.map(row => ({
      Id: row[0] || '',
      Question: row[1] || '',
      Answer: row[2] || '',
      Category: row[3] || '일반',
      CreatedAt: row[4] ? new Date(row[4]).toISOString() : new Date().toISOString(),
      StaffId: row[5] || '',
      QualityScore: parseFloat(row[6]) || 1.0,
      Keywords: row[7] ? row[7].split(',').map(k => k.trim()).filter(k => k) : [],
      IsApproved: row[8] === 'TRUE' || row[8] === true,
      UsageCount: parseInt(row[9]) || 0
    })).filter(data => data.Question && data.Answer); // 질문과 답변이 있는 데이터만
    
    console.log(`학습 데이터 ${learningDataList.length}개 조회됨`);
    return createResponse(true, `학습 데이터 ${learningDataList.length}개 조회됨`, learningDataList);
    
  } catch (error) {
    console.error('학습 데이터 조회 오류:', error);
    return createResponse(false, `학습 데이터 조회 실패: ${error.message}`);
  }
}

/**
 * 클레임 조회 처리
 */
function getClaim(queryData) {
  try {
    const { claimId } = queryData;
    
    if (!claimId) {
      return createResponse(false, '클레임 ID가 필요합니다.');
    }
    
    const claimSheet = getSheet(CLAIM_SHEET_ID, SHEETS.CLAIMS);
    const customerSheet = getSheet(CLAIM_SHEET_ID, SHEETS.CUSTOMERS);
    const chatSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_MESSAGES);
    
    if (claimSheet.getLastRow() <= 1) {
      return createResponse(false, '클레임을 찾을 수 없습니다.');
    }
    
    // 클레임 정보 조회
    const claimData = claimSheet.getDataRange().getValues();
    let claimInfo = null;
    let customerId = null;
    let chatSessionId = null;
    
    // 클레임 데이터 찾기 (ID 컬럼이 0번째)
    for (let i = 1; i < claimData.length; i++) {
      if (claimData[i][0] === claimId) {
        const row = claimData[i];
        customerId = row[9]; // CustomerID 컬럼
        chatSessionId = row[1]; // ChatSessionID 컬럼
        
        claimInfo = {
          Id: row[0],
          ChatSessionId: row[1],
          CreatedAt: row[2],
          UpdatedAt: row[3],
          Status: row[4],
          Category: row[5],
          Priority: row[6],
          Title: row[7],
          Description: row[8],
          CustomerId: row[9],
          AssignedStaff: row[10],
          Resolution: row[11]
        };
        break;
      }
    }
    
    if (!claimInfo) {
      return createResponse(false, '클레임을 찾을 수 없습니다.');
    }
    
    // 고객 정보 조회
    let customerInfo = null;
    if (customerId && customerSheet.getLastRow() > 1) {
      const customerData = customerSheet.getDataRange().getValues();
      for (let i = 1; i < customerData.length; i++) {
        if (customerData[i][0] === customerId) {
          const row = customerData[i];
          customerInfo = {
            Id: row[0],
            Name: row[1],
            Email: row[2],
            Phone: row[3],
            Company: row[4],
            SerialNumber: row[1] // Name을 SerialNumber로도 사용 (L-CAM 특성상)
          };
          break;
        }
      }
    }
    
    // 채팅 히스토리 요약 생성
    let chatSummary = null;
    if (chatSessionId && chatSheet.getLastRow() > 1) {
      chatSummary = generateChatSummary(chatSheet, chatSessionId);
    }
    
    // 최종 클레임 상세 정보 구성
    const claimDetails = {
      Id: claimInfo.Id,
      ChatSessionId: claimInfo.ChatSessionId,
      Title: claimInfo.Title,
      Description: claimInfo.Description,
      Category: mapCategoryToEnum(claimInfo.Category),
      Priority: mapPriorityToEnum(claimInfo.Priority),
      Status: claimInfo.Status,
      CreatedAt: claimInfo.CreatedAt,
      UpdatedAt: claimInfo.UpdatedAt,
      Customer: customerInfo || {
        SerialNumber: '',
        Email: '',
        Phone: '',
        Company: ''
      },
      ChatSummary: chatSummary,
      AssignedStaff: claimInfo.AssignedStaff,
      Resolution: claimInfo.Resolution
    };
    
    return createResponse(true, '클레임 정보 조회 성공', claimDetails);
    
  } catch (error) {
    console.error('클레임 조회 오류:', error);
    return createResponse(false, `클레임 조회 실패: ${error.message}`);
  }
}

/**
 * 채팅 요약 생성
 */
function generateChatSummary(chatSheet, sessionId) {
  try {
    const data = chatSheet.getDataRange().getValues();
    const messages = [];
    
    // 해당 세션의 메시지 수집
    for (let i = 1; i < data.length; i++) {
      const row = data[i];
      if (row[1] === sessionId) { // SessionID 컬럼
        messages.push({
          content: row[2], // Content 컬럼
          type: row[4], // Type 컬럼
          timestamp: row[3] // Timestamp 컬럼
        });
      }
    }
    
    if (messages.length === 0) {
      return '채팅 내역 없음';
    }
    
    // 시간순 정렬
    messages.sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
    
    // 고객 메시지만 추출 (최대 3개)
    const customerMessages = messages
      .filter(m => m.type === 'Customer')
      .slice(-3)
      .map(m => m.content);
    
    if (customerMessages.length === 0) {
      return '고객 메시지 없음';
    }
    
    // 요약 생성 (간단한 형태)
    const summary = `총 ${messages.length}개 메시지, 고객 메시지 ${customerMessages.length}개\n` +
                   `주요 내용: ${customerMessages.join(' | ')}`;
    
    return summary;
    
  } catch (error) {
    console.error('채팅 요약 생성 오류:', error);
    return '채팅 요약 생성 실패';
  }
}

/**
 * 카테고리를 C# enum 값으로 매핑
 */
function mapCategoryToEnum(category) {
  const categoryMap = {
    'Error Code 대응': 'ErrorCode',
    '가공 품질 이슈': 'ProcessingQuality', 
    '기구 문제': 'MechanicalIssue',
    '스핀들 문제': 'SpindleProblem',
    '누수 문제': 'LeakageProblem',
    '도구 관련': 'ToolIssue',
    '유지보수': 'Maintenance',
    '기타': 'Other'
  };
  
  return categoryMap[category] || 'Other';
}

/**
 * 우선순위를 C# enum 값으로 매핑
 */
function mapPriorityToEnum(priority) {
  const priorityMap = {
    '낮음': 'Low',
    '보통': 'Normal',
    '높음': 'High',
    '긴급': 'Emergency'
  };
  
  return priorityMap[priority] || 'Normal';
}

/**
 * 세션 생성 처리
 */
function createSession(sessionData) {
  try {
    const sessionSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_SESSIONS);

    // 헤더가 없으면 생성
    if (sessionSheet.getLastRow() === 0) {
      sessionSheet.getRange(1, 1, 1, 10).setValues([[
        'Id', 'Customer_SerialNumber', 'Customer_DeviceModel', 'Status',
        'StartedAt', 'EndedAt', 'AssignedStaff', 'LastActivity',
        'AttachmentRequested', 'CurrentClaimId'
      ]]);
    }

    // 세션 데이터 안전하게 처리
    const sessionId = sessionData.Id || sessionData.id || Utilities.getUuid();
    const serialNumber = sessionData.Customer?.SerialNumber || '';
    const deviceModel = sessionData.Customer?.DeviceModel || 'Unknown';
    const status = sessionData.Status || 'Online';
    const startedAt = new Date(sessionData.StartedAt || new Date());

    // 기존 세션이 있는지 확인
    const existingSession = findChatSession(sessionSheet, sessionId);
    if (existingSession) {
      return createResponse(false, '세션이 이미 존재합니다.');
    }

    // 새 세션 데이터 저장
    const row = [
      sessionId,
      serialNumber,
      deviceModel,
      status,
      startedAt,
      null, // EndedAt
      sessionData.AssignedStaff || '',
      startedAt, // LastActivity
      sessionData.AttachmentRequested || false,
      sessionData.CurrentClaimId || ''
    ];

    sessionSheet.appendRow(row);

    console.log('새 세션 생성:', sessionId);

    return createResponse(true, '세션이 생성되었습니다.', {
      Id: sessionId,
      Status: status,
      StartedAt: startedAt
    });

  } catch (error) {
    console.error('세션 생성 오류:', error);
    return createResponse(false, `세션 생성 실패: ${error.message}`);
  }
}

/**
 * 세션 업데이트 처리
 */
function updateSession(sessionData) {
  try {
    const sessionSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_SESSIONS);
    const sessionId = sessionData.Id || sessionData.id;

    if (!sessionId) {
      return createResponse(false, '세션 ID가 필요합니다.');
    }

    const rowIndex = findChatSessionRowIndex(sessionSheet, sessionId);
    if (rowIndex === -1) {
      return createResponse(false, '세션을 찾을 수 없습니다.');
    }

    // 업데이트할 데이터 설정
    if (sessionData.Status) {
      sessionSheet.getRange(rowIndex, 4).setValue(sessionData.Status); // Status 컬럼
    }
    if (sessionData.AssignedStaff !== undefined) {
      sessionSheet.getRange(rowIndex, 7).setValue(sessionData.AssignedStaff); // AssignedStaff 컬럼
    }
    if (sessionData.CurrentClaimId !== undefined) {
      sessionSheet.getRange(rowIndex, 10).setValue(sessionData.CurrentClaimId); // CurrentClaimId 컬럼
    }
    if (sessionData.AttachmentRequested !== undefined) {
      sessionSheet.getRange(rowIndex, 9).setValue(sessionData.AttachmentRequested); // AttachmentRequested 컬럼
    }

    // LastActivity 업데이트
    sessionSheet.getRange(rowIndex, 8).setValue(new Date()); // LastActivity 컬럼

    console.log('세션 업데이트:', sessionId);

    return createResponse(true, '세션이 업데이트되었습니다.');

  } catch (error) {
    console.error('세션 업데이트 오류:', error);
    return createResponse(false, `세션 업데이트 실패: ${error.message}`);
  }
}

/**
 * 세션 상태 업데이트 처리
 */
function updateSessionStatus(updateData) {
  try {
    const sessionSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_SESSIONS);
    const sessionId = updateData.sessionId;
    const newStatus = updateData.status;

    if (!sessionId || !newStatus) {
      return createResponse(false, '세션 ID와 상태가 필요합니다.');
    }

    const rowIndex = findChatSessionRowIndex(sessionSheet, sessionId);
    if (rowIndex === -1) {
      return createResponse(false, '세션을 찾을 수 없습니다.');
    }

    // 상태 업데이트
    sessionSheet.getRange(rowIndex, 4).setValue(newStatus); // Status 컬럼
    sessionSheet.getRange(rowIndex, 8).setValue(new Date()); // LastActivity 컬럼

    // 세션이 종료되는 경우 EndedAt 설정
    if (newStatus === 'Completed' || newStatus === 'Disconnected') {
      sessionSheet.getRange(rowIndex, 6).setValue(new Date()); // EndedAt 컬럼
    }

    console.log('세션 상태 업데이트:', sessionId, '->', newStatus);

    return createResponse(true, '세션 상태가 업데이트되었습니다.');

  } catch (error) {
    console.error('세션 상태 업데이트 오류:', error);
    return createResponse(false, `세션 상태 업데이트 실패: ${error.message}`);
  }
}

/**
 * 메시지 전송 시 세션 자동 생성/업데이트 (내부 함수)
 */
function createOrUpdateChatSession(sessionId, senderName, messageType) {
  try {
    const sessionSheet = getSheet(CHAT_SHEET_ID, SHEETS.CHAT_SESSIONS);

    // 헤더가 없으면 생성
    if (sessionSheet.getLastRow() === 0) {
      sessionSheet.getRange(1, 1, 1, 10).setValues([[
        'Id', 'Customer_SerialNumber', 'Customer_DeviceModel', 'Status',
        'StartedAt', 'EndedAt', 'AssignedStaff', 'LastActivity',
        'AttachmentRequested', 'CurrentClaimId'
      ]]);
    }

    const rowIndex = findChatSessionRowIndex(sessionSheet, sessionId);

    if (rowIndex === -1) {
      // 새 세션 생성
      const now = new Date();

      // 세션 ID에서 시리얼 번호 추출 (예: LM1234_SESSION_20250918... -> LM1234)
      const serialNumber = sessionId.split('_')[0] || senderName;
      const deviceModel = sessionId.includes('TEST') ? 'L-CAM_TEST' : 'L-CAM';

      const row = [
        sessionId,
        serialNumber,
        deviceModel,
        'Online', // 기본 상태
        now, // StartedAt
        null, // EndedAt
        '', // AssignedStaff
        now, // LastActivity
        false, // AttachmentRequested
        '' // CurrentClaimId
      ];

      sessionSheet.appendRow(row);
      console.log('새 세션 자동 생성:', sessionId);

    } else {
      // 기존 세션의 LastActivity 업데이트
      sessionSheet.getRange(rowIndex, 8).setValue(new Date()); // LastActivity 컬럼
      console.log('세션 활동 시간 업데이트:', sessionId);
    }

  } catch (error) {
    console.error('세션 자동 생성/업데이트 오류:', error);
  }
}

/**
 * 세션 찾기 (행 인덱스 반환)
 */
function findChatSessionRowIndex(sessionSheet, sessionId) {
  if (sessionSheet.getLastRow() <= 1) return -1;

  const data = sessionSheet.getDataRange().getValues();
  for (let i = 1; i < data.length; i++) {
    if (data[i][0] === sessionId) { // Id 컬럼
      return i + 1; // 스프레드시트 행 번호 (1부터 시작)
    }
  }
  return -1;
}

/**
 * 세션 찾기 (세션 데이터 반환)
 */
function findChatSession(sessionSheet, sessionId) {
  const rowIndex = findChatSessionRowIndex(sessionSheet, sessionId);
  if (rowIndex === -1) return null;

  const row = sessionSheet.getRange(rowIndex, 1, 1, 10).getValues()[0];
  return {
    Id: row[0],
    Customer: {
      SerialNumber: row[1],
      DeviceModel: row[2]
    },
    Status: row[3],
    StartedAt: row[4],
    EndedAt: row[5],
    AssignedStaff: row[6],
    LastActivity: row[7],
    AttachmentRequested: row[8],
    CurrentClaimId: row[9]
  };
}


/* ====================================================================
 * HUVITZ LM‑100 Support Additions (2025‑09)
 * - Creates support sheets (ErrorCodes, LEDStates, Routines, SetupFAQ, Procedures)
 * - Helper functions to detect error codes, map LED colors, compute maintenance due
 * - Safe: no business policy text; focuses on technical guidance reflected in the user manual
 * ==================================================================== */

/** Sheet names (do not modify unless you know what you're doing) */
const LM100_SHEETS = Object.freeze({
  ERROR_CODES: 'ErrorCodes',
  LED_STATES: 'LEDStates',
  ROUTINES: 'Routines',
  SETUP_FAQ: 'SetupFAQ',
  PROCEDURES: 'Procedures'
});

/** Public: one‑time installer to add an onOpen trigger that calls LM100_onOpen() */
function LM100_installOnOpenTrigger() {
  const ss = SpreadsheetApp.getActive();
  const ssId = ss.getId();
  const existing = ScriptApp.getProjectTriggers().filter(t => t.getHandlerFunction() === 'LM100_onOpen');
  if (existing.length === 0) {
    ScriptApp.newTrigger('LM100_onOpen').forSpreadsheet(ssId).onOpen().create();
  }
  LM100_onOpen();
}

/** Trigger target: adds menu for LM‑100 helpers */
function LM100_onOpen(e) { try { LM100_addMenu(); } catch (err) { /* no‑op */ } }

function LM100_addMenu() {
  const ui = SpreadsheetApp.getUi();
  ui.createMenu('LM‑100 Support')
    .addItem('① 시트 초기화/업데이트', 'LM100_initSupportSheets')
    .addItem('② 유지보수 Next Due 재계산', 'LM100_recalcRoutines')
    .addItem('➜ 매뉴얼 링크 열기', 'LM100_openManual')
    .addToUi();
}

/** Creates required sheets if absent; updates headers if present */
function LM100_initSupportSheets() {
  const ss = SpreadsheetApp.getActive();

  // ErrorCodes
  const ec = LM100_getSheet_(LM100_SHEETS.ERROR_CODES);
  LM100_upsertHeaders_(ec, ['Code','Title','Subsystem','Situation/Cause','Action1','Action2','Action3','SeeAlso','Severity']);
  LM100_appendIfEmpty_(ec, [
    ['7000','Window/Tank Open','Safety','Window or water tank open','Check window closed','Check water tank inserted','—','Ch.10','High'],
    ['701','Block Not Inserted','Mechanism','Block not inserted or misaligned','Insert block correctly','Tighten clamp (≥1.4 Nm)','—','Ch.6','Medium'],
    ['702','Bur Mounting Error','Tool','Bur not mounted or worn','Mount correct bur','Replace bur if worn','—','Ch.6','Medium']
  ]);
  ec.setFrozenRows(1);

  // LEDStates
  const ls = LM100_getSheet_(LM100_SHEETS.LED_STATES);
  LM100_upsertHeaders_(ls, ['UiText','State','OperatorAction']);
  LM100_appendIfEmpty_(ls, [
    ['흰/흰','READY','대기 상태'],
    ['보라/흰','MILLING','가공 중'],
    ['보라/초록','FINISH','가공 완료 — 적출/후처리'],
    ['빨강/빨강','ERROR','표시된 에러코드 확인 후 조치'],
    ['흰/파랑','WATER TANK OPENED','워터탱크 삽입/잠금 확인']
  ]);
  ls.setFrozenRows(1);

  // Routines
  const rt = LM100_getSheet_(LM100_SHEETS.ROUTINES);
  LM100_upsertHeaders_(rt, ['Item','LastDoneDate','CycleDays','NextDueDate','Notes']);
  LM100_appendIfEmpty_(rt, [
    ['WaterTank','','','', '청소/점검 후 Renewal 버튼 기록 준수'],
    ['Filter','','','', '필터 교체 주기 기록'],
    ['WaterPump','','','', '펌프 On/Off 확인'],
    ['AutoClean','','','', '오토클리닝 실행 후 배수 확인'],
    ['SpindleWarmup','','','', '필요 시 워밍업']
  ]);
  rt.setFrozenRows(1);

  // SetupFAQ
  const sf = LM100_getSheet_(LM100_SHEETS.SETUP_FAQ);
  LM100_upsertHeaders_(sf, ['Topic','Steps','Notes']);
  LM100_appendIfEmpty_(sf, [
    ['USB Upgrade','1) USB 삽입 → 2) 버전 선택 → 3) Upgrade 실행(약 50–60초) → 4) 완료 확인','전원·케이블 분리 금지'],
    ['Network (DHCP/Static IP)','설정 모드 진입 → DHCP/Static 선택 → IP/Mask/GW 입력 → 저장','변경 후 장비 재인식 필요할 수 있음'],
    ['Backup/Restore/Factory Reset','백업 파일 저장 → 복원 시 선택 → 초기화는 설정 값이 삭제됨','중요 설정은 백업 후 진행']
  ]);
  sf.setFrozenRows(1);

  // Procedures
  const pc = LM100_getSheet_(LM100_SHEETS.PROCEDURES);
  LM100_upsertHeaders_(pc, ['Name','Steps','Cautions','WhenToUse']);
  LM100_appendIfEmpty_(pc, [
    ['Residual Water Removal','워터펌프 모드 실행 → 순환 후 탱크 분리 → 배수 수행','감전/누수 주의, 장비 전원 상태 확인','운송/장거리 이동 전'],
    ['Nozzle Cleaning','수위 확인 → 스트레이너/트레이 청소 → 필터 교체 → 노즐 청소','압축공기 직분사 금지','분사 불량/냉각 문제 발생 시'],
    ['Multi‑Tank Switching','재료별 탱크 분리 운용 → 교체 시 색상 혼입 방지','탱크 결합 확인(LED 파랑→흰)','재료 전환 시']
  ]);
  pc.setFrozenRows(1);

  SpreadsheetApp.flush();
}

/** Recalculate NextDueDate = LastDoneDate + CycleDays for Routines */
function LM100_recalcRoutines() {
  const rt = LM100_getSheet_(LM100_SHEETS.ROUTINES);
  const values = rt.getDataRange().getValues();
  if (values.length <= 1) return;
  const header = values[0];
  const idxLast = header.indexOf('LastDoneDate');
  const idxCycle = header.indexOf('CycleDays');
  const idxNext = header.indexOf('NextDueDate');
  for (let r = 1; r < values.length; r++) {
    const last = values[r][idxLast];
    const cycle = Number(values[r][idxCycle]);
    if (last && !isNaN(new Date(last)) && cycle > 0) {
      const next = new Date(last);
      next.setDate(next.getDate() + cycle);
      rt.getRange(r+1, idxNext+1).setValue(next);
    }
  }
}

/** Helper: detect error code within free text and look up from ErrorCodes sheet */
function LM100_detectErrorCode(text) {
  const m = String(text||'').match(/\b(?:에러\s*코드|에러|Error(?:\s*Code)?)\s*[:#]?\s*(\d{1,4})\b/i);
  if (!m) return null;
  const code = m[1];
  const tbl = LM100_getTable_(LM100_SHEETS.ERROR_CODES);
  const row = tbl.find(r => String(r.Code) === String(code));
  return row || { Code: code, Title: 'Unknown', Subsystem: '', 'Situation/Cause':'', Action1:'', Action2:'', Action3:'', SeeAlso:'', Severity:'' };
}

/** Helper: parse LED pair like "보라/초록" or "purple/green" and map to state */
function LM100_mapLedToState(text) {
  const t = String(text||'').toLowerCase();
  const pairs = [
    ['흰/흰','white/white'],
    ['보라/흰','purple/white'],
    ['보라/초록','purple/green'],
    ['빨강/빨강','red/red'],
    ['흰/파랑','white/blue']
  ];
  let key = null;
  for (const [ko,en] of pairs) {
    const koN = ko.replace(/\s/g,'');
    const enN = en.replace(/\s/g,'');
    if (t.includes(koN) || t.includes(enN)) { key = ko; break; }
  }
  if (!key) return null;
  const tbl = LM100_getTable_(LM100_SHEETS.LED_STATES);
  return tbl.find(r => String(r.UiText).replace(/\s/g,'') === key.replace(/\s/g,'')) || null;
}

/** Compose a short support message from free text (error code or LED) */
function LM100_composeSupportMessage(freeText) {
  const parts = [];
  const ec = LM100_detectErrorCode(freeText);
  if (ec && ec.Title !== 'Unknown') {
    parts.push(`에러코드 ${ec.Code} — ${ec.Title}`);
    if (ec['Situation/Cause']) parts.push(`원인: ${ec['Situation/Cause']}`);
    const actions = [ec.Action1, ec.Action2, ec.Action3].filter(Boolean).map(a => `• ${a}`);
    if (actions.length) parts.push('조치:\n' + actions.join('\n'));
    if (ec.SeeAlso) parts.push(`참고: ${ec.SeeAlso}`);
  } else {
    const led = LM100_mapLedToState(freeText);
    if (led) {
      parts.push(`상태등(${led.UiText}) → ${led.State}`);
      if (led.OperatorAction) parts.push(`조치: ${led.OperatorAction}`);
    }
  }
  return parts.length ? parts.join('\n') : '';
}

/** Open manual link dialog (replace URL with your internal repository if needed) */
function LM100_openManual() {
  const html = HtmlService.createHtmlOutput(
    '<div style="font:14px system-ui,Arial;padding:16px;max-width:520px">' +
    '<h3>LM‑100 사용자 매뉴얼</h3>' +
    '<p><a target="_blank" href="https://intranet.example.com/manuals/LM-100_R3_20241031.pdf">사내 매뉴얼 열기</a><br>' +
    '※ 사내 저장소 URL로 교체하세요.</p>' +
    '</div>'
  ).setWidth(560).setHeight(180);
  SpreadsheetApp.getUi().showModalDialog(html, 'Manual');
}

/* ---------------- Internal helpers ---------------- */
function LM100_getSheet_(name) {
  const ss = SpreadsheetApp.getActive();
  let sh = ss.getSheetByName(name);
  if (!sh) sh = ss.insertSheet(name);
  return sh;
}
function LM100_upsertHeaders_(sh, headers) {
  const first = sh.getRange(1,1,1,headers.length).getValues()[0];
  let needs = false;
  for (let i=0;i<headers.length;i++) if (first[i] !== headers[i]) { needs = true; break; }
  if (needs) {
    sh.getRange(1,1,1,headers.length).setValues([headers]);
    sh.setFrozenRows(1);
    sh.getRange(1,1,1,headers.length).setFontWeight('bold').setBackground('#f1f5f9');
  }
}
function LM100_appendIfEmpty_(sh, rows) {
  const lastRow = sh.getLastRow();
  if (lastRow <= 1) {
    if (rows && rows.length) sh.getRange(2,1,rows.length,rows[0].length).setValues(rows);
  }
}
function LM100_getTable_(sheetName) {
  const sh = LM100_getSheet_(sheetName);
  const rng = sh.getDataRange();
  const values = rng.getValues();
  if (values.length <= 1) return [];
  const header = values[0];
  return values.slice(1).map(row => {
    const obj = {};
    header.forEach((h, i) => obj[h] = row[i]);
    return obj;
  });
}

/** Demo for console */
function LM100_demo() {
  Logger.log(LM100_composeSupportMessage('에러코드 7000'));
  Logger.log(LM100_composeSupportMessage('상태등이 보라/초록 입니다'));
}
/* ======================= END LM‑100 additions ======================= */
