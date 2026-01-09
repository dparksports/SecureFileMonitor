using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SecureFileMonitor.Core.Models;
using SecureFileMonitor.Core.Native;

namespace SecureFileMonitor.Core.Services
{
    public class UsnJournalService : IUsnJournalService, IDisposable
    {
        private SafeFileHandle? _volumeHandle;
        private string _driveLetter = string.Empty;
        private long _nextUsn;

        public void Initialize(string driveLetter)
        {
            _driveLetter = driveLetter.TrimEnd('\\');
            string volumePath = $@"\\.\{_driveLetter}";
            
            _volumeHandle = NativeMethods.CreateFile(
                volumePath,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, // Write access needed for DeviceIoControl? Actually Read is usually enough, but sometimes Query needs specific rights. Docs say GENERIC_READ | GENERIC_WRITE recommended for USN ops.
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (_volumeHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open volume {_driveLetter}");
            }

            var journalData = QueryJournal();
            _nextUsn = journalData.NextUsn;
        }

        private USN_JOURNAL_DATA_V0 QueryJournal()
        {
            USN_JOURNAL_DATA_V0 journalData = new USN_JOURNAL_DATA_V0();
            int size = Marshal.SizeOf(journalData);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            try
            {
                if (!NativeMethods.DeviceIoControl(
                    _volumeHandle,
                    UsnConstants.FSCTL_QUERY_USN_JOURNAL,
                    IntPtr.Zero,
                    0,
                    ptr,
                    (uint)size,
                    out uint bytesReturned,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "FSCTL_QUERY_USN_JOURNAL failed");
                }

                journalData = Marshal.PtrToStructure<USN_JOURNAL_DATA_V0>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return journalData;
        }

        public IEnumerable<FileEntry> ReadChanges()
        {
            // Initial call logic or subsequent. For now, assume we continue from _nextUsn.
            // Setup input for READ_USN_JOURNAL
            READ_USN_JOURNAL_DATA_V0 readData = new READ_USN_JOURNAL_DATA_V0
            {
                StartUsn = _nextUsn,
                ReasonMask = 0xFFFFFFFF, // All reasons
                ReturnOnlyOnClose = 0,
                Timeout = 0,
                BytesToWaitFor = 0,
                UsnJournalID = QueryJournal().UsnJournalID
            };

            int inputSize = Marshal.SizeOf(readData);
            IntPtr inputPtr = Marshal.AllocHGlobal(inputSize);
            Marshal.StructureToPtr(readData, inputPtr, false);

            // Buffer for output (64KB is standard chunk)
            uint outputBufferSize = 8192 + 65536; 
            IntPtr outputBuffer = Marshal.AllocHGlobal((int)outputBufferSize);

            try
            {
                if (NativeMethods.DeviceIoControl(
                    _volumeHandle,
                    UsnConstants.FSCTL_READ_USN_JOURNAL,
                    inputPtr,
                    (uint)inputSize,
                    outputBuffer,
                    outputBufferSize,
                    out uint bytesReturned,
                    IntPtr.Zero))
                {
                   // Parse buffer
                   IntPtr currentPtr = new IntPtr(outputBuffer.ToInt64() + sizeof(long)); // First 8 bytes is the next USN
                   long nextUsnObj = Marshal.ReadInt64(outputBuffer);
                   _nextUsn = nextUsnObj;

                   long bytesLeft = bytesReturned - sizeof(long);
                   
                   while (bytesLeft > 0)
                   {
                        // Parse USN_RECORD_V2 (simplified assumption, usually confirm version)
                        // In reality, need to check RecordLength and Version at start of record.
                        
                        // Because StructLayout is sequential, we can read the header fields.
                        // But FileName is variable length, so manual marshalling is safer.
                        
                        var recordLength = (uint)Marshal.ReadInt32(currentPtr);
                        if (recordLength == 0) break; // Padding at end

                        // Manual read of fields we care about
                        // Offset 8: USN (Reference Number is at 8? No wait. Struct V2)
                        /*
                            uint RecordLength; // 0
                            ushort MajorVersion; // 4
                            ushort MinorVersion; // 6
                            ulong FileReferenceNumber; // 8
                            ulong ParentFileReferenceNumber; // 16
                            long Usn; // 24
                            long TimeStamp; // 32
                            uint Reason; // 40
                            ...
                            ushort FileNameLength; // 56
                            ushort FileNameOffset; // 58
                        */
                        
                        // Read simple fields
                        var reason = (uint)Marshal.ReadInt32(currentPtr, 40);
                        var fileNameLength = (ushort)Marshal.ReadInt16(currentPtr, 56);
                        var fileNameOffset = (ushort)Marshal.ReadInt16(currentPtr, 58);
                        var frn = (long)Marshal.ReadInt64(currentPtr, 8);
                        var pFrn = (long)Marshal.ReadInt64(currentPtr, 16);
                        var usn = (long)Marshal.ReadInt64(currentPtr, 24);
                        var timestamp = (long)Marshal.ReadInt64(currentPtr, 32);

                        string fileName = string.Empty;
                        if (fileNameLength > 0)
                        {
                            IntPtr namePtr = new IntPtr(currentPtr.ToInt64() + fileNameOffset);
                            fileName = Marshal.PtrToStringUni(namePtr, fileNameLength / 2);
                        }

                        yield return new FileEntry
                        {
                            FileName = fileName,
                            FileId = frn,
                            ParentId = pFrn,
                            Usn = usn,
                            FileAttributes = (uint)Marshal.ReadInt32(currentPtr, 52), // Need to add to model?
                            LastModified = DateTime.FromFileTime(timestamp)
                        };

                        currentPtr = new IntPtr(currentPtr.ToInt64() + recordLength);
                        bytesLeft -= recordLength;
                   }
                }
            }
            finally
            {
                 Marshal.FreeHGlobal(inputPtr);
                 Marshal.FreeHGlobal(outputBuffer);
            }
        }

        public long GetCurrentUsn()
        {
            return _nextUsn;
        }

        public void Dispose()
        {
            _volumeHandle?.Dispose();
        }
    }
}
