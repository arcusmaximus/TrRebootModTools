using System;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class CdcHash
    {
        private static readonly Dictionary<CdcGame, CdcHash> Instances = new();

        private static CdcHash For(CdcGame game)
        {
            return Instances.GetOrAdd(
                game,
                v => v switch
                {
                    CdcGame.Tr2013 => new Tr2013Hash(),
                    CdcGame.Rise   => new RiseHash(),
                    CdcGame.Shadow => new ShadowHash(),
                    _ => throw new NotSupportedException()
                }
            );
        }

        public static ulong Calculate(string filePath, CdcGame game, bool convertPathSeparators = false)
        {
            if (convertPathSeparators && OperatingSystem.IsLinux())
                filePath = filePath.Replace('/', '\\');

            return For(game).Calculate(filePath);
        }

        public static string? Lookup(ulong hash, CdcGame game, bool convertPathSeparators = false)
        {
            string? filePath = For(game).Lookup(hash);
            if (filePath != null && convertPathSeparators && OperatingSystem.IsLinux())
                filePath = filePath.Replace('\\', '/');

            return filePath;
        }



        private readonly Dictionary<ulong, string> _lookupTable = new();

        protected CdcHash()
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, ListFileName);
            if (!File.Exists(filePath))
                return;

            using StreamReader reader = new StreamReader(filePath);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                _lookupTable[Calculate(line)] = line;
            }
        }

        protected abstract string ListFileName { get; }

        public abstract ulong Calculate(string str);

        protected static uint Calculate32(string str)
        {
            uint dwHash = 0xFFFFFFFFu;
            foreach (char c in str)
            {
                dwHash ^= (uint)c << 24;

                for (int j = 0; j < 8; j++)
                {
                    if ((dwHash & 0x80000000) != 0)
                    {
                        dwHash = (dwHash << 1) ^ 0x04C11DB7u;
                    }
                    else
                    {
                        dwHash <<= 1;
                    }
                }
            }
            return ~dwHash;
        }

        protected static ulong Calculate64(string str)
        {
            ulong dwHash = 0xCBF29CE484222325;
            foreach (char c in str)
            {
                dwHash = (dwHash ^ c) * 0x100000001B3;
            }
            return dwHash;
        }

        public string? Lookup(ulong hash)
        {
            return _lookupTable.GetValueOrDefault(hash);
        }

        public IEnumerable<ulong> Hashes => _lookupTable.Keys;
    }
}
