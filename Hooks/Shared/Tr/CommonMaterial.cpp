#include "pch.h"

using namespace std;

namespace Tr
{
    void MaterialData::SetConstants(int pass, ShaderType shaderType, span<Vec4> values)
    {
        if (pass < 0 || pass >= 9)
            return;

        MaterialPass* pPass = Passes[pass];
        if (!pPass)
            return;

        span<Vec4> constants;
        switch (shaderType)
        {
            case ShaderType::PIXEL:
                constants = span(pPass->PixelConstants, pPass->NumPixelConstants);
                break;

            case ShaderType::VERTEX:
                constants = span(pPass->VertexConstants, pPass->NumVertexConstants);
                break;

            default:
                return;
        }
        if (values.size() != constants.size())
            return;

        memcpy(constants.data(), values.data(), values.size_bytes());
    }

    void CommonMaterial::SetConstants(int pass, ShaderType shaderType, span<Vec4> values)
    {
        if (pass < 0 || pass >= sizeof(_pPixelConstantBuffers) / sizeof(_pPixelConstantBuffers[0]))
            return;

        StaticContentBuffer_t* pBuffer;
        switch (shaderType)
        {
            case ShaderType::PIXEL:
                pBuffer = _pPixelConstantBuffers[pass];
                break;

            case ShaderType::VERTEX:
                pBuffer = _pVertexConstantBuffers[pass];
                break;

            default:
                return;
        }
        if (!pBuffer)
            return;

        pBuffer->SetConstants(values);
    }
}