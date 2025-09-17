#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import re
from datetime import datetime
from pathlib import Path

def analyze_logs():
    """디버그 로그 파일들을 분석하여 고객/직원 모드별로 정리"""

    log_dir = Path(os.path.expanduser("~/Desktop/ChatReporter_Logs"))

    if not log_dir.exists():
        print("로그 디렉터리가 없습니다:", log_dir)
        return

    log_files = list(log_dir.glob("*.log"))
    if not log_files:
        print("로그 파일이 없습니다.")
        return

    print(f"=== 채팅 애플리케이션 디버그 로그 분석 ===")
    print(f"로그 파일 수: {len(log_files)}")
    print()

    customer_logs = []
    staff_logs = []

    for log_file in sorted(log_files):
        print(f"📁 파일: {log_file.name}")

        with open(log_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        print(f"   라인 수: {len(lines)}")

        # 모드별로 분류
        if "Customer" in log_file.name:
            customer_logs.extend([(log_file.name, line) for line in lines])
        elif "Staff" in log_file.name:
            staff_logs.extend([(log_file.name, line) for line in lines])

        # 주요 이벤트 찾기
        session_events = [line for line in lines if "[SESSION]" in line]
        message_events = [line for line in lines if "[MESSAGE]" in line]
        api_events = [line for line in lines if "[API]" in line]
        ui_events = [line for line in lines if "[UI]" in line]
        error_events = [line for line in lines if "[ERROR]" in line]

        print(f"   세션 이벤트: {len(session_events)}")
        print(f"   메시지 이벤트: {len(message_events)}")
        print(f"   API 이벤트: {len(api_events)}")
        print(f"   UI 이벤트: {len(ui_events)}")
        print(f"   오류 이벤트: {len(error_events)}")

        if error_events:
            print("   🚨 오류 발견:")
            for error in error_events:
                print(f"      {error.strip()}")

        print()

    print("=== 모드별 분석 ===")

    print(f"\n🟦 고객 모드 로그 ({len(customer_logs)}개 이벤트):")
    analyze_mode_logs(customer_logs, "Customer")

    print(f"\n🟨 직원 모드 로그 ({len(staff_logs)}개 이벤트):")
    analyze_mode_logs(staff_logs, "Staff")

    print("\n=== 상호작용 분석 ===")
    analyze_interactions(customer_logs, staff_logs)

def analyze_mode_logs(logs, mode):
    """특정 모드의 로그를 분석"""

    if not logs:
        print(f"   {mode} 모드 로그가 없습니다.")
        return

    # 카테고리별 분류
    categories = {
        "SESSION": [],
        "MESSAGE": [],
        "API": [],
        "UI": [],
        "ERROR": []
    }

    for filename, line in logs:
        for category in categories.keys():
            if f"[{category}]" in line:
                categories[category].append((filename, line.strip()))
                break

    for category, events in categories.items():
        if events:
            print(f"   📊 {category}: {len(events)}개")
            for filename, event in events[-3:]:  # 최근 3개만 표시
                timestamp = extract_timestamp(event)
                content = event.split("] ", 3)[-1] if "] " in event else event
                print(f"      {timestamp} - {content}")
            if len(events) > 3:
                print(f"      ... 및 {len(events)-3}개 더")

def analyze_interactions(customer_logs, staff_logs):
    """고객과 직원 간의 상호작용 분석"""

    print("\n🔄 세션 상호작용:")

    # 세션 관련 이벤트만 추출
    customer_sessions = [log for log in customer_logs if "[SESSION]" in log[1]]
    staff_sessions = [log for log in staff_logs if "[SESSION]" in log[1]]

    print(f"   고객 세션 이벤트: {len(customer_sessions)}")
    print(f"   직원 세션 이벤트: {len(staff_sessions)}")

    # 메시지 상호작용
    customer_messages = [log for log in customer_logs if "[MESSAGE]" in log[1]]
    staff_messages = [log for log in staff_logs if "[MESSAGE]" in log[1]]

    print(f"   고객 메시지 이벤트: {len(customer_messages)}")
    print(f"   직원 메시지 이벤트: {len(staff_messages)}")

    # API 호출 비교
    customer_api = [log for log in customer_logs if "[API]" in log[1]]
    staff_api = [log for log in staff_logs if "[API]" in log[1]]

    print(f"   고객 API 호출: {len(customer_api)}")
    print(f"   직원 API 호출: {len(staff_api)}")

def extract_timestamp(log_line):
    """로그 라인에서 타임스탬프 추출"""
    match = re.search(r'\[(\d{2}:\d{2}:\d{2}\.\d{3})\]', log_line)
    return match.group(1) if match else "시간불명"

if __name__ == "__main__":
    analyze_logs()