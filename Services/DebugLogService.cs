using System;
using System.IO;

namespace ChatSupporter.Services;

public class DebugLogService
{
    private readonly string _logDirectory;
    private readonly string _sessionId;
    private readonly string _mode;

    public DebugLogService(string mode, string sessionId = "")
    {
        _mode = mode; // "Customer" or "Staff"
        _sessionId = string.IsNullOrEmpty(sessionId) ? DateTime.Now.ToString("HHmmss") : sessionId;
        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ChatReporter_Logs");

        Directory.CreateDirectory(_logDirectory);
    }

    public void Log(string category, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{_mode}] [{category}] {message}";

        Console.WriteLine(logEntry);

        // 파일로도 저장
        var fileName = $"ChatReporter_{_mode}_{DateTime.Now:yyyyMMdd}_{_sessionId}.log";
        var filePath = Path.Combine(_logDirectory, fileName);

        try
        {
            File.AppendAllText(filePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // 로그 실패는 무시
        }
    }

    public void LogAPI(string action, string status, string details = "")
    {
        Log("API", $"{action} - {status}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
    }

    public void LogSession(string action, string sessionId, string details = "")
    {
        Log("SESSION", $"{action} - {sessionId}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
    }

    public void LogMessage(string action, string sender, string content, string details = "")
    {
        var shortContent = content.Length > 50 ? content.Substring(0, 50) + "..." : content;
        Log("MESSAGE", $"{action} - {sender}: {shortContent}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
    }

    public void LogUI(string action, string details = "")
    {
        Log("UI", $"{action}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
    }

    public void LogError(string action, string error)
    {
        Log("ERROR", $"{action} - {error}");
    }
}