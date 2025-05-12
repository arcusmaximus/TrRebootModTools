using System;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
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
                CdcGame.Rise => new RiseMaterial(id, stream),
                CdcGame.Shadow => new ShadowMaterial(id, stream)
            };
        }

        private readonly byte[] _data;

        protected Material(int id, Stream stream, CdcGame game)
        {
            Id = id;
            _data = new byte[(int)stream.Length];
            stream.Read(_data, 0, _data.Length);

            MemoryStream memStream = new(_data);
            ResourceRefDefinitions refs = ResourceRefDefinitions.Create(null, memStream, game);
            int pointerSize = CdcGameInfo.Get(game).PointerSize;

            for (int passIdx = 0; passIdx < NumPasses; passIdx++)
            {
                int? passPos = refs.GetInternalRefTarget(refs.Size + PassRefsOffset + passIdx * pointerSize);
                if (passPos == null)
                    continue;

                (int psConstantsPos, Vec4[] psConstants) = ReadConstants(refs, passPos.Value, PsConstantsCountOffset, PsConstantsRefOffset);
                (int vsConstantsPos, Vec4[] vsConstants) = ReadConstants(refs, passPos.Value, VsConstantsCountOffset, VsConstantsRefOffset);
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

        private (int, Vec4[]) ReadConstants(ResourceRefDefinitions refs, int passPos, int constantsCountOffset, int constantsRefOffset)
        {
            int numConstants = BitConverter.ToInt32(_data, passPos + constantsCountOffset);
            Vec4[] constants = new Vec4[numConstants];

            int? constantsPos = refs.GetInternalRefTarget(passPos + constantsRefOffset);
            if (constantsPos == null)
                return (0, []);

            int constantPos = constantsPos.Value;
            for (int i = 0; i < numConstants; i++)
            {
                constants[i] = new Vec4(
                    BitConverter.ToSingle(_data, constantPos + 0x0),
                    BitConverter.ToSingle(_data, constantPos + 0x4),
                    BitConverter.ToSingle(_data, constantPos + 0x8),
                    BitConverter.ToSingle(_data, constantPos + 0xC)
                );
                constantPos += 0x10;
            }
            return (constantsPos.Value, constants);
        }

        public int Id { get; private set; }

        public List<Pass> Passes { get; } = new();

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
            stream.Write(_data, 0, _data.Length);
        }

        private unsafe void WriteConstants(int pos, Vec4[] constants)
        {
            fixed (byte* pData = _data)
            {
                float* pConstant = (float*)(pData + pos);
                for (int i = 0; i < constants.Length; i++)
                {
                    pConstant[0] = constants[i].X;
                    pConstant[1] = constants[i].Y;
                    pConstant[2] = constants[i].Z;
                    pConstant[3] = constants[i].W;
                    pConstant += 4;
                }
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
        }
    }
}
