#include "pch.h"
#include "MaterialConstantStore.h"

using namespace std;
using namespace Tr;

MaterialConstantStore MaterialConstantStore::Instance;

void MaterialConstantStore::Add(int materialId, int pass, ShaderType shaderType, const span<Vec4>& values)
{
    _constants[materialId][ConstantBufferKey { pass, shaderType }] = std::ranges::to<vector<Vec4>>(values);
}

void MaterialConstantStore::Clear()
{
    _constants.clear();
}

void MaterialConstantStore::Apply(int materialId, MaterialData* pMaterialData)
{
    auto materialIt = _constants.find(materialId);
    if (materialIt == _constants.end())
        return;

    for (auto pair : materialIt->second)
    {
        pMaterialData->SetConstants(pair.first.Pass, pair.first.ShaderType, pair.second);
    }
}
