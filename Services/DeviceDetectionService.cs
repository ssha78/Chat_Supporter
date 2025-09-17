using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ChatSupporter.Models;

namespace ChatSupporter.Services;

public class DeviceDetectionService
{
    private readonly List<DeviceInfo> _detectedDevices = new();
    private readonly System.Threading.Timer _scanTimer;

    public event EventHandler<DeviceInfo>? DeviceDetected;

    public DeviceDetectionService()
    {
        _scanTimer = new System.Threading.Timer(ScanForDevices, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public IReadOnlyList<DeviceInfo> DetectedDevices => _detectedDevices.AsReadOnly();

    private void ScanForDevices(object? state)
    {
        try
        {
            ScanLCamDevices();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Device scan error: {ex.Message}");
        }
    }

    private void ScanLCamDevices()
    {
        // 레지스트리에서 L-CAM 설치 경로 가져오기
        var installPath = GetLCamInstallPath();

        if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
        {
            ScanDirectory(installPath);
        }
        else
        {
            // 레지스트리에서 경로를 찾지 못한 경우 기본 경로들 시도
            var basePaths = new[]
            {
                @"C:\Huvitz\Lilivis",
                @"C:\Program Files\Huvitz\Lilivis",
                @"C:\Program Files (x86)\Huvitz\Lilivis"
            };

            foreach (var basePath in basePaths)
            {
                if (Directory.Exists(basePath))
                {
                    ScanDirectory(basePath);
                }
            }
        }
    }

    private string GetLCamInstallPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Huvitz\L-CAM");
            if (key != null)
            {
                var installPath = key.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(installPath))
                {
                    Debug.WriteLine($"L-CAM 설치 경로 발견: {installPath}");
                    return installPath;
                }

                // InstallPath가 없으면 다른 가능한 키들 시도
                var possibleKeys = new[] { "Path", "Directory", "Location" };
                foreach (var keyName in possibleKeys)
                {
                    var path = key.GetValue(keyName) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        Debug.WriteLine($"L-CAM 경로 발견 ({keyName}): {path}");
                        return path;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"레지스트리 읽기 오류: {ex.Message}");
        }

        return string.Empty;
    }

    private void ScanDirectory(string path)
    {
        try
        {
            var directories = Directory.GetDirectories(path, "L-CAM*", SearchOption.TopDirectoryOnly);
            
            foreach (var dir in directories)
            {
                var executablePath = Path.Combine(dir, "L-CAM", "L-CAM.exe");
                var statusJsonPath = Path.Combine(dir, "L-CAM", "machine_status_Lilivis.json");

                if (File.Exists(executablePath))
                {
                    var serialNumber = ExtractSerialNumber(dir, statusJsonPath);
                    if (!string.IsNullOrEmpty(serialNumber))
                    {
                        var deviceInfo = new DeviceInfo
                        {
                            SerialNumber = serialNumber,
                            DeviceModel = "L-CAM",
                            ExecutablePath = executablePath,
                            StatusJsonPath = statusJsonPath,
                            InstallPath = dir,
                            LastDetected = DateTime.Now
                        };

                        UpdateDeviceList(deviceInfo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Directory scan error for {path}: {ex.Message}");
        }
    }

    private string ExtractSerialNumber(string installPath, string statusJsonPath)
    {
        try
        {
            // 1. JSON 파일에서 시리얼 번호 찾기
            if (File.Exists(statusJsonPath))
            {
                var jsonContent = File.ReadAllText(statusJsonPath);
                Debug.WriteLine($"JSON 내용: {jsonContent}");

                // 여러 가능한 시리얼 번호 키 패턴 시도
                var serialPatterns = new[]
                {
                    @"""serial_number""\s*:\s*""([^""]+)""",
                    @"""serialNumber""\s*:\s*""([^""]+)""",
                    @"""serial""\s*:\s*""([^""]+)""",
                    @"""deviceSerial""\s*:\s*""([^""]+)""",
                    @"""device_serial""\s*:\s*""([^""]+)""",
                    @"""SerialNumber""\s*:\s*""([^""]+)"""
                };

                foreach (var pattern in serialPatterns)
                {
                    var serialMatch = Regex.Match(jsonContent, pattern, RegexOptions.IgnoreCase);
                    if (serialMatch.Success)
                    {
                        var serial = serialMatch.Groups[1].Value;
                        Debug.WriteLine($"JSON에서 시리얼 번호 발견: {serial}");
                        return serial;
                    }
                }
            }

            // 2. 설치 폴더에서 다른 JSON 파일들 확인
            var jsonFiles = Directory.GetFiles(installPath, "*.json", SearchOption.AllDirectories)
                                   .Take(10); // 최대 10개 파일만 확인

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(jsonFile);
                    var serialPatterns = new[]
                    {
                        @"""serial_number""\s*:\s*""([^""]+)""",
                        @"""serialNumber""\s*:\s*""([^""]+)""",
                        @"""serial""\s*:\s*""([^""]+)"""
                    };

                    foreach (var pattern in serialPatterns)
                    {
                        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var serial = match.Groups[1].Value;
                            Debug.WriteLine($"{jsonFile}에서 시리얼 번호 발견: {serial}");
                            return serial;
                        }
                    }
                }
                catch
                {
                    // JSON 파일 읽기 실패는 무시
                    continue;
                }
            }

            // 3. 폴더명에서 추출 (최후 수단)
            var folderMatch = Regex.Match(installPath, @"L-CAM[_\-]?(\d+\.\d+\.\d+)");
            if (folderMatch.Success)
            {
                var generatedSerial = $"1LM2024X{folderMatch.Groups[1].Value.Replace(".", "")}";
                Debug.WriteLine($"폴더명에서 생성된 시리얼: {generatedSerial}");
                return generatedSerial;
            }

            // 4. 기본 시리얼 번호 생성
            var defaultSerial = $"L-CAM_{DateTime.Now:yyyyMMdd}_{Path.GetFileName(installPath)}";
            Debug.WriteLine($"기본 시리얼 번호 생성: {defaultSerial}");
            return defaultSerial;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Serial number extraction error: {ex.Message}");
            return $"L-CAM_UNKNOWN_{DateTime.Now:HHmmss}";
        }
    }

    private void UpdateDeviceList(DeviceInfo newDevice)
    {
        var existing = _detectedDevices.FirstOrDefault(d => d.SerialNumber == newDevice.SerialNumber);
        
        if (existing == null)
        {
            _detectedDevices.Add(newDevice);
            DeviceDetected?.Invoke(this, newDevice);
        }
        else
        {
            existing.LastDetected = newDevice.LastDetected;
        }
    }

    public void Dispose()
    {
        _scanTimer?.Dispose();
    }
}

public class DeviceInfo
{
    public string SerialNumber { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string StatusJsonPath { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public DateTime LastDetected { get; set; } = DateTime.Now;
    public bool IsConnected => (DateTime.Now - LastDetected).TotalMinutes < 5;
}