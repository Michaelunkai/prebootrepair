using System;
using System.Runtime.InteropServices;

namespace NtfsRepairTool
{
    /// <summary>
    /// NTFS Boot Sector structure (first 512 bytes of NTFS volume)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NtfsBootSector
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] JumpInstruction;        // 0x00 - Jump instruction
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] OemId;                  // 0x03 - "NTFS    "
        
        public ushort BytesPerSector;         // 0x0B - Usually 512
        public byte SectorsPerCluster;        // 0x0D - Usually 8 (4KB clusters)
        public ushort ReservedSectors;        // 0x0E - Must be 0
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Unused1;                // 0x10 - Must be 0
        
        public ushort Unused2;                // 0x13 - Must be 0
        public byte MediaDescriptor;          // 0x15 - Usually 0xF8 for hard disk
        public ushort Unused3;                // 0x16 - Must be 0
        public ushort SectorsPerTrack;        // 0x18
        public ushort NumberOfHeads;          // 0x1A
        public uint HiddenSectors;            // 0x1C
        public uint Unused4;                  // 0x20 - Must be 0
        public uint Unused5;                  // 0x24 - Usually 0x80 00 80 00
        public ulong TotalSectors;            // 0x28 - Total sectors in volume
        public ulong MftCluster;              // 0x30 - Cluster number of $MFT
        public ulong MftMirrorCluster;        // 0x38 - Cluster number of $MFTMirr
        public sbyte ClustersPerMftRecord;    // 0x40 - MFT record size
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Unused6;
        
        public sbyte ClustersPerIndexBlock;   // 0x44 - Index block size
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Unused7;
        
        public ulong VolumeSerialNumber;      // 0x48 - Volume serial number
        public uint Checksum;                 // 0x50 - Boot sector checksum
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 426)]
        public byte[] BootCode;               // 0x54 - Boot code
        
        public ushort EndOfSectorMarker;      // 0x1FE - Must be 0xAA55
        
        public string GetOemIdString() => System.Text.Encoding.ASCII.GetString(OemId).Trim();
        
        public int GetMftRecordSize()
        {
            if (ClustersPerMftRecord > 0)
                return ClustersPerMftRecord * SectorsPerCluster * BytesPerSector;
            else
                return 1 << (-ClustersPerMftRecord);
        }
        
        public long GetClusterSize() => SectorsPerCluster * BytesPerSector;
        
        public bool IsValid()
        {
            return GetOemIdString() == "NTFS" && 
                   EndOfSectorMarker == 0xAA55 &&
                   BytesPerSector >= 512 &&
                   SectorsPerCluster > 0;
        }
    }

    /// <summary>
    /// MFT Record Header (FILE record)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MftRecordHeader
    {
        public uint Signature;                // "FILE" = 0x454C4946
        public ushort UpdateSequenceOffset;   // Offset to update sequence
        public ushort UpdateSequenceSize;     // Size of update sequence
        public ulong LogFileSequenceNumber;   // $LogFile sequence number
        public ushort SequenceNumber;         // Record sequence number
        public ushort HardLinkCount;          // Number of hard links
        public ushort FirstAttributeOffset;   // Offset to first attribute
        public ushort Flags;                  // 0x01 = in use, 0x02 = directory
        public uint UsedSize;                 // Used size of record
        public uint AllocatedSize;            // Allocated size of record
        public ulong BaseRecordReference;     // Base record reference
        public ushort NextAttributeId;        // Next attribute ID
        
        public bool IsInUse => (Flags & 0x01) != 0;
        public bool IsDirectory => (Flags & 0x02) != 0;
        
        public bool IsValidSignature() => Signature == 0x454C4946; // "FILE"
    }

    /// <summary>
    /// NTFS Attribute Header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AttributeHeader
    {
        public uint AttributeType;            // Attribute type
        public uint Length;                   // Total length of attribute
        public byte NonResident;              // 0 = resident, 1 = non-resident
        public byte NameLength;               // Name length (in characters)
        public ushort NameOffset;             // Offset to name
        public ushort Flags;                  // Attribute flags
        public ushort AttributeId;            // Attribute ID
    }

    /// <summary>
    /// Common NTFS Attribute Types
    /// </summary>
    public static class AttributeTypes
    {
        public const uint StandardInformation = 0x10;
        public const uint AttributeList = 0x20;
        public const uint FileName = 0x30;
        public const uint ObjectId = 0x40;
        public const uint SecurityDescriptor = 0x50;
        public const uint VolumeName = 0x60;
        public const uint VolumeInformation = 0x70;
        public const uint Data = 0x80;
        public const uint IndexRoot = 0x90;
        public const uint IndexAllocation = 0xA0;
        public const uint Bitmap = 0xB0;
        public const uint ReparsePoint = 0xC0;
        public const uint End = 0xFFFFFFFF;
    }

    /// <summary>
    /// Disk repair result
    /// </summary>
    public class RepairResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int ErrorsFound { get; set; }
        public int ErrorsFixed { get; set; }
        public List<string> Details { get; set; } = new();
    }
}
