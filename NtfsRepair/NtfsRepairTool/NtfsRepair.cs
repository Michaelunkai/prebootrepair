using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NtfsRepairTool
{
    /// <summary>
    /// NTFS filesystem repair operations
    /// </summary>
    public class NtfsRepair : IDisposable
    {
        private DiskAccess? _disk;
        private NtfsBootSector _bootSector;
        private string _driveLetter;
        private bool _readOnly = true;
        private List<string> _log = new();

        public bool IsOpen => _disk?.IsOpen ?? false;
        public IReadOnlyList<string> Log => _log;

        public NtfsRepair(string driveLetter)
        {
            _driveLetter = driveLetter.TrimEnd(':').ToUpper();
        }

        private void LogMessage(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _log.Add(entry);
            Console.WriteLine(entry);
        }

        /// <summary>
        /// Open the volume for analysis
        /// </summary>
        public bool Open(bool forWriting = false)
        {
            _disk = new DiskAccess(_driveLetter);
            _readOnly = !forWriting;

            if (forWriting)
            {
                if (!_disk.OpenReadWrite())
                {
                    LogMessage("Failed to open volume for writing");
                    return false;
                }
                
                LogMessage("Locking volume for exclusive access...");
                if (!_disk.LockVolume())
                {
                    LogMessage("Warning: Could not lock volume");
                }
            }
            else
            {
                if (!_disk.OpenRead())
                {
                    LogMessage("Failed to open volume for reading");
                    return false;
                }
            }

            LogMessage($"Opened {_driveLetter}: for {(forWriting ? "repair" : "analysis")}");
            return true;
        }

        /// <summary>
        /// Analyze the NTFS volume and return a report
        /// </summary>
        public RepairResult Analyze()
        {
            var result = new RepairResult();
            
            if (!IsOpen)
            {
                result.Message = "Volume not open";
                return result;
            }

            LogMessage("=== NTFS Volume Analysis ===");

            // 1. Read and validate boot sector
            LogMessage("Reading boot sector...");
            var bootSector = _disk!.ReadBootSector();
            
            if (bootSector == null)
            {
                result.Message = "Failed to read boot sector";
                result.ErrorsFound++;
                result.Details.Add("CRITICAL: Cannot read boot sector");
                return result;
            }

            _bootSector = bootSector.Value;
            
            // Validate boot sector
            if (!_bootSector.IsValid())
            {
                result.ErrorsFound++;
                result.Details.Add("CRITICAL: Invalid boot sector signature");
                
                // Check if OEM ID is corrupted
                string oemId = _bootSector.GetOemIdString();
                if (oemId != "NTFS")
                {
                    result.Details.Add($"  - OEM ID is '{oemId}' (expected 'NTFS')");
                }
                
                if (_bootSector.EndOfSectorMarker != 0xAA55)
                {
                    result.Details.Add($"  - End marker is 0x{_bootSector.EndOfSectorMarker:X4} (expected 0xAA55)");
                }
            }
            else
            {
                LogMessage("Boot sector: VALID");
                result.Details.Add("Boot sector: OK");
            }

            // Display volume info
            long clusterSize = _bootSector.GetClusterSize();
            int mftRecordSize = _bootSector.GetMftRecordSize();
            long volumeSize = (long)_bootSector.TotalSectors * _bootSector.BytesPerSector;
            
            LogMessage($"  Bytes per sector: {_bootSector.BytesPerSector}");
            LogMessage($"  Sectors per cluster: {_bootSector.SectorsPerCluster}");
            LogMessage($"  Cluster size: {clusterSize} bytes");
            LogMessage($"  MFT record size: {mftRecordSize} bytes");
            LogMessage($"  Volume size: {volumeSize / (1024 * 1024 * 1024.0):F2} GB");
            LogMessage($"  MFT starts at cluster: {_bootSector.MftCluster}");
            LogMessage($"  MFT mirror at cluster: {_bootSector.MftMirrorCluster}");

            // 2. Check MFT
            LogMessage("Checking Master File Table (MFT)...");
            long mftOffset = (long)_bootSector.MftCluster * clusterSize;
            
            var mftRecord = _disk.ReadMftRecord(mftOffset, mftRecordSize);
            if (mftRecord == null)
            {
                result.ErrorsFound++;
                result.Details.Add("CRITICAL: Cannot read MFT");
            }
            else if (!mftRecord.Value.IsValidSignature())
            {
                result.ErrorsFound++;
                result.Details.Add("CRITICAL: MFT record signature invalid");
                result.Details.Add($"  - Signature: 0x{mftRecord.Value.Signature:X8} (expected 0x454C4946 'FILE')");
            }
            else
            {
                LogMessage("MFT record 0 ($MFT): VALID");
                result.Details.Add("MFT: OK");
            }

            // 3. Check MFT Mirror
            LogMessage("Checking MFT Mirror...");
            long mftMirrorOffset = (long)_bootSector.MftMirrorCluster * clusterSize;
            
            var mftMirror = _disk.ReadMftRecord(mftMirrorOffset, mftRecordSize);
            if (mftMirror == null)
            {
                result.ErrorsFound++;
                result.Details.Add("WARNING: Cannot read MFT Mirror");
            }
            else if (!mftMirror.Value.IsValidSignature())
            {
                result.ErrorsFound++;
                result.Details.Add("WARNING: MFT Mirror signature invalid");
            }
            else
            {
                LogMessage("MFT Mirror: VALID");
                result.Details.Add("MFT Mirror: OK");
            }

            // 4. Check critical system files
            LogMessage("Checking critical system files...");
            string[] systemFiles = { "$MFT", "$MFTMirr", "$LogFile", "$Volume", "$AttrDef", 
                                    "$Root", "$Bitmap", "$Boot", "$BadClus", "$Secure" };
            
            for (int i = 0; i < Math.Min(10, systemFiles.Length); i++)
            {
                long recordOffset = mftOffset + (i * mftRecordSize);
                var record = _disk.ReadMftRecord(recordOffset, mftRecordSize);
                
                if (record == null || !record.Value.IsValidSignature())
                {
                    result.ErrorsFound++;
                    result.Details.Add($"WARNING: System file {systemFiles[i]} (record {i}) corrupted");
                }
                else if (!record.Value.IsInUse)
                {
                    result.ErrorsFound++;
                    result.Details.Add($"WARNING: System file {systemFiles[i]} not marked as in use");
                }
            }
            
            if (result.ErrorsFound == 0)
            {
                LogMessage("All critical system files: VALID");
            }

            // Summary
            LogMessage($"=== Analysis Complete ===");
            LogMessage($"Errors found: {result.ErrorsFound}");
            
            result.Success = result.ErrorsFound == 0;
            result.Message = result.Success 
                ? "Volume appears healthy" 
                : $"Found {result.ErrorsFound} issue(s) requiring attention";

            return result;
        }

        /// <summary>
        /// Attempt to repair detected issues
        /// </summary>
        public RepairResult Repair()
        {
            var result = new RepairResult();
            
            if (!IsOpen || _readOnly)
            {
                result.Message = "Volume not open for writing";
                return result;
            }

            LogMessage("=== NTFS Repair ===");

            // First analyze to find issues
            var analysis = Analyze();
            result.ErrorsFound = analysis.ErrorsFound;

            if (analysis.ErrorsFound == 0)
            {
                result.Success = true;
                result.Message = "No errors to repair";
                return result;
            }

            LogMessage($"Attempting to repair {analysis.ErrorsFound} issue(s)...");

            // 1. Try to restore boot sector from backup
            foreach (var detail in analysis.Details)
            {
                if (detail.Contains("boot sector"))
                {
                    LogMessage("Attempting boot sector repair from backup...");
                    if (RepairBootSector())
                    {
                        result.ErrorsFixed++;
                        result.Details.Add("Boot sector restored from backup");
                    }
                }
                
                if (detail.Contains("MFT Mirror"))
                {
                    LogMessage("Attempting MFT Mirror repair...");
                    if (RepairMftMirror())
                    {
                        result.ErrorsFixed++;
                        result.Details.Add("MFT Mirror repaired from MFT");
                    }
                }
            }

            result.Success = result.ErrorsFixed > 0;
            result.Message = $"Repaired {result.ErrorsFixed} of {result.ErrorsFound} issues";

            return result;
        }

        /// <summary>
        /// Attempt to repair the boot sector from the backup copy
        /// </summary>
        private bool RepairBootSector()
        {
            if (_disk == null) return false;

            // NTFS keeps a backup boot sector at the end of the volume
            long volumeSize = (long)_bootSector.TotalSectors * _bootSector.BytesPerSector;
            long backupOffset = volumeSize - 512;

            LogMessage($"Reading backup boot sector from offset {backupOffset}...");
            
            var backup = _disk.ReadAt(backupOffset, 512);
            if (backup == null)
            {
                LogMessage("Failed to read backup boot sector");
                return false;
            }

            // Verify backup is valid
            GCHandle handle = GCHandle.Alloc(backup, GCHandleType.Pinned);
            try
            {
                var backupSector = Marshal.PtrToStructure<NtfsBootSector>(handle.AddrOfPinnedObject());
                if (!backupSector.IsValid())
                {
                    LogMessage("Backup boot sector is also invalid");
                    return false;
                }

                // Write backup to primary location
                LogMessage("Writing backup boot sector to primary location...");
                if (_disk.WriteAt(0, backup))
                {
                    LogMessage("Boot sector restored successfully");
                    return true;
                }
            }
            finally
            {
                handle.Free();
            }

            return false;
        }

        /// <summary>
        /// Repair MFT Mirror from the primary MFT
        /// </summary>
        private bool RepairMftMirror()
        {
            if (_disk == null) return false;

            long clusterSize = _bootSector.GetClusterSize();
            int mftRecordSize = _bootSector.GetMftRecordSize();
            
            long mftOffset = (long)_bootSector.MftCluster * clusterSize;
            long mirrorOffset = (long)_bootSector.MftMirrorCluster * clusterSize;

            // Read first 4 records from MFT (the essential ones)
            int bytesToCopy = mftRecordSize * 4;
            
            LogMessage($"Copying MFT records to MFT Mirror...");
            
            var mftData = _disk.ReadAt(mftOffset, bytesToCopy);
            if (mftData == null)
            {
                LogMessage("Failed to read MFT data");
                return false;
            }

            if (_disk.WriteAt(mirrorOffset, mftData))
            {
                LogMessage("MFT Mirror updated successfully");
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            _disk?.Dispose();
        }
    }
}
