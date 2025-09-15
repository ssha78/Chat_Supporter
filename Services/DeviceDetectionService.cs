using System.Diagnostics;
using System.Text.RegularExpressions;
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
            if (File.Exists(statusJsonPath))
            {
                var jsonContent = File.ReadAllText(statusJsonPath);
                var serialMatch = Regex.Match(jsonContent, @"""serial_number""\s*:\s*""([^""]+)""");
                if (serialMatch.Success)
                {
                    return serialMatch.Groups[1].Value;
                }
            }

            var folderMatch = Regex.Match(installPath, @"L-CAM[_\-]?(\d+\.\d+\.\d+)");
            if (folderMatch.Success)
            {
                return $"1LM2024X{folderMatch.Groups[1].Value.Replace(".", "")}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Serial number extraction error: {ex.Message}");
        }

        return string.Empty;
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