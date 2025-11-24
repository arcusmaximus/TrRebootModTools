# Rise of the Tomb Raider Material Parameters

This is a documentation of Rise of the Tomb Raider materials, with all the known material parameters that have been found by trial and erroring. Unfortunately, unlike most modern games, it is not possible to know what each material parameter (so each float) does, because of the way the game stores materials and shaders. This is why this documentation exists.

This list will get updated from time to time.

## Please note that the naming and understanding of material parameter names is approximative. This means that I may incorrectly label some parameters.

## Please note that this documentation should only be relevant for an "advanced" level of modding. In most of the cases, you won't need to know all this info.

### Material parameters with a question mark (?) simply indicates that the functionality of the parameter hasn't been found (either yet, or ever).

----------------------------------------------------------------------------------------

- **Lara pants (267146.tr10material in laracroft.drm)**

        passRefs[3]
            psConstants[22]
                psConstants[1] = x.ColorR y.ColorG z.ColorB w.?
                psConstants[4] = x.FresnelColorR y.FresnelColorG z.FresnelColorB w.?
                psConstants[7] = x.? y.? z.DirtIntensity w.?
                psConstants[11] = x.? y.? z.ColorX w.ColorY
                psConstants[11] = x.? y.? z.TilingDetail1X w.TilingDetail1Y
                psConstants[17] = x.FresnelIntensity1 y.FresnelIntensity2 z.? w.FresnelIntensity3
                psConstants[13] = x.? y.? z.TilingDetail1 w.?
                psConstants[21] = x.FresnelIntensity4 y.FresnelIntensity5 z.? w.?

        passRefs[7]
            psConstants[16]
                psConstants[7] = x.? y.? z.TilingDetail2X w.TilingDetail2Y
                psConstants[13] = x.? y.? z.? w.Detail2Intensity
                psConstants[15] = x.? y.NormalIntensity z.? w.?
        
----------------------------------------------------------------------------------------

- **Lara boot laces (267149.tr10material in laracroft.drm)**

        passRefs[3]
            psConstants[34]
                psConstants[1] = x.GlobalColorR y.GlobalColorG z.GlobalColorB w.?
                psConstants[2] = x.GreenDetailMaskColorR y.GreenDetailMaskColorG z.GreenDetailMaskColorB w.?
                psConstants[3] = x.BlueDetailMaskColorR y.BlueDetailMaskColorG z.BlueDetailMaskColorB w.?
                psConstants[4] = x.? y.GlobalDirtIntensity1 z.GlobalDirtIntensity2 w.?
                psConstants[5] = x.BlueDetailDirtMask y.GlobalDirtMaskIntensity1 z.GlobalDirtMaskIntensity2 w.?
                psConstants[6] = x.GreenDetailIntensity y.GlobalDetailIntensity1 z.GlobalDetailIntensity2 w.?
                psConstants[7] = x.GlobalRefrectanceColorR y.GlobalRefrectanceColorG z.GlobalRefrectanceColorB w.RedDetailRefrectance
                psConstants[8] = x.? y.SnowMaskIntensity1 z.SnowMaskIntensity2 w.?
                psConstants[12] = x.RedDetailRefrectanceColorR y.RedDetailRefrectanceColorG z.RedDetailRefrectanceColorB w.RedDetailRefrectance
                psConstants[14] = x.? y.GlobalBloodMaskIntensity1 z.GlobalBloodMaskIntensity2 w.?
                psConstants[15] = x.? y.? z.? w.GlobalDetailMapIntensity
                psConstants[19] = x.? y.? z.DetailMaskX w.DetailMaskY
                psConstants[21] = x.RefrectanceRoughnessX y.RefrectanceRoughnessY z. w.
                psConstants[22] = x.ColorX y.ColorY z. w.
                psConstants[23] = x.? y.? z.TilingMapRedDiffuseX w.TilingMapRedDiffuseY (detail diffuse map, RED colored part of detail mask ID 10374, diffuse being ID 233)
                psConstants[25] = x.DetailRedRefrectanceRoughnessX y.DetailRedRefrectanceRoughnessY z.? w.?
                psConstants[31] = x.? y.RefrectanceIntensity z.? w.?

        passRefs[7]
            psConstants[25]
                psConstants[8] = x.TilingDetailRefrectanceX y.TilingDetailRefrectanceY z.? w.?
                psConstants[1] = x.? y.? z.? w.DetailRedRefrectanceRoughnessScale
                psConstants[2] = x.? y.? z.? w.DetailRedRoughnessIntensityInverted
                psConstants[10] = x.? y.? z.TilingMapGreenX w.TilingMapGreenY (detail normal map, GREEN colored part of detail mask texture ID 10374)
                psConstants[13] = x.TilingMapRedX y.TilingMapRedY z.? w.? (detail normal map, RED colored part of detail mask texture ID 10374)
                psConstants[19] = x.Detailintensity y.? z.? w.?
                psConstants[22] = x.? y.? z.NormalIntensity w.?
        
----------------------------------------------------------------------------------------
        
- **Lara Eyebrows (111348.tr10material in laracroft.drm)**
        
        passRefs[8]
            psConstants[14]
                psConstants[0] = x.ColorR y.ColorG z.ColorB w.?
                psConstants[1] = x.SpecularR y.SpecularG z.SpecularB w.?
                psConstants[3] = x.SpecularR y.SpecularG z.SpecularB w.?
                psConstants[9] = x.? y.SpecularStrength z.? w.?
                psConstants[12] = x.Something_About_Specularity y.? z.? w.SpecularSpread (lower value, more spread specularity)
        
----------------------------------------------------------------------------------------

- **Lara Tearline (2591.tr10material in laracroft.drm)**
        
        passRefs[8]
            psConstants[6]
                psConstants[1] = x.? y.? z.? w.SpecularStrength
                psConstants[4] = x.? y.? z.? w.SpecularSpread (lower value, more spread specularity)
