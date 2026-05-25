using System;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Serialization;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class Material
    {
        public static Material Open(int id, Stream stream, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013Material(id, stream),
                CdcGame.Rise   => new RiseMaterial(id, stream),
                CdcGame.Shadow => new ShadowMaterial(id, stream),
                _ => throw new NotSupportedException()
            };
        }

        private readonly ResourceReader _reader;
        private readonly ArraySegment<byte> _data;

        protected Material(int id, Stream stream, CdcGame game)
        {
            Id = id;
            if (stream is not MemoryStream memStream || !memStream.TryGetBuffer(out _data))
            {
                _data = new byte[stream.Length];
                stream.Read(_data);
                memStream = new(_data.Array!, _data.Offset, _data.Count, true, true);
            }

            _reader = ResourceReader.Create(memStream, game);
            _reader.Seek(PassRefsOffset);
            ResourceRef?[] passRefs = _reader.ReadRefArray(NumPasses);
            for (int passIdx = 0; passIdx < NumPasses; passIdx++)
            {
                ResourceRef? passRef = passRefs[passIdx];
                if (passRef == null)
                    continue;

                (int psConstantsPos, Vec4[] psConstants) = ReadConstants(passRef, PsConstantsCountOffset, PsConstantsRefOffset);
                (int vsConstantsPos, Vec4[] vsConstants) = ReadConstants(passRef, VsConstantsCountOffset, VsConstantsRefOffset);
                Passes.Add(
                    new Pass
                    {
                        Index = passIdx,
                        PsConstantsPos = psConstantsPos,
                        PsConstants = psConstants,
                        VsConstantsPos = vsConstantsPos,
                        VsConstants = vsConstants
                    }
                );
            }
        }

        private (int, Vec4[]) ReadConstants(ResourceRef passRef, int constantsCountOffset, int constantsRefOffset)
        {
            _reader.Seek(passRef + constantsCountOffset);
            int numConstants = _reader.ReadInt32();

            _reader.Seek(passRef + constantsRefOffset);
            ResourceRef? constantsRef = _reader.ReadRef();
            if (constantsRef == null)
                return (0, []);

            _reader.Seek(constantsRef);
            int constantsPos = _reader.Position;
            Vec4[] constants = new Vec4[numConstants];
            for (int i = 0; i < numConstants; i++)
            {
                constants[i] = _reader.ReadStruct<Vec4>();
            }
            return (constantsPos, constants);
        }

        public int Id { get; private set; }

        public List<Pass> Passes { get; } = [];

        protected abstract int NumPasses { get; }
        protected abstract int PassRefsOffset { get; }

        protected abstract int PsConstantsCountOffset { get; }
        protected abstract int PsConstantsRefOffset { get; }
        
        protected abstract int VsConstantsCountOffset { get; }
        protected abstract int VsConstantsRefOffset { get; }

        public void Write(Stream stream)
        {
            foreach (Pass pass in Passes)
            {
                WriteConstants(pass.PsConstantsPos, pass.PsConstants);
                WriteConstants(pass.VsConstantsPos, pass.VsConstants);
            }
            stream.Write(_data);
        }

        private void WriteConstants(int pos, Vec4[] constants)
        {
            for (int i = 0; i < constants.Length; i++)
            {
                BitConverter.TryWriteBytes(_data.AsSpan(pos + 0x0, 4), constants[i].X);
                BitConverter.TryWriteBytes(_data.AsSpan(pos + 0x4, 4), constants[i].Y);
                BitConverter.TryWriteBytes(_data.AsSpan(pos + 0x8, 4), constants[i].Z);
                BitConverter.TryWriteBytes(_data.AsSpan(pos + 0xC, 4), constants[i].W);
                pos += 0x10;
            }
        }

        public class Pass
        {
            public int Index { get; init; }

            internal int PsConstantsPos { get; init; }
            internal Vec4[] PsConstants { get; init; }

            internal int VsConstantsPos { get; init; }
            internal Vec4[] VsConstants { get; init; }

            public Vec4[] GetConstants(ShaderType shaderType)
            {
                return shaderType switch
                {
                    ShaderType.Pixel => PsConstants,
                    ShaderType.Vertex => VsConstants
                };
            }

            public override string ToString()
            {
                return $"Pass {Index}";
            }
        }
    }
}
