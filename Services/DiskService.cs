using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using PreBootRepair.Models;

namespace PreBootRepair.Services
{
    public class DiskService
    {
        private const string AppDataPath = @"C:\ProgramData\PreBootRepair";
        private const string ResultFilePath = @"C:\ProgramData\PreBootRepair\LastScanResult.txt";
        private const string LogPath = @"C:\ProgramData\PreBootRepair\Logs";
        private const string RegistryPath = @"SOFTWARE\PreBootRepair";
        private const string BootExecutePath = @"SYSTEM\CurrentControlSet\Control\Session Manager";

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            out uint lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_IS_VOLUME_DIRTY = 0x90078;
        private const uint VOLUME_IS_DIRTY = 0x00000001;

        public DiskService()
        {
            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            try
            {
                Directory.CreateDirectory(AppDataPath);
                Directory.CreateDirectory(LogPath);
            }
            catch { }
        }

        public List<Models.DriveInfo> GetAllDrives()
        {
            var drives = new List<Models.DriveInfo>();

            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    var info = new Models.DriveInfo
                    {
                        DriveLetter = drive.Name.TrimEnd('\\'),
                        Label = drive.VolumeLabel,
                        FileSystem = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        FreeBytes = drive.AvailableFreeSpace,
                        IsSystemDrive = drive.Name[0] == Path.GetPathRoot(Environment.SystemDirectory)?[0]
                    };

                    // Check if dirty
                    info.IsDirty = IsVolumeDirty(info.DriveLetter);
                    
                    drives.Add(info);
                }
            }

            return drives;
        }

        public bool IsVolumeDirty(string driveLetter)
        {
            try
            {
                string volumePath = $@"\\.\{driveLetter.TrimEnd(':')}:";
                IntPtr handle = CreateFile(volumePath, GENERIC_READ, 
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, 
                    OPEN_EXISTING, 0, IntPtr.Zero);

                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    return false;

                try
                {
                    uint flags;
                    uint bytesReturned;
                    bool result = DeviceIoControl(handle, FSCTL_IS_VOLUME_DIRTY,
                        IntPtr.Zero, 0, out flags, sizeof(uint), out bytesReturned, IntPtr.Zero);

                    if (result)
                        return (flags & VOLUME_IS_DIRTY) != 0;
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
            catch { }
            
            return false;
        }

        public async Task<string> GetSmartStatusAsync(int diskNumber = 0)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Get-PhysicalDisk | Where-Object DeviceId -eq {diskNumber} | Select-Object -ExpandProperty HealthStatus\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    output = output.Trim();
                    if (output.Contains("Healthy", StringComparison.OrdinalIgnoreCase))
                        return "✅ Healthy";
                    if (output.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                        return "⚠️ Warning";
                    if (output.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase))
                        return "❌ Unhealthy";
                }
            }
            catch { }

            return "Unknown";
        }

        public async Task<Models.DriveInfo> AnalyzeDriveAsync(string driveLetter)
        {
            var drives = GetAllDrives();
            var drive = drives.FirstOrDefault(d => d.DriveLetter.StartsWith(driveLetter[0].ToString(), StringComparison.OrdinalIgnoreCase));
            
            if (drive != null)
            {
                drive.SmartStatus = await GetSmartStatusAsync();
            }

            return drive ?? new Models.DriveInfo { DriveLetter = driveLetter };
        }

        public async Task<bool> ScheduleBasicRepairAsync(string driveLetter)
        {
            Log($"Scheduling basic repair for {driveLetter}");
            
            try
            {
                // Use fsutil to set dirty bit
                var result = await RunCommandAsync("fsutil", $"dirty set {driveLetter}");
                
                if (result.ExitCode == 0)
                {
                    SaveScheduleInfo(driveLetter, RepairMode.Basic);
                    Log("Basic repair scheduled successfully");
                    return true;
                }
                
                Log($"fsutil failed with exit code {result.ExitCode}: {result.Output}");
            }
            catch (Exception ex)
            {
                Log($"Error scheduling basic repair: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> ScheduleStandardRepairAsync(string driveLetter)
        {
            Log($"Scheduling standard repair for {driveLetter}");
            
            try
            {
                // Read current BootExecute value
                using var key = Registry.LocalMachine.OpenSubKey(BootExecutePath, true);
                if (key == null)
                {
                    Log("Failed to open Session Manager registry key");
                    return false;
                }

                var bootExecute = key.GetValue("BootExecute") as string[];
                var bootList = bootExecute?.ToList() ?? new List<string> { "autocheck autochk *" };

                // Build CHKDSK command: autocheck autochk /r \??\C:
                string driveChar = driveLetter.TrimEnd(':')[0].ToString().ToUpper();
                string chkdskCmd = $@"autocheck autochk /r \??\{driveChar}:";

                // Check if already scheduled
                bool alreadyScheduled = bootList.Any(e => e.Contains($@"\??\{driveChar}:"));
                
                if (!alreadyScheduled)
                {
                    bootList.Insert(0, chkdskCmd);
                    key.SetValue("BootExecute", bootList.ToArray(), RegistryValueKind.MultiString);
                }

                SaveScheduleInfo(driveLetter, RepairMode.Standard);
                Log("Standard CHKDSK repair scheduled via BootExecute");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error scheduling standard repair: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> ScheduleAdvancedRepairAsync(string driveLetter)
        {
            Log($"Scheduling advanced repair for {driveLetter}");
            
            try
            {
                // First schedule standard CHKDSK
                bool chkdskOk = await ScheduleStandardRepairAsync(driveLetter);
                
                // Also create a scheduled task for SFC/DISM after boot
                string taskXml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <BootTrigger>
      <StartBoundary>2024-01-01T00:00:00</StartBoundary>
      <Enabled>true</Enabled>
      <Delay>PT2M</Delay>
    </BootTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT4H</ExecutionTimeLimit>
    <DeleteExpiredTaskAfter>PT0S</DeleteExpiredTaskAfter>
  </Settings>
  <Actions>
    <Exec>
      <Command>cmd.exe</Command>
      <Arguments>/c sfc /scannow &amp;&amp; DISM /Online /Cleanup-Image /RestoreHealth</Arguments>
    </Exec>
  </Actions>
</Task>";

                string taskFile = Path.Combine(AppDataPath, "PostBootRepair.xml");
                await File.WriteAllTextAsync(taskFile, taskXml);

                var result = await RunCommandAsync("schtasks", 
                    $"/create /tn \"PreBootRepair_PostBoot\" /xml \"{taskFile}\" /f");
                
                if (result.ExitCode == 0)
                {
                    SaveScheduleInfo(driveLetter, RepairMode.Advanced);
                    Log("Advanced repair scheduled (CHKDSK + post-boot SFC/DISM)");
                    return chkdskOk;
                }

                Log($"Failed to create scheduled task: {result.Output}");
            }
            catch (Exception ex)
            {
                Log($"Error scheduling advanced repair: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> CancelScheduledRepairAsync()
        {
            Log("Cancelling scheduled repair");
            
            try
            {
                // Remove from BootExecute
                using var key = Registry.LocalMachine.OpenSubKey(BootExecutePath, true);
                if (key != null)
                {
                    var bootExecute = key.GetValue("BootExecute") as string[];
                    if (bootExecute != null)
                    {
                        var filtered = bootExecute
                            .Where(e => !e.Contains(@"\??") || e == "autocheck autochk *")
                            .ToArray();
                        
                        if (filtered.Length == 0)
                            filtered = new[] { "autocheck autochk *" };
                        
                        key.SetValue("BootExecute", filtered, RegistryValueKind.MultiString);
                    }
                }

                // Remove scheduled task
                await RunCommandAsync("schtasks", "/delete /tn \"PreBootRepair_PostBoot\" /f");

                // Clear registry status
                ClearScheduleInfo();
                
                Log("Scheduled repair cancelled");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error cancelling repair: {ex.Message}");
            }

            return false;
        }

        public ScheduleInfo GetScheduleInfo()
        {
            var info = new ScheduleInfo();
            
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
                if (key != null)
                {
                    info.IsScheduled = key.GetValue("RepairScheduled")?.ToString() == "1";
                    info.TargetDrive = key.GetValue("TargetDrive")?.ToString() ?? "";
                    
                    if (Enum.TryParse<RepairMode>(key.GetValue("RepairMode")?.ToString(), out var mode))
                        info.Mode = mode;
                    
                    if (DateTime.TryParse(key.GetValue("ScheduledTime")?.ToString(), out var time))
                        info.ScheduledTime = time;
                }
            }
            catch { }

            return info;
        }

        private void SaveScheduleInfo(string driveLetter, RepairMode mode)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(RegistryPath);
                key?.SetValue("RepairScheduled", "1");
                key?.SetValue("TargetDrive", driveLetter);
                key?.SetValue("RepairMode", mode.ToString());
                key?.SetValue("ScheduledTime", DateTime.Now.ToString("o"));
            }
            catch { }
        }

        private void ClearScheduleInfo()
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(RegistryPath);
                key?.SetValue("RepairScheduled", "0");
            }
            catch { }
        }

        public async Task<bool> RebootAsync(int delaySeconds = 0)
        {
            Log($"Initiating reboot with {delaySeconds}s delay");
            
            try
            {
                var result = await RunCommandAsync("shutdown", 
                    $"/r /t {delaySeconds} /c \"PreBootRepair: Disk repair will run on next boot\"");
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log($"Reboot failed: {ex.Message}");
            }

            return false;
        }

        public ScanResult? GetLastScanResult()
        {
            try
            {
                if (File.Exists(ResultFilePath))
                {
                    var lines = File.ReadAllLines(ResultFilePath);
                    var result = new ScanResult();
                    
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            switch (parts[0])
                            {
                                case "SUCCESS": result.Success = parts[1] == "1"; break;
                                case "TIMESTAMP": DateTime.TryParse(parts[1], out var ts); result.Timestamp = ts; break;
                                case "DRIVE": result.DriveLetter = parts[1]; break;
                                case "ERRORS_FOUND": int.TryParse(parts[1], out var ef); result.ErrorsFound = ef; break;
                                case "ERRORS_FIXED": int.TryParse(parts[1], out var efx); result.ErrorsFixed = efx; break;
                                case "DETAILS": result.Details = parts[1]; break;
                                case "EXIT_CODE": int.TryParse(parts[1], out var ec); result.ExitCode = ec; break;
                            }
                        }
                    }
                    
                    return result;
                }
            }
            catch { }

            return null;
        }

        private async Task<(int ExitCode, string Output)> RunCommandAsync(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (-1, "Failed to start process");

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output + error);
        }

        private void Log(string message)
        {
            try
            {
                string logFile = Path.Combine(LogPath, $"PreBootRepair_{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
                File.AppendAllText(logFile, entry + Environment.NewLine);
            }
            catch { }
        }
    }
}
