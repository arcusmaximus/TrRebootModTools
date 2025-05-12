#pragma once

class MaterialConstantStore
{
public:
    static MaterialConstantStore Instance;

    void Add(int materialId, int pass, ShaderType shaderType, const std::span<Vec4>& values);
    void Clear();

    void Apply(int materialId, Tr::MaterialData* pMaterialData);

private:
    struct ConstantBufferKey
    {
        int Pass;
        ShaderType ShaderType;

        bool operator<(const ConstantBufferKey& other) const
        {
            if (Pass < other.Pass)
                return true;

            if (Pass > other.Pass)
                return false;

            return ShaderType < other.ShaderType;
        }
    };

    std::map<int, std::map<ConstantBufferKey, std::vector<Vec4> > > _constants;
};
