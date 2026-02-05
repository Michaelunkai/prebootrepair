# PreBootRepair WinPE Component

This directory contains the **true pre-boot** disk repair components that run in Windows PE (Preinstallation Environment) **before Windows loads**.

## Components

### 1. NtfsRepairTool.exe (Tools/)
Custom NTFS filesystem repair tool with:
- **Direct sector access** - Reads raw disk sectors via Windows API
- **NTFS boot sector validation** - Checks OEM ID, sector sizes, MFT location
- **MFT (Master File Table) analysis** - Validates MFT record signatures
- **MFT Mirror verification** - Ensures backup MFT is intact
- **Critical system file checking** - Validates $MFT, $MFTMirr, $LogFile, etc.
- **Repair capabilities**:
  - Boot sector restoration from backup
  - MFT Mirror reconstruction from primary MFT

### 2. PreBootRepair.cmd (Scripts/)
Interactive menu-driven repair script offering:
- Custom NTFS deep analysis and repair
- CHKDSK quick/standard/deep scans
- Boot repair (bootrec commands)
- Integration with TestDisk/PhotoRec

### 3. Build-WinPE.ps1
PowerShell script to create a bootable WinPE ISO with all repair tools.

## Building the WinPE ISO

### Prerequisites
1. Install [Windows ADK](https://docs.microsoft.com/en-us/windows-hardware/get-started/adk-install)
   - Select "Deployment Tools"
   - Select "Windows Preinstallation Environment (WinPE)"

2. (Optional) Download [TestDisk](https://www.cgsecurity.org/testdisk-7.2.win64.zip)
   - Extract to `Tools\testdisk_win.exe` and `Tools\photorec_win.exe`

### Build Steps
```powershell
# Run as Administrator
cd PreBootRepair\WinPE
.\Build-WinPE.ps1 -OutputPath "PreBootRepair.iso"
```

### Manual Build (if ADK issues)
1. Create WinPE using `copype amd64 C:\WinPE`
2. Mount boot.wim: `dism /mount-wim /wimfile:C:\WinPE\media\sources\boot.wim /index:1 /mountdir:C:\mount`
3. Copy files:
   - `Scripts\PreBootRepair.cmd` → `C:\mount\Windows\System32\`
   - `Tools\NtfsRepairTool.exe` → `C:\mount\Tools\`
4. Update startnet.cmd to call PreBootRepair.cmd
5. Unmount: `dism /unmount-wim /mountdir:C:\mount /commit`
6. Create ISO with oscdimg

## Usage

### From Bootable USB/ISO
1. Boot from the PreBootRepair USB/ISO
2. WinPE will load and automatically launch PreBootRepair
3. Select your target drive
4. Choose repair option:
   - **Option 1**: NTFS Deep Analysis - See detailed filesystem health
   - **Option 2**: NTFS Auto-Repair - Fix detected issues
   - **Options 3-5**: Windows CHKDSK variants
   - **Option 6**: Boot repair for non-bootable systems

### From Windows (Limited)
The NtfsRepairTool can run from Windows but with limitations:
```cmd
# Analyze a drive
NtfsRepairTool.exe C:

# Attempt repair (may fail on locked volumes)
NtfsRepairTool.exe D: /repair
```

**Note**: For best results, always boot from WinPE. Windows locks system files and prevents full repair.

## Technical Details

### NTFS Structures Parsed
- Boot Sector (sector 0, 512 bytes)
- MFT Record Headers (FILE signature validation)
- Attribute headers (for system file verification)

### P/Invoke APIs Used
- `CreateFile` - Raw disk access via `\\.\X:` paths
- `ReadFile`/`WriteFile` - Sector I/O
- `DeviceIoControl` - Volume lock/unlock
- `SetFilePointerEx` - Seek to specific offsets

### Repair Operations
1. **Boot Sector Repair**: Copies backup boot sector (at volume end) to sector 0
2. **MFT Mirror Repair**: Copies first 4 MFT records from primary MFT to MFT Mirror location

## Comparison: This vs CHKDSK

| Feature | NtfsRepairTool | CHKDSK |
|---------|---------------|--------|
| Boot sector analysis | ✅ Detailed | ❌ No |
| MFT structure view | ✅ Yes | ❌ No |
| Works on locked volumes | ✅ From WinPE | ⚠️ Schedules for reboot |
| Bad sector scan | ❌ No | ✅ /R flag |
| File recovery | ❌ No | ⚠️ Limited |
| Open source | ✅ Yes | ❌ No |

This tool complements CHKDSK by providing visibility and repair for issues CHKDSK doesn't show.

## License
MIT License - Free for personal and commercial use.
