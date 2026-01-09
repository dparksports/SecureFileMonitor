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
        private readonly Dictionary<string, SafeFileHandle> _volumeHandles = new();
        private readonly Dictionary<string, long> _nextUsnMap = new();

        public void Initialize(string driveLetter)
        {
            driveLetter = driveLetter.TrimEnd('\\');
            if (_volumeHandles.ContainsKey(driveLetter)) return;

            var volHandle = NativeMethods.CreateFile(
                $"\\\\.\\{driveLetter}",
                (uint)(FileAccess.Read | FileAccess.Write),
                (uint)(FileShare.Read | FileShare.Write),
                IntPtr.Zero,
                (uint)FileMode.Open,
                0,
                IntPtr.Zero
            );

            if (volHandle.IsInvalid)
            {
                 // CreateFile failed. Likely access denied or drive not supported.
                 // Log and continue.
                 return;
            }

            _volumeHandles[driveLetter] = volHandle;
            
            try 
            {
                CreateUsnJournal(driveLetter);
                var usnData = QueryJournal(driveLetter);
                _nextUsnMap[driveLetter] = usnData.NextUsn;
            }
            catch
            {
                _volumeHandles.Remove(driveLetter);
                volHandle.Dispose();
                throw;
            }
        }

        public async Task InitializeAllDrivesAsync()
        {
            await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed);
                foreach (var drive in drives)
                {
                    try 
                    {
                        Initialize(drive.Name);
                    }
                    catch (Exception) { /* Ignore failures for individual drives */ }
                }
            });
        }

        private void CreateUsnJournal(string driveLetter)
        {
            var cujd = new CREATE_USN_JOURNAL_DATA
            {
                MaximumSize = 0,
                AllocationDelta = 0
            };

            int size = Marshal.SizeOf(cujd);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(cujd, buffer, true);

            try
            {
                NativeMethods.DeviceIoControl(
                    _volumeHandles[driveLetter],
                    UsnConstants.FSCTL_CREATE_USN_JOURNAL,
                    buffer,
                    (uint)size,
                    IntPtr.Zero,
                    0,
                    out uint bytesReturned,
                    IntPtr.Zero
                );
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public UsnJournalData QueryJournal(string driveLetter)
        {
            var qujd = new USN_JOURNAL_DATA_V0();
            int size = Marshal.SizeOf(qujd);
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                bool result = NativeMethods.DeviceIoControl(
                    _volumeHandles[driveLetter],
                    UsnConstants.FSCTL_QUERY_USN_JOURNAL,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)size,
                    out uint bytesReturned,
                    IntPtr.Zero
                );

                if (!result) throw new IOException("Failed to query USN Journal");

                qujd = Marshal.PtrToStructure<USN_JOURNAL_DATA_V0>(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return new UsnJournalData
            {
                UsnJournalID = qujd.UsnJournalID,
                FirstUsn = qujd.FirstUsn,
                NextUsn = qujd.NextUsn,
                LowestValidUsn = qujd.LowestValidUsn,
                MaxUsn = qujd.MaxUsn,
                MaximumSize = qujd.MaximumSize,
                AllocationDelta = qujd.AllocationDelta
            };
        }

        public IEnumerable<FileEntry> ReadChanges(string driveLetter)
        {
            if (!_volumeHandles.ContainsKey(driveLetter)) yield break;

            // Setup input for READ_USN_JOURNAL
            READ_USN_JOURNAL_DATA_V0 readData = new READ_USN_JOURNAL_DATA_V0
            {
                StartUsn = _nextUsnMap[driveLetter],
                ReasonMask = 0xFFFFFFFF, // All reasons
                ReturnOnlyOnClose = 0,
                Timeout = 0,
                BytesToWaitFor = 0,
                UsnJournalID = QueryJournal(driveLetter).UsnJournalID
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
                    _volumeHandles[driveLetter],
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
                   _nextUsnMap[driveLetter] = nextUsnObj;

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

        public long GetCurrentUsn(string driveLetter)
        {
            return _nextUsnMap.ContainsKey(driveLetter) ? _nextUsnMap[driveLetter] : 0;
        }

        public void Dispose()
        {
            foreach (var h in _volumeHandles.Values)
                h.Dispose();
            _volumeHandles.Clear();
        }
    }
}
