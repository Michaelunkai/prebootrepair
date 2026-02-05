using System;

namespace PreBootRepair.Models
{
    public enum RepairMode
    {
        Basic,      // Mark dirty bit - triggers autochk
        Standard,   // Schedule CHKDSK /F /R
        Advanced    // CHKDSK + SFC + DISM post-boot
    }

    public enum RepairStatus
    {
        NotScheduled,
        Scheduled,
        InProgress,
        Completed,
        Failed
    }

    public class DriveInfo
    {
        public string DriveLetter { get; set; } = "";
        public string Label { get; set; } = "";
        public string FileSystem { get; set; } = "";
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public bool IsSystemDrive { get; set; }
        public bool IsDirty { get; set; }
        public string SmartStatus { get; set; } = "Unknown";

        public double TotalGB => TotalBytes / (1024.0 * 1024.0 * 1024.0);
        public double FreeGB => FreeBytes / (1024.0 * 1024.0 * 1024.0);
        public double UsedPercent => TotalBytes > 0 ? (TotalBytes - FreeBytes) * 100.0 / TotalBytes : 0;

        public string DisplayName => string.IsNullOrEmpty(Label) 
            ? $"{DriveLetter} ({TotalGB:F1} GB)" 
            : $"{DriveLetter} ({Label}) - {TotalGB:F1} GB";
    }

    public class ScanResult
    {
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public string DriveLetter { get; set; } = "";
        public RepairMode Mode { get; set; }
        public int ErrorsFound { get; set; }
        public int ErrorsFixed { get; set; }
        public string Details { get; set; } = "";
        public int ExitCode { get; set; }
    }

    public class ScheduleInfo
    {
        public bool IsScheduled { get; set; }
        public RepairMode Mode { get; set; }
        public string TargetDrive { get; set; } = "";
        public DateTime ScheduledTime { get; set; }
    }
}
