#pragma once

//#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#if TR_VERSION == 9
    #include <d3d11.h>
#else
    #include <d3d12.h>
#endif

#include <stdint.h>
#include <algorithm>
#include <filesystem>
#include <format>
#include <fstream>
#include <map>
#include <ranges>
#include <regex>
#include <span>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "../vendor/Detours/src/detours.h"

typedef uint64_t QWORD;

enum class ShaderType
{
    PIXEL,
    VERTEX
};

struct Vec4
{
    float x;
    float y;
    float z;
    float w;
};

#include "Util/CaseInsensitiveLess.h"
#include "Util/mem_func_ptr.h"

namespace Tr
{
    struct ResourceLocation;

#if TR_VERSION == 11
    using Locale_t = QWORD;
#else
    using Locale_t = DWORD;
#endif
}

#include "Tr/Game.h"
#include "Tr/IFileSystem.h"
#include "Tr/MSFileSystemFile.h"
#include "Tr/MultiFileSystem.h"
#include "Tr/ArchiveSet.h"
#include "Tr/RenderAsyncCreateResource.h"
#include "Tr/PCDX11StaticConstantBuffer.h"
#include "Tr/PCDX11RenderDevice.h"
#include "Tr/PCDX12UploadPool.h"
#include "Tr/PCDX12RenderDevice.h"
#include "Tr/PCDX12StaticConstantBuffer.h"
#include "Tr/CommonMaterial.h"

#include "ResourceKey.h"
#include "ResourceNaming.h"
#include "MaterialConstantStore.h"
#include "NotificationChannel.h"
#include "CommandChannel.h"
#include "Debug.h"
#include "TrHook.h"
