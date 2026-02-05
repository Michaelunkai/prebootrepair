# PreBootRepair 🛡️

**A complete, free, open-source Windows disk repair solution with REAL pre-boot capabilities.**

This is NOT just a CHKDSK wrapper. It includes:
- **Custom NTFS repair tool** with direct sector access and MFT parsing
- **WinPE bootable environment** for true pre-boot repairs
- **Windows GUI application** for scheduling repairs

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4)
![License](https://img.shields.io/badge/License-MIT-green)

## 🎯 Components

### 1. NtfsRepairTool (Console)
Custom NTFS filesystem repair with direct disk access:
```
NtfsRepairTool.exe C:           # Analyze
NtfsRepairTool.exe C: /repair   # Fix issues
```

**Features:**
- Direct sector reading via Windows API (CreateFile, ReadFile)
- NTFS boot sector validation (OEM ID, sector sizes, checksums)
- MFT (Master File Table) structure parsing
- MFT Mirror verification
- Critical system file checking ($MFT, $MFTMirr, $LogFile, etc.)
- Boot sector restoration from backup
- MFT Mirror reconstruction

**Sample Output:**
```
[*] Target drive: C:
[*] Opening volume for analysis...
[*] === NTFS Volume Analysis ===
[*] Reading boot sector...
[*] Boot sector: VALID
[*]   Bytes per sector: 512
[*]   Sectors per cluster: 8
[*]   Cluster size: 4096 bytes
[*]   MFT record size: 1024 bytes
[*]   Volume size: 1860.68 GB
[*] MFT record 0 ($MFT): VALID
[*] MFT Mirror: VALID
[*] All critical system files: VALID
```

### 2. PreBootRepair GUI (WPF)
Modern Windows application with:
- Drive analysis and health checking
- Three repair modes (Basic, Standard, Advanced)
- Dark theme UI
- Command-line support
- Keyboard shortcuts

### 3. WinPE Boot Environment
Bootable repair environment that runs BEFORE Windows:
- Custom NtfsRepairTool integration
- CHKDSK, bootrec, diskpart access
- TestDisk/PhotoRec support
- Interactive menu-driven interface

## 📦 Downloads

| Component | Size | Description |
|-----------|------|-------------|
| `PreBootRepair.exe` | 68 MB | GUI application (self-contained) |
| `NtfsRepairTool.exe` | 34 MB | Console NTFS repair (self-contained) |
| `PreBootRepair.iso` | ~300 MB | Bootable WinPE (build required) |

## 🚀 Quick Start

### Option A: Windows GUI
1. Download `PreBootRepair.exe`
2. Run as Administrator
3. Select drive → Analyze → Schedule Repair → Reboot

### Option B: Console Tool
```powershell
# Analyze a drive
.\NtfsRepairTool.exe D:

# Repair detected issues
.\NtfsRepairTool.exe D: /repair
```

### Option C: True Pre-Boot (WinPE)
1. Build WinPE ISO (see WinPE/README.md)
2. Create bootable USB with Rufus
3. Boot from USB
4. Select repair options from menu

## 🔧 How It Works

### NTFS Direct Access
```csharp
// Open raw volume
CreateFile(@"\\.\C:", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, ...)

// Read boot sector (sector 0)
ReadFile(handle, buffer, 512, ...)

// Parse NTFS structures
NtfsBootSector bootSector = Marshal.PtrToStructure<NtfsBootSector>(buffer);
```

### Boot Sector Validation
Checks:
- OEM ID = "NTFS    " (8 bytes)
- End of sector marker = 0xAA55
- Bytes per sector (usually 512)
- Sectors per cluster (usually 8)
- MFT cluster location

### MFT Analysis
- Locates MFT using boot sector pointer
- Validates FILE signature (0x454C4946)
- Checks record flags (in-use, directory)
- Verifies all 10 critical system files

### Repair Operations
1. **Boot Sector**: Copy from backup (last sector of volume)
2. **MFT Mirror**: Copy first 4 records from primary MFT
3. **Critical Files**: Validate signatures and flags

## 🆚 Comparison

| Feature | PreBootRepair | CHKDSK | Commercial Tools |
|---------|--------------|--------|------------------|
| Open source | ✅ | ❌ | ❌ |
| Boot sector details | ✅ | ❌ | ✅ |
| MFT structure view | ✅ | ❌ | ✅ |
| True pre-boot | ✅ WinPE | ⚠️ autochk | ✅ |
| Bad sector scan | ⚠️ Via CHKDSK | ✅ | ✅ |
| Free | ✅ | ✅ | ❌ |
| GUI | ✅ | ❌ | ✅ |

## 📁 Project Structure

```
PreBootRepair/
├── src/                    # WPF GUI source
├── NtfsRepair/             # Console tool source
│   └── NtfsRepairTool/
│       ├── NtfsStructures.cs    # NTFS data structures
│       ├── DiskAccess.cs        # Raw disk I/O
│       ├── NtfsRepair.cs        # Repair logic
│       └── Program.cs           # CLI interface
├── WinPE/                  # Bootable environment
│   ├── Scripts/
│   │   └── PreBootRepair.cmd    # Boot menu script
│   ├── Tools/
│   │   └── NtfsRepairTool.exe   # Compiled tool
│   ├── Build-WinPE.ps1          # ISO builder
│   └── README.md
├── release_v1.0.0/         # Release package
└── README.md               # This file
```

## 🛠️ Building

### Prerequisites
- .NET 8 SDK
- Windows 10/11
- (For WinPE) Windows ADK

### Build Commands
```powershell
# Build GUI
cd PreBootRepair
dotnet publish -c Release -o dist

# Build Console Tool
cd NtfsRepair/NtfsRepairTool
dotnet publish -c Release -o ../../WinPE/Tools

# Build WinPE ISO (requires ADK)
cd WinPE
.\Build-WinPE.ps1
```

## ⚠️ Safety

- All operations use Windows native APIs
- Boot sector repair uses built-in backup copy
- No third-party drivers or kernel modifications
- Designed for recovery, not destruction
- Always maintain backups of important data

## 📜 License

MIT License - Free for personal and commercial use.

## 🤝 Contributing

Contributions welcome! Areas for improvement:
- [ ] More NTFS attribute parsing
- [ ] Directory index repair
- [ ] Bad sector detection without CHKDSK
- [ ] USB drive creation wizard
- [ ] Scheduled automatic health checks

## 🙏 Credits

Built by OpenClaw using:
- .NET 8, WPF, Win32 API
- NTFS filesystem documentation
- Windows PE (WinPE)

---

**⚠️ Disclaimer**: While designed to be safe, disk repair operations carry inherent risks. Always maintain backups of important data.
