#pragma once

#define TR_NUM_MATERIAL_PASSES (TR_VERSION == 11 ? 9 : 10)

namespace Tr
{
#if TR_VERSION == 9
    using StaticContentBuffer_t = PCDX11StaticConstantBuffer;
#else
    using StaticContentBuffer_t = PCDX12StaticConstantBuffer;
#endif

    struct MaterialPass
    {
        void* PixelShader;
        void* VertexShader;
        int NodeFlags;

        BYTE NumPixelTextures;
        BYTE NumPixelInstanceTextures;
        BYTE NumPixelMaterialTextures;
        BYTE FirstPixelInstanceTexture;
        void* PixelTextures;
        int NumPixelConstants;
        Vec4* PixelConstants;

        BYTE NumVertexTextures;
        BYTE NumVertexInstanceTextures;
        BYTE NumVertexMaterialTextures;
        BYTE FirstVertexInstanceTexture;
        void* VertexTextures;
        int NumVertexConstants;
        Vec4* VertexConstants;
    };

    struct MaterialData
    {
    public:
        int Version;
        int Id;
        int ColorWriteMask;
        int BlendMode;
        int BlendFactor;
        short FogType;
        short FadeMode;
        int Flags;
        int CombinedNodeFlags;
        int DebugColor;
        int AlphaBloomBlendMode;
        BYTE AlphaTestRef;
        BYTE DepthWriteAlphaThreshold;
        BYTE MaxLights;
        BYTE MaxShadowLights;
        struct
        {
            int FrontParams;
            int BackParams;
            int WriteMasks;
            int HiStencil;
        } StencilParams;
#if TR_VERSION > 9
        const char* MaterialTypeName;
#endif
        const char* ResourceName;
        int ResourceBaseNameOffset;
        int Magic;
        MaterialPass* Passes[TR_NUM_MATERIAL_PASSES];

        void SetConstants(int pass, ShaderType shaderType, std::span<Vec4> values);
    };

    class CommonMaterial
    {
    public:
        static CommonMaterial* GetById(int id);

        virtual void SetData(MaterialData* pData) = 0;
        virtual MaterialData* GetData() const = 0;

        void SetConstants(int pass, ShaderType shaderType, std::span<Vec4> values);

    private:
        BYTE _padding[TR_VERSION == 9 ? 0x2C : 0x20];
        StaticContentBuffer_t* _pPixelConstantBuffers[TR_NUM_MATERIAL_PASSES];
        StaticContentBuffer_t* _pVertexConstantBuffers[TR_NUM_MATERIAL_PASSES];
        StaticContentBuffer_t* _pHullConstantBuffers[TR_NUM_MATERIAL_PASSES];
        StaticContentBuffer_t* _pDomainConstantBuffers[TR_NUM_MATERIAL_PASSES];
    };
}

#undef TR_NUM_MATERIAL_PASSES
