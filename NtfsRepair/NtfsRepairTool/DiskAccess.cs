using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NtfsRepairTool
{
    /// <summary>
    /// Low-level disk access using Windows API
    /// </summary>
    public class DiskAccess : IDisposable
    {
        private SafeFileHandle? _handle;
        private readonly string _path;
        private bool _disposed;

        // P/Invoke declarations
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFilePointerEx(
            SafeFileHandle hFile,
            long liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        // Constants
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
        private const uint FILE_BEGIN = 0;
        private const uint FSCTL_LOCK_VOLUME = 0x00090018;
        private const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
        private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;

        public bool IsOpen => _handle != null && !_handle.IsInvalid && !_handle.IsClosed;

        public DiskAccess(string driveLetter)
        {
            // Convert drive letter to device path
            driveLetter = driveLetter.TrimEnd(':').ToUpper();
            _path = $@"\\.\{driveLetter}:";
        }

        public DiskAccess(int physicalDriveNumber)
        {
            _path = $@"\\.\PhysicalDrive{physicalDriveNumber}";
        }

        /// <summary>
        /// Open the disk for reading
        /// </summary>
        public bool OpenRead()
        {
            _handle = CreateFile(_path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            
            if (_handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"[!] Failed to open {_path} for reading. Error: {error}");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Open the disk for reading and writing (requires admin + volume lock)
        /// </summary>
        public bool OpenReadWrite()
        {
            _handle = CreateFile(_path, GENERIC_READ | GENERIC_WRITE, 
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 
                FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH, 
                IntPtr.Zero);
            
            if (_handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"[!] Failed to open {_path} for writing. Error: {error}");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Lock the volume for exclusive access
        /// </summary>
        public bool LockVolume()
        {
            if (!IsOpen) return false;
            
            uint bytesReturned;
            bool result = DeviceIoControl(_handle!, FSCTL_LOCK_VOLUME,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
            
            if (!result)
            {
                Console.WriteLine($"[!] Failed to lock volume. Error: {Marshal.GetLastWin32Error()}");
            }
            
            return result;
        }

        /// <summary>
        /// Unlock the volume
        /// </summary>
        public bool UnlockVolume()
        {
            if (!IsOpen) return false;
            
            uint bytesReturned;
            return DeviceIoControl(_handle!, FSCTL_UNLOCK_VOLUME,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
        }

        /// <summary>
        /// Dismount the volume
        /// </summary>
        public bool DismountVolume()
        {
            if (!IsOpen) return false;
            
            uint bytesReturned;
            return DeviceIoControl(_handle!, FSCTL_DISMOUNT_VOLUME,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);
        }

        /// <summary>
        /// Seek to a specific byte offset
        /// </summary>
        public bool Seek(long offset)
        {
            if (!IsOpen) return false;
            
            long newPosition;
            return SetFilePointerEx(_handle!, offset, out newPosition, FILE_BEGIN);
        }

        /// <summary>
        /// Read bytes from current position
        /// </summary>
        public byte[]? Read(int count)
        {
            if (!IsOpen) return null;
            
            byte[] buffer = new byte[count];
            uint bytesRead;
            
            if (ReadFile(_handle!, buffer, (uint)count, out bytesRead, IntPtr.Zero))
            {
                if (bytesRead < count)
                {
                    Array.Resize(ref buffer, (int)bytesRead);
                }
                return buffer;
            }
            
            return null;
        }

        /// <summary>
        /// Read bytes from a specific offset
        /// </summary>
        public byte[]? ReadAt(long offset, int count)
        {
            if (!Seek(offset)) return null;
            return Read(count);
        }

        /// <summary>
        /// Write bytes at current position
        /// </summary>
        public bool Write(byte[] data)
        {
            if (!IsOpen) return false;
            
            uint bytesWritten;
            return WriteFile(_handle!, data, (uint)data.Length, out bytesWritten, IntPtr.Zero)
                   && bytesWritten == data.Length;
        }

        /// <summary>
        /// Write bytes at a specific offset
        /// </summary>
        public bool WriteAt(long offset, byte[] data)
        {
            if (!Seek(offset)) return false;
            return Write(data);
        }

        /// <summary>
        /// Read and parse the NTFS boot sector
        /// </summary>
        public NtfsBootSector? ReadBootSector()
        {
            byte[]? data = ReadAt(0, 512);
            if (data == null) return null;
            
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<NtfsBootSector>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Read an MFT record
        /// </summary>
        public MftRecordHeader? ReadMftRecord(long recordOffset, int recordSize)
        {
            byte[]? data = ReadAt(recordOffset, recordSize);
            if (data == null || data.Length < Marshal.SizeOf<MftRecordHeader>()) 
                return null;
            
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<MftRecordHeader>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (IsOpen)
                {
                    UnlockVolume();
                    _handle?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
