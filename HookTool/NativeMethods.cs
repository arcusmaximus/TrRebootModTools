using System;
using System.Runtime.InteropServices;

namespace TrRebootTools.HookTool
{
    internal static class NativeMethods
    {
        [DllImport("kernel32")]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32")]
        public static extern int SuspendThread(IntPtr hThread);

        public static void SuspendThreadChecked(IntPtr hThread)
        {
            if (SuspendThread(hThread) < 0)
                throw new Exception("Failed to suspend thread");
        }

        [DllImport("kernel32")]
        public static extern int ResumeThread(IntPtr hThread);

        public static void ResumeThreadChecked(IntPtr hThread)
        {
            if (ResumeThread(hThread) < 0)
                throw new Exception("Failed to resume thread");
        }

        [DllImport("kernel32")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int flAllocationType, int flProtect);

        public static IntPtr VirtualAllocExChecked(IntPtr hProcess, int dwSize, int flProtect)
        {
            IntPtr pMem = VirtualAllocEx(hProcess, IntPtr.Zero, dwSize, MEM_COMMIT, flProtect);
            if (pMem == IntPtr.Zero)
                throw new Exception("Failed to allocate memory");

            return pMem;
        }

        [DllImport("kernel32")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, int dwFreeType);

        [DllImport("kernel32")]
        public static extern ulong VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        public static MEMORY_BASIC_INFORMATION VirtualQueryExChecked(IntPtr hProcess, IntPtr lpAddress)
        {
            MEMORY_BASIC_INFORMATION info;
            ulong result = VirtualQueryEx(hProcess, lpAddress, out info, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
            if (result == 0)
                throw new Exception("Failed to query memory");

            return info;
        }

        [DllImport("kernel32")]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

        public static byte[] ReadProcessMemoryChecked(IntPtr hProcess, IntPtr address, int size)
        {
            byte[] data = new byte[size];
            if (!ReadProcessMemory(hProcess, address, data, size, out int actualReadSize) || actualReadSize != size)
                throw new Exception("Failed to read process memory");

            return data;
        }

        [DllImport("kernel32")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        public static void WriteProcessMemoryChecked(IntPtr hProcess, IntPtr address, byte[] data)
        {
            if (!WriteProcessMemory(hProcess, address, data, data.Length, out int actualWriteSize) || actualWriteSize != data.Length)
                throw new Exception("Failed to write process memory");
        }

        [DllImport("kernel32")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, int dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, int dwCreationFlags, out int lpThreadId);

        public static IntPtr CreateRemoteThreadChecked(IntPtr hProcess, IntPtr lpStartAddress, IntPtr lpParameter, out int lpThreadId)
        {
            IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, lpStartAddress, lpParameter, 0, out lpThreadId);
            if (hThread == IntPtr.Zero)
                throw new Exception("Failed to create remote thread");

            return hThread;
        }

        [DllImport("kernel32")]
        public static extern int WaitForSingleObject(IntPtr hObject, int dwMilliseconds);

        public static void WaitForSingleObjectChecked(IntPtr hObject, int dwMilliseconds)
        {
            if (WaitForSingleObject(hObject, dwMilliseconds) != WAIT_OBJECT_0)
                throw new TimeoutException();
        }

        [DllImport("kernel32")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32")]
        public static extern bool TerminateProcess(IntPtr hProcess, int uExitCode);

        public const int CREATE_SUSPENDED = 4;

        public const int MEM_COMMIT     = 0x00001000;
        public const int MEM_RELEASE    = 0x00008000;

        public const int PAGE_NOACCESS  = 0x01;
        public const int PAGE_READWRITE = 0x04;
        public const int PAGE_EXECUTE   = 0x10;
        public const int PAGE_GUARD     = 0x100;

        public const int WAIT_OBJECT_0 = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct IMAGE_DOS_HEADER
        {
            public short e_magic;
            public short e_cblp;
            public short e_cp;
            public short e_crlc;
            public short e_cparhdr;
            public short e_minalloc;
            public short e_maxalloc;
            public short e_ss;
            public short e_sp;
            public short e_csum;
            public short e_ip;
            public short e_cs;
            public short e_lfarlc;
            public short e_ovno;
            public fixed short e_res[4];
            public short e_oemid;
            public short e_oeminfo;
            public fixed short e_res2[10];
            public int e_lfanew;
        }

        public const short IMAGE_DOS_SIGNATURE  = 0x5A4D;
        public const int IMAGE_NT_SIGNATURE     = 0x00004550;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER
        {
            public short Machine;
            public short NumberOfSections;
            public int TimeDateStamp;
            public int PointerToSymbolTable;
            public int NumberOfSymbols;
            public short SizeOfOptionalHeader;
            public short Characteristics;
        }

        public const short IMAGE_FILE_DLL = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER32
        {
            public short Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public int SizeOfCode;
            public int SizeOfInitializedData;
            public int SizeOfUninitializedData;
            public int AddressOfEntryPoint;
            public int BaseOfCode;
            public int BaseOfData;
            public int ImageBase;
            public int SectionAlignment;
            public int FileAlignment;
            public short MajorOperatingSystemVersion;
            public short MinorOperatingSystemVersion;
            public short MajorImageVersion;
            public short MinorImageVersion;
            public short MajorSubsystemVersion;
            public short MinorSubsystemVersion;
            public int Win32VersionValue;
            public int SizeOfImage;
            public int SizeOfHeaders;
            public int CheckSum;
            public short Subsystem;
            public short DllCharacteristics;
            public int SizeOfStackReserve;
            public int SizeOfStackCommit;
            public int SizeOfHeapReserve;
            public int SizeOfHeapCommit;
            public int LoaderFlags;
            public int NumberOfRvaAndSizes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_OPTIONAL_HEADER64
        {
            public short Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public int SizeOfCode;
            public int SizeOfInitializedData;
            public int SizeOfUninitializedData;
            public int AddressOfEntryPoint;
            public int BaseOfCode;
            public long ImageBase;
            public int SectionAlignment;
            public int FileAlignment;
            public short MajorOperatingSystemVersion;
            public short MinorOperatingSystemVersion;
            public short MajorImageVersion;
            public short MinorImageVersion;
            public short MajorSubsystemVersion;
            public short MinorSubsystemVersion;
            public int Win32VersionValue;
            public int SizeOfImage;
            public int SizeOfHeaders;
            public int CheckSum;
            public short Subsystem;
            public short DllCharacteristics;
            public long SizeOfStackReserve;
            public long SizeOfStackCommit;
            public long SizeOfHeapReserve;
            public long SizeOfHeapCommit;
            public int LoaderFlags;
            public int NumberOfRvaAndSizes;
        }

        public const short IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10B;
        public const short IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20B;
        public const int IMAGE_DIRECTORY_ENTRY_IMPORT = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY
        {
            public int VirtualAddress;
            public int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_IMPORT_DESCRIPTOR
        {
            public int OriginalFirstThunk;
            public int TimeDateStamp;
            public int ForwarderChain;
            public int Name;
            public int FirstThunk;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public int AllocationProtect;
            public uint PartitionId;
            public long RegionSize;
            public int State;
            public int Protect;
            public int Type;
        }
    }
}
