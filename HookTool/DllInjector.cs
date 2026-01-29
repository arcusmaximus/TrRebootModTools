using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static TrRebootTools.HookTool.NativeMethods;

namespace TrRebootTools.HookTool
{
    internal static class DllInjector
    {
        private record ExeInfo(IntPtr ImageBase, IntPtr EntryPoint, IMAGE_DATA_DIRECTORY ImportDirectory, bool Is64Bit);

        public static IntPtr CreateProcessWithDll(string exePath, string? commandLine, string workingDirectory, string dllPath)
        {
            STARTUPINFO startInfo = new() { cb = Marshal.SizeOf<STARTUPINFO>() };
            PROCESS_INFORMATION processInfo;
            bool started = CreateProcess(
                exePath,
                !string.IsNullOrEmpty(commandLine) ? $"\"{exePath}\" {commandLine}" : null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_SUSPENDED,
                IntPtr.Zero,
                workingDirectory ?? Path.GetDirectoryName(exePath)!,
                ref startInfo,
                out processInfo
            );
            if (!started)
                throw new Exception("Failed to launch game.");

            IntPtr hProcess = processInfo.hProcess;
            IntPtr hThread = processInfo.hThread;
            try
            {
                ExeInfo exeInfo = GetExeInfo(hProcess);
                IntPtr ppLoadLibrary = FindLoadLibraryPointer(hProcess, exeInfo);
                RunToEntryPoint(hProcess, hThread, exeInfo);

                IntPtr pLoadLibrary = ReadProcessPointer(hProcess, ppLoadLibrary, exeInfo.Is64Bit);
                LoadDllIntoProcess(hProcess, exeInfo, pLoadLibrary, dllPath);
                ResumeThreadChecked(processInfo.hThread);
            }
            catch
            {
                TerminateProcess(hProcess, 0);
                CloseHandle(hProcess);
                throw;
            }
            finally
            {
                CloseHandle(hThread);
            }
            return hProcess;
        }

        private static unsafe ExeInfo GetExeInfo(IntPtr hProcess)
        {
            foreach (IntPtr pRegion in GetMemoryRegions(hProcess))
            {
                byte[] header = ReadProcessMemoryChecked(hProcess, pRegion, 0x1000);
                fixed (byte* pImage = header)
                {
                    IMAGE_DOS_HEADER* pDosHeader = (IMAGE_DOS_HEADER*)pImage;
                    if (pDosHeader->e_magic != IMAGE_DOS_SIGNATURE || pDosHeader->e_lfanew < 0 || pDosHeader->e_lfanew > 0x400)
                        continue;

                    byte* pNtHeaders = pImage + pDosHeader->e_lfanew;
                    int ntHeadersMagic = *(int*)pNtHeaders;
                    if (ntHeadersMagic != IMAGE_NT_SIGNATURE)
                        continue;

                    IMAGE_FILE_HEADER* pFileHeader = (IMAGE_FILE_HEADER*)(pNtHeaders + 4);
                    if ((pFileHeader->Characteristics & IMAGE_FILE_DLL) != 0)
                        continue;

                    byte* pOptionalHeaders = (byte*)pFileHeader + Marshal.SizeOf<IMAGE_FILE_HEADER>();
                    IntPtr pEntryPoint;
                    IMAGE_DATA_DIRECTORY* pDirectories;
                    bool is64Bit;
                    switch (*(short*)pOptionalHeaders)
                    {
                        case IMAGE_NT_OPTIONAL_HDR32_MAGIC:
                            pEntryPoint = pRegion + ((IMAGE_OPTIONAL_HEADER32*)pOptionalHeaders)->AddressOfEntryPoint;
                            pDirectories = (IMAGE_DATA_DIRECTORY*)(pOptionalHeaders + Marshal.SizeOf<IMAGE_OPTIONAL_HEADER32>());
                            is64Bit = false;
                            break;

                        case IMAGE_NT_OPTIONAL_HDR64_MAGIC:
                            pEntryPoint = pRegion + ((IMAGE_OPTIONAL_HEADER64*)pOptionalHeaders)->AddressOfEntryPoint;
                            pDirectories = (IMAGE_DATA_DIRECTORY*)(pOptionalHeaders + Marshal.SizeOf<IMAGE_OPTIONAL_HEADER64>());
                            is64Bit = true;
                            break;

                        default:
                            continue;
                    }

                    return new ExeInfo(pRegion, pEntryPoint, pDirectories[IMAGE_DIRECTORY_ENTRY_IMPORT], is64Bit);
                }
            }

            throw new Exception("Failed to find .exe memory region");
        }

        private static IEnumerable<IntPtr> GetMemoryRegions(IntPtr hProcess)
        {
            IntPtr pRegion = new IntPtr(0x10000);
            while (true)
            {
                if (VirtualQueryEx(hProcess, pRegion, out MEMORY_BASIC_INFORMATION info, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                if (info.State == MEM_COMMIT && (info.Protect & PAGE_NOACCESS) == 0 && (info.Protect & PAGE_GUARD) == 0)
                    yield return info.BaseAddress;

                pRegion = (IntPtr)((long)info.BaseAddress + info.RegionSize);
            }
        }

        private static unsafe IntPtr FindLoadLibraryPointer(IntPtr hProcess, ExeInfo exeInfo)
        {
            byte[] imports = ReadProcessMemoryChecked(hProcess, exeInfo.ImageBase + exeInfo.ImportDirectory.VirtualAddress, 0x8000);
            IMAGE_IMPORT_DESCRIPTOR dllEntry = GetDllImportEntry(exeInfo, imports, "kernel32.dll");
            return GetFunctionImportThunk(exeInfo, dllEntry, imports, "LoadLibraryW");
        }

        private static unsafe IMAGE_IMPORT_DESCRIPTOR GetDllImportEntry(ExeInfo exeInfo, byte[] imports, string dllName)
        {
            fixed (byte* pImports = imports)
            {
                byte* pImportsEnd = pImports + imports.Length;
                IMAGE_IMPORT_DESCRIPTOR* pDllEntry = (IMAGE_IMPORT_DESCRIPTOR*)pImports;
                while (pDllEntry < pImportsEnd && pDllEntry->OriginalFirstThunk != 0)
                {
                    if (ReadString(imports, pDllEntry->Name - exeInfo.ImportDirectory.VirtualAddress).Equals(dllName, StringComparison.InvariantCultureIgnoreCase))
                        return *pDllEntry;

                    pDllEntry++;
                }
            }
            throw new Exception($"Import entry for {dllName} not found");
        }

        private static unsafe IntPtr GetFunctionImportThunk(ExeInfo exeInfo, IMAGE_IMPORT_DESCRIPTOR dllEntry, byte[] imports, string functionName)
        {
            fixed (byte* pImports = imports)
            {
                int pointerSize = exeInfo.Is64Bit ? 8 : 4;

                byte* pThunk = pImports + (dllEntry.OriginalFirstThunk - exeInfo.ImportDirectory.VirtualAddress);
                int funcIdx = 0;
                while (pThunk < pImports + imports.Length)
                {
                    long thunk = exeInfo.Is64Bit ? *(long*)pThunk : *(int*)pThunk;
                    if (thunk == 0)
                        break;

                    if (thunk > 0 && ReadString(imports, (int)thunk - exeInfo.ImportDirectory.VirtualAddress + 2) == functionName)
                        return exeInfo.ImageBase + dllEntry.FirstThunk + funcIdx * pointerSize;

                    pThunk += pointerSize;
                    funcIdx++;
                }
            }
            throw new Exception($"Function {functionName} not found in imports");
        }

        private static void RunToEntryPoint(IntPtr hProcess, IntPtr hThread, ExeInfo exeInfo)
        {
            byte[] origInstr = ReadProcessMemoryChecked(hProcess, exeInfo.EntryPoint, 2);
            WriteProcessMemoryChecked(hProcess, exeInfo.EntryPoint, [0xEB, 0xFE]);
            ResumeThreadChecked(hThread);
            Thread.Sleep(1000);
            SuspendThreadChecked(hThread);
            WriteProcessMemoryChecked(hProcess, exeInfo.EntryPoint, origInstr);
        }

        private static void LoadDllIntoProcess(IntPtr hProcess, ExeInfo exeInfo, IntPtr pLoadLibrary, string dllPath)
        {
            IntPtr pDllPath = VirtualAllocExChecked(hProcess, 0x1000, PAGE_READWRITE);
            IntPtr hThread = IntPtr.Zero;
            try
            {
                byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath);
                WriteProcessMemoryChecked(hProcess, pDllPath, dllPathBytes);
                hThread = CreateRemoteThreadChecked(hProcess, pLoadLibrary, pDllPath, out _);
                WaitForSingleObjectChecked(hThread, 5000);
            }
            finally
            {
                VirtualFreeEx(hProcess, pDllPath, 0, MEM_RELEASE);
                if (hThread != IntPtr.Zero)
                    CloseHandle(hThread);
            }
        }

        private static string ReadString(byte[] data, int offset)
        {
            StringBuilder builder = new();
            while (data[offset] != 0)
            {
                builder.Append((char)data[offset++]);
            }
            return builder.ToString();
        }

        private static IntPtr ReadProcessPointer(IntPtr hProcess, IntPtr address, bool is64Bit)
        {
            if (is64Bit)
            {
                byte[] data = ReadProcessMemoryChecked(hProcess, address, 8);
                return (IntPtr)BitConverter.ToInt64(data, 0);
            }
            else
            {
                byte[] data = ReadProcessMemoryChecked(hProcess, address, 4);
                return (IntPtr)BitConverter.ToInt32(data, 0);
            }
        }
    }
}
