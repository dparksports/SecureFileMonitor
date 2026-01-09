using System;
using System.Runtime.InteropServices;

namespace SecureFileMonitor.Core.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct USN_JOURNAL_DATA_V0
    {
        public ulong UsnJournalID;
        public long FirstUsn;
        public long NextUsn;
        public long LowestValidUsn;
        public long MaxUsn;
        public long MaximumSize;
        public long AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct READ_USN_JOURNAL_DATA_V0
    {
        public long StartUsn;
        public uint ReasonMask;
        public uint ReturnOnlyOnClose;
        public ulong Timeout;
        public long BytesToWaitFor;
        public ulong UsnJournalID;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct USN_RECORD_V2
    {
        public uint RecordLength;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ulong FileReferenceNumber;
        public ulong ParentFileReferenceNumber;
        public long Usn;
        public long TimeStamp;
        public uint Reason;
        public uint SourceInfo;
        public uint SecurityId;
        public uint FileAttributes;
        public ushort FileNameLength;
        public ushort FileNameOffset;
        // FileName follows
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_USN_JOURNAL_DATA
    {
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }

    public static class UsnConstants
    {
        public const uint FSCTL_CREATE_USN_JOURNAL = 0x000900e7; // Correct code
        public const uint FSCTL_READ_USN_JOURNAL = 0x000900bb;
        public const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
        
        public const uint USN_REASON_DATA_OVERWRITE = 0x00000001;
        public const uint USN_REASON_DATA_EXTEND = 0x00000002;
        public const uint USN_REASON_DATA_TRUNCATION = 0x00000004;
        public const uint USN_REASON_FILE_CREATE = 0x00000100;
        public const uint USN_REASON_FILE_DELETE = 0x00000200;
        public const uint USN_REASON_RENAME_OLD_NAME = 0x00001000;
        public const uint USN_REASON_RENAME_NEW_NAME = 0x00002000;
        public const uint USN_REASON_CLOSE = 0x80000000;
    }
}
