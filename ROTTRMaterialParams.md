# Rise of the Tomb Raider Material Parameters

This is a list of Rise of the Tomb Raider materials, with all the known material parameters that have been found by trial and erroring. Unfortunately, unlike most modern games, it is not possible to know what each material parameter (so each float) does, because of the way the game stores materials and shaders. This is why this list exists.

This list will keep getting updated from time to time.

## Please note that this documentation should only be relevant for an "advanced" level of modding. In most of the cases, you won't need to know all this info.

### Material parameters with a question mark (?) simply indicates that the functionality of the parameter hasn't been found (either yet, or ever).

----------------------------------------------------------------------------------------

- **Lara boot laces (267149.tr10material in laracroft.drm)**


        passRefs[3]
            psConstants[34]
                psConstants[23] = x.? y.? z.TilingMapRedDiffuseX w.TilingMapRedDiffuseY (detail diffuse map, RED colored part of detail mask ID 10374, diffuse being ID 233)
                psConstants[24] = x.Tiling2ndRoughnessMapX y.Tiling2ndRoughnessMapY z.? w.?

        passRefs[7]
            psConstants[25]
                psConstants[10] = x.? y.? z.TilingMapGreenX w.TilingMapGreenY (detail normal map, GREEN colored part of detail mask texture ID 10374)
                psConstants[13] = x.TilingMapRedX y.TilingMapRedY z.? w.? (detail normal map, RED colored part of detail mask texture ID 10374)
        
----------------------------------------------------------------------------------------
        
- **Lara Eyebrows (111348.tr10material in laracroft.drm)**
        
        passRefs[8]
            psConstants[14]
                psConstants[0] = x.ColorR y.ColorG z.ColorB ?.w 
                psConstants[1] = x.SpecularR y.SpecularG z.SpecularB ?.w 
                psConstants[3] = x.SpecularR y.SpecularG z.SpecularB ?.w
                psConstants[9] = x.? y.SpecularStrength z.? w.?
                psConstants[12] = x.Something_About_Specularity ?.y ?.z w.SpecularSpread (lower value, more spread specularity)
        
----------------------------------------------------------------------------------------

- **Lara Tearline (2591.tr10material in laracroft.drm)**
        
        passRefs[8]
            psConstants[6]
                psConstants[1] = x.? y.? z.? w.SpecularStrength
                psConstants[4] = x.? y.? z.? w.SpecularSpread (lower value, more spread specularity)
