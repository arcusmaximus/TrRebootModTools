# Tomb Raider Reboot Modding Tools

## Introduction

This toolset allows modding the games from the Tomb Raider Reboot trilogy. The following are supported:

|                                            | Tomb Raider (2013)* | Rise of the Tomb Raider | Shadow of the Tomb Raider |
| ------------------------------------------ | ------------------ | ----------------------- | ------------------------- |
| Meshes                                     | ✓                  | ✓                      | ✓                         |
| Textures                                   | ✓                  | ✓                      | ✓                         |
| Text (outfit descriptions, subtitles etc.) | ✓                  | ✓                      | ✓                         |
| Cloth physics                              | ✓                  | ✓                       | ✓                         |
| Hair                                       | ✓                  | ✓                       | ✓                        |
| Animations                                 |                    |                         | ✓                         |
| Sound                                      |                    |                         | ✓                         |

\* For Tomb Raider (2013), the "Definitive Edition" that can only be bought on the Xbox store is **not** supported.
Only the "regular" version is.

This page will give a quick introduction to the various file types involved, and then explain how to create and package mods.

If you're reading on GitHub, you can access the table of contents using the button on the rightmost side of the toolbar.

## File extensions

The list below describes the most common file types you'll encounter. An "X" in an extension is a placeholder for a game
version number. For example, when you see ".trXdtp", this refers to one of the following:

- .tr9dtp: Tomb Raider (2013).
- .tr10dtp: Rise of the Tomb Raider.
- .tr11dtp: Shadow of the Tomb Raider.

The file extensions are as follows:

- **.tiger**

  An archive that contains *resources* and *files*. Resources only have an ID; examples include meshes and textures.
  Files have a folder path and filename; examples include sound files, string localization files, and .drm resource collections.

  (Previous modding tools have referred to resources as "sections," which is also what the games call them internally.
  However, this page will refer to them as "resources" for simplicity's sake.)

  If two archives contain the same file, the archive with the highest version number in its .nfo takes precedence. This makes it
  possible to mod the game by creating new archives rather than modifying existing ones. The exception is TR2013, which
  doesn't have .nfo files: in this case, the mod manager automatically creates a backup of patch2.000.tiger,
  then replaces it by a modded one.

- **.drm**

  One of the most common file types found in archives. ("DRM" here stands for "Data RAM.")
  These are essentially resource collections: for a certain ingame object such as an outfit or weapon,
  they list which resources (meshes, textures...) should be loaded into memory, and in which archives those
  resources can be found.

  It's good to know that .drm files only refer to resources; they don't contain them. Quite often, the same resource
  is referenced by multiple .drm files, which means that modding one texture may affect multiple outfits, for example.

- **.trXobjectref**

  The root resource of a .drm resource collection. Contains nothing more than a reference to a .trXdtp resource (the object
  metadata) which in turn references a skeleton and models.

- **.trXmodel**

  A small metadata resource that references a model data resource and one or more materials.

- **.trXmodeldata**

  Contains mesh geometry, including things like UV maps and blend shapes.

- **.trXmaterial**

  References texture and shader resources, and providers parameters for the latter. Can technically be edited using the binary
  templates, but usually it's easier to simply find an existing material that suits your needs.

- **.dds**

  Regular DirectX texture. The game actually has its own texture format, but it's so close to DDS that the extractor and
  mod manager perform the conversion automatically (without any quality loss).

- **.trXshaderlib**

  Contains one or more compiled shaders (DXBC).

- **.trXcontentref**

  The root resource of certain special .drm files containing lists of game content,
  such as collectibles or outfit traits. Just like .trXobjectref, this resource type contains nothing more than a reference
  to a .trXdtp resource with the actual data.

- **.trXdtp**

  Catch-all resource type used for (almost) everything not listed above.

There are a few other extensions such as .trXscript, which are however unexplored and not moddable.

## Installation

The extractor and manager need no installation and can be run from anywhere. They'll try to autodetect where
each game is installed and ask you to provide the location if they can't find it.

The Blender addon can be installed as follows. Note that it requires Blender **4.0** or above.

- Click Edit → Preferences in the menu.
- Select the "Add-ons" tab.
- Click the small arrow in the top right corner and choose "Install from Disk..."
- Select io_scene_tr_reboot.zip.
- Enable the checkmark next to "Import-Export: TR Reboot mesh support."

Finally, if you want to use the binary templates (not needed for mesh/animation editing), simply place them
in the correct folder depending on which hex editor you have:

- ImHex: "patterns" subfolder.
- 010 Editor: "010 Templates" subfolder.

## Mesh modding

### Importing

Start by extracting the .drm of the model you want to edit. Then launch Blender, choose File → Import →
Tomb Raider Reboot object, and select the .trXobjectref file. This will import the model's meshes, materials,
and skeleton if there is one.

The import filechooser has the following options on the right hand side:

- **Import LODs**

  By default, the addon doesn't import any of the model's LOD meshes to reduce clutter. In certain cases, though,
  you may want to see them: if you're looking to replace Lara's head (explained further below), or if an original outfit
  simply doesn't have a full-detail mesh for one of its parts.

  Note, however, that the addon doesn't support *exporting* LODs. All meshes will be exported as full-detail
  meshes, regardless of whether they were LOD meshes when you imported them. This means you should delete any LOD meshes
  before exporting.

- **Split meshes into parts**

  By default, the addon creates a Blender mesh per TR mesh in the model, where each TR mesh consists of one or more parts
  (set of faces sharing the same material). If this isn't granular enough for you, you can enable this option to create
  a Blender mesh per TR mesh part. Alternatively, you can of course use Blender's "Separate By Material" operator
  after importing.

- **(SOTTR) Merge with existing skeleton(s)**

  In SOTTR, each of Lara's outfits is split into as many as four pieces (head/hair/torso/legs), with each piece having its
  own partial skeleton. What's more, different skeletons typically have different IDs for the same bone.
  This of course complicates rigging.

  To solve this problem, the addon can merge the different skeletons into one skeleton that covers the whole outfit.
  Simply import one outfit piece, then import another (and another...) into the same Blender scene with this option
  enabled. The merging feature is explained in more detail later on.

- **(SOTTR) Keep original skeletons**

  Lets you choose between two different ways of merging skeletons:

  * When this option is enabled, the original (partial) skeletons will be kept in a hidden Blender collection.
    When you export the model, it's automatically split into pieces and distributed over the partial skeletons.
    Use this option if you don't intend to export the merged skeleton.

  * When this option is disabled, the original skeletons are not kept. You can — and need to — export the
    merged skeleton as a new SOTTR skeleton together with your models.

  This option only applies if *Merge with existing skeleton(s)* is enabled.

The Blender meshes are named using the following convention:
`<DRM name>_model_<object ID>_<model ID>_<model data ID>_<mesh index>`. Skeletons, bones, materials
etc. follow similar conventions. These names should be left unchanged as they're needed for the export to work.


### Fixing materials

The addon creates a Blender material for every TR material in the "Material" folder (even ones not referenced by the model).
Each Blender material's shader tree contains all the textures used by the TR material, which makes it easier to find
textures to change.

The addon also assigns these materials to the mesh parts, so that you can see a textured preview of the model right after importing.
You'll likely notice, however, that some textures are off. This is because the addon doesn't know which of each material's
textures is the diffuse one — it takes a guess, but it can be wrong. You can fix this by going to Blender's Shading tab and
linking the correct texture yourself.

Note that the Blender shader tree only affects how the material looks in Blender. If you want to change how it looks ingame,
you need to use the binary template instead (or use a different material).

### Normals

The addon imports mesh normals, with an important exception: if a mesh has blend shapes, it lets Blender calculate the normals instead.
The reason is that Blender's shape keys don't support custom vertex normals, only positions.

After importing, it's recommended to use Blender's "Face Orientation" overlay to verify that no faces ended up pointing inside out.
This tends to be the case for the teeth in head meshes for example. Incorrectly oriented faces will appear blackened ingame.

If you want to remove the custom normals for other meshes (e.g. because they get in the way during editing), you can do so by
selecting the mesh, switching to the Data tab (green triangle) in Blender's Properties panel, expanding the "Geometry Data" header,
and clicking "Clear Custom Split Normals Data."

Because TR blend shapes use custom vertex normals but Blender's shape keys don't support these, exported heads tend to
have artifacts even if you didn't change anything. (The most noticeable effect is dark patches on the eyelids.)
To work around this, the addon lets you transfer the shape key normals from the original model to your modified one.
Find the "TR Mesh Properties" panel in Blender's Data Properties tab, select the original file
as the "Shape Key Normals Source", and export your model as usual.

### Skeletons

In SOTTR, each outfit is split into up to four pieces — head, hair, torso, and legs — where each piece has its own skeleton
that only contains the necessary bones.

#### Bone hiding

TR skeletons contain quite a few bones that aren't used for deforming the model. To reduce clutter, the addon hides
these bones by either moving them to the second bone layer (Blender 3) or into a hidden bone collection (Blender 4).
So, if you import a file that seems to have way too few bones (like tr11_lara.drm), check this layer/bone collection.

#### Twist bones

Many SOTTR bones are not animated directly, but are so-called twist bones that have their position and rotation calculated based
on other bones. They are displayed in green in Pose Mode, and can be hidden in Blender 4.0 by toggling the "Twist bones"
bone collection. While the twisting information is not applied as Blender bone constraints or drivers,
it's still stored in the Blender file, and will be written out again when exporting the skeleton (meaning that,
if you mod a skeleton, the twisting information won't be lost).

#### Bone IDs

Bones in TR don't have human-readable names like in some other engines. Instead, each bone has one or two IDs that are
reflected in its name in Blender, e.g. `bone_x_141` or `bone_7893_140`.

- The first ID, if present, is the *global ID* and is used by animations (e.g. "Rotate bone 7893 by 5°"). By necessity,
  any given bone (e.g. left hip) has the same global ID in every skeleton, no matter which outfit or outfit piece it belongs to.
  If a bone does not have a global ID (name contains `x` instead of a number), it can't be driven by animations;
  instead, it's driven by cloth physics (see further below).

- The second ID is the *local ID* and is used by the model's vertex weights (e.g. "This vertex is influenced for 80% by bone 140").
  The same bone typically has a different local ID in every single skeleton, even skeletons of the same outfit.

Because one bone can have several different local IDs, rigging gets complicated. The torso skeleton might says that
the hip bone is called `bone_7893_140`, but the legs skeleton might say that it's called `bone_7893_21` instead.
Worse, the torso skeleton of another outfit might give it yet another name.

Summarized, these local IDs cause two problems:
- Rigging is difficult if you're faced with two or three partial skeletons with overlapping bones and conflicting names.
- You can't reuse a mesh that's rigged for one outfit in another outfit.

The addon has features for helping with both of these.

#### Merging with Keep Originals

If you enable the options "Merge with existing skeleton(s)" and "Keep original skeletons" in the Import filechooser,
you can solve both of the above problems at once.

The way it works is as follows:

- When you import the first model, the bones will have both their global and local IDs (e.g. `bone_7893_140`).

- When you import a second model, the addon will create a new armature containing the bones of both the
  first and second skeleton (duplicates removed), and all the bones renamed to use *just* the global ID (`bone_7893`).
  It also parents the meshes of both the first and second model to this new armature and renames
  their vertex groups to match the new bone names.

  The original, partial skeletons are moved to a hidden collection named "Split meshes for export"
  and should not be deleted.

  (Physics bones, which have no global ID, are instead renamed to something like `bone_322371_x_141` where
  the first number is the ID of the partial skeleton they originate from.)

- If needed, import the third and fourth pieces of the outfit as well. (Note that exporting heads with
  blend shapes is slow, so it's generally recommended to import those without merging so you can
  export them separately later on.)

- You can now edit the meshes — or delete them and start from scratch — and weight them for the single, merged skeleton.
  Also, because the bone names now only contain global IDs, you can reuse these meshes in another outfit
  without needing to rename the vertex groups.

- When you do an export, the addon will automatically create copies of the meshes (inside the hidden collection),
  rename their vertex groups to match the bones of the original partial skeletons, and export those copies.
  These meshes will then work with the original skeletons; you don't need to check "Export skeleton."

  If a mesh spans multiple partial skeletons, the addon will automatically split it up. This tends to result in
  visible seams ingame, however, so it's better to split the meshes yourself in a place where the seams are
  minimized or hidden.

Note that, because the merged skeleton uses incomplete bone names (with the local IDs removed), it can't be exported
as a new SOTTR skeleton. If you export the models with "Export skeleton" enabled, the addon will first synchronize
the physics bones from the merged skeleton to the partial skeletons, and then export the partial skeletons instead.

#### Merging without Keep Originals

If you want to export the merged skeleton for use in SOTTR, you can uncheck "Keep original skeletons."
If you do this, you'll again get a single merged skeleton without duplicate bones, meaning rigging again becomes
easier. The original partial skeletons will *not* be kept around and (importantly) all bones will have
complete names with local IDs, meaning the skeleton is exportable.

On the flipside, the presence of local IDs again means you can't immediately reuse meshes in another
outfit, because the bone names will be different there.

Because merging skeletons this way changes their local bone IDs, you have to check both "Export skeleton" and
"Export cloth" when exporting models so that the IDs stay synchronized between all three. Also, in order
to be able to export the cloth physics, you need to check "Import cloth and collisions" when importing
the models at the start.

#### Vertex group name fixing

If you rigged a mesh for one skeleton and bring it to another, the rigging typically won't work anymore,
reason being that the local IDs in the vertex group names no longer match those of the bones. (The one
exception is when both skeletons were merged with "Keep originals.")

However, if you select the mesh and go to Blender's Data Properties tab (green triangle), you'll
find a panel labeled "TR Mesh Properties" with a button named "Fix Vertex Group Names."
If you click this, the addon will match the vertex groups with the parent armature's bones based on
global ID, and rename the vertex groups so they have the correct local ID. This way, you can
reuse the mesh after all.

### Mesh editing

Previous modding tools have resorted to patching/amending the original file, which limited the changes you could make.
This toolset, however, exports model files from scratch, which gives you a lot more freedom. Specifically, you can:

- Add, change, and remove meshes as you please. (There's no need to keep the mesh index at the end of the
  object name contiguous.)
- Add, change, and remove blend shapes as you please.
- Add, change, and remove material slots as you please, and freely assign different materials to faces.
  You can also reference materials that weren't originally referenced by the model — *including* materials from
  another .drm file. It's enough to rename the Blender material to use the ID of the external TR material,
  but you can of course also import the external .drm file into the current scene and delete its armature/meshes again,
  keeping just the materials.


### Head modding

In ROTTR and SOTTR, the head mesh has over a hundred blend shapes for facial expressions. Replicating all these on a
custom mesh is quite the undertaking. In SOTTR, this is fortunately not necessary: the head also supports bone-based facial
animation, meaning all you really have to do is transfer the vertex weights to your custom head. The full-detail standard head
isn't weighted for facial bones, but the LOD meshes are, which is where the "Import LODs" option comes in handy.

TR2013 doesn't support blendshapes at all, so there, too, you can simply transfer the weights. In ROTTR, however,
this is sadly not possible.


### Draw groups

Certain parts of an outfit may only be visible under certain conditions. One example is the Expedition Jacket in ROTTR:
it contains a version of the hood that's on Lara's head, and another that's hanging down the back. Each hood has a
unique *Draw Group ID* that allows the game to find it and then show/hide it as needed.

This Draw Group ID can be seen, and changed, in the "TR Mesh Properties" panel in the "Data" tab (green triangle icon)
of Blender's Properties editor.


### Exporting

Exporting is done through the menu item File → Export → Tomb Raider Reboot model. The addon will export
the models you have selected, or all models in the scene if none are selected. It'll produce several files
per model depending on the target game.

As mentioned before, the addon creates model files from scratch, so there's no need to overwrite an existing file.

The addon will automatically add a Triangulate modifier to meshes that don't have one yet, and temporarily disable
all other modifiers so they don't affect the export result.

## Animation modding

Apart from models, the Blender addon also supports importing and exporting SOTTR animations (.tr11anim files).
You can animate bone positions/rotations/scales and blendshape values.

To find an animation to modify, you can use a binary template to browse the .tr11animlib files in e.g. tr11_lara.drm —
these files map animation names to IDs, where the IDs correspond to the names of the .tr11anim files.
You can also check the appendix at the end of this page to find the IDs of the photo mode poses.

Once you've found an animation you'd like to edit or replace, you'll want to do the following:

- Import tr11_lara.tr11objectref from tr11_lara.drm and delete the dummy mesh, keeping just the skeleton.
- Import the head, torso, and leg models of some outfit.
- Hide the bone collections "Cloth bones" and "Twist bones" to reduce clutter.
- Import the .tr11anim file (File → Import → SOTTR animation).
- Edit the animation. While imported animations have a keyframe on every frame, this is not required for
  custom animations.
- Export the animation to a .tr11anim file (File → Export → SOTTR animation). As with models, there's no
  need to overwrite an existing file.

The addon supports bone constraints. In fact, it automatically creates constraints on a few (hidden by default)
bones of the tr11_lara skeleton, as this is necessary for the animation to play correctly ingame.

The animation's frame count is determined by the Blender scene's end frame.
Its speed is in turn determined by the Blender scene's *FPS Base* (see Output tab → Format panel;
you'll need to set the *Frame Rate* to *Custom* to see the Base). Note that the Base works in
the opposite way as the FPS: a higher Base will result in each frame taking more time and the
animation thus being slower overall, not faster.

## Cloth physics modding

Not all bones in an outfit are driven by an animation or other bones: some are instead driven by a
cloth physics simulation. You can add these physics to your own models.

The cloth system involves the following concepts:
* Strip: a patch of fabric (or hair, or rope...) consisting of masses and springs.
* Mass: an invisible point that can freely move around, influenced by gravity, wind, and springs.
  Each mass pulls one or more model vertices along with it through its associated bone.
* Spring: an invisible spring that connects two masses and can stretch, compress, and wiggle around.
  This is what keeps the masses from just flying away.
* Collision: a simple, invisible shape (box, sphere, capsule...) which cloth masses are not allowed to enter.

If an imported outfit has physics, you'll notice that in addition to the model and skeleton,
you now have a bunch of wireframe meshes. Each such mesh represents a cloth strip:
the vertices are the masses, and the edges are the springs.

The naming convention for each cloth strip is as follows:
`<object name>_clothstrip_<skeleton ID>_<cloth definition ID>_<cloth component ID>_<cloth strip ID>`.
The first three IDs correspond to .trXdtp files (which you can view and edit with the binary templates).
The last ID is an arbitrary number that identifies the cloth strip.

If you want to add a new cloth strip, you can duplicate an existing one and change its name so that
(only) the last ID is a unique number. You can also delete any cloth strip mesh, or edit it in
whatever way you want.

> [!TIP]
> If there's only an empty cloth strip with ID 1111, that means the outfit doesn't have any physics.
> You can still add physics in this case, but may have to include the object .tr11dtp file in your mod
> to get things working. You can identify the object file by opening the .tr11objectref file
> with the corresponding binary template.

The addon adds a custom "Tomb Raider Cloth" tab to Blender's sidebar (which you can open by pressing `N`).
This tab lets you perform various cloth-related operations:

* **Bones**
  * **Regenerate**

    After you've added/edited/removed a cloth strip mesh, you can click this button to update
    the skeleton to match. The addon will create, move, and delete physics bones so there's one bone
    at each cloth strip vertex (mass). Once you're happy with the bones, you can weight the model to them.

  * **Pin** (Pose mode)

    Pins the selected bones, marking them in red. Pinned bones (masses) are stuck in place and don't
    flutter around. You should pin the bones at the edge of the strip where it attaches to the rest
    of the outfit.

  * **Unpin** (Pose mode)

    Unpins the selected bones, marking them in grey. Unpinned bones (masses) are free to flutter around.

  * **Bounceback strength** (Pose mode)

    Shows, and lets you change, the bounceback strength of the selected bones (masses).
    This determines how much the masses want to return to their original location.

* **Strip**
  * **Parent**

    The outfit bone which the selected strip is attached to. This should be a bone that's
    close to the strip while not being part of it. After changing this, click *Regenerate*
    to parent the physics bones to this bone.

  * **Gravity Factor**

    The gravity multiplier of the selected strip. The default is 1, but you can make it higher or
    lower, including setting it to 0 or even making it negative.

  * **Wind Factor**

    Determines how strongly the strip is affected by wind.

  * **Pose Follow Factor**

    How much the cloth masses follow Lara's orientation and pose. With a low factor,
    they're free to move independently of the rest of the outfit, while with a high factor,
    they stick close to their original relative position.

  * **Rigidity**

    Overall spring rigidity. Appears to have little effect ingame.

  * **Drag**

    How much the mass movement is damped (slowed down over time).

* **Springs** (Cloth strip edit mode)

  * **Stretchiness**

    Shows, and lets you change, the stretchiness of the selected springs (edges).
    With a value of 0, springs can't stretch beyond their original length.
    With a value of 1, they're allowed to stretch up to twice their original length.

The addon also imports collisions; they're hidden by default, but you can make them visible
if you're interested in seeing them. The collisions themselves can't be edited,
but if you delete one, the exported cloth strips will no longer be linked to it
and no longer collide with it ingame. (This also means you should normally keep the collisions
around in your Blender file, as otherwise, the cloth strips won't collide with anything.)

Once you're done making your changes, export the model as usual (File → Export → Tomb Raider Reboot model),
making sure to check "Export skeleton" and "Export cloth" in the file chooser.

### TR2013

TR2013 is a bit more annoying to get custom cloth physics into. Rather than putting the clothstrips
and bones in the outfit itself like with the other games, they need to be put in laracroft.drm:

- Import both laracroft.drm and the outfit you want to mod into the same Blender scene. (The miniature skeleton
  in laracroft.drm is not needed and can be deleted again.)
- Set up your custom cloth strip meshes under the laracroft skeleton and regenerate the physics bones as usual.
- Update the outfit skeleton to match the laracroft skeleton. You can do this by deleting all the bones
  in the outfit skeleton (make sure all the bone collections are visible first), duplicating the laracroft
  skeleton, and then joining this duplicate into the outfit skeleton: select the duplicate laracroft skeleton,
  then the outfit skeleton, and finally press Ctrl-J while the mouse hovers of the 3D Viewport.
- Update the outfit mesh, adding vertex weights for the physics bones in the outfit skeleton.
- When you're done, export both the laracroft model (for the skeleton and cloth strips) and the outfit model
  (for the mesh).


## Hair modding

The addon supports importing and exporting of [TressFX/PureHair](https://en.wikipedia.org/wiki/TressFX) models.
Simply import one of the following to get started:

| TR2013               | ROTTR             | SOTTR              |
| -------------------- | ----------------- | ------------------ |
| lara_hairsplines.drm | lara_hair_all.drm | tr11_lara_hair.drm |

You can set the viewport shading mode to Material Preview to see where hair strands start (black) and where they
end (white).

### TR2013

For TR2013, the hair is very straightforward: each hair part (ponytail, bangs etc.) gets imported as a single
Blender "hair curves" object. Each part has vertex weights for the same single bone, indicating how strongly each
vertex is "stuck" to the scalp. You can edit these weights by clicking *Start Weight Painting* in the *Tomb Raider*
tab in Blender's Sidebar.

You can add or remove parts as you see fit. The export will include the results of any modifiers. Also, while
the game expects each hair strand to have exactly 16 vertices, you don't need to worry about this as the addon
(temporarily) resamples all the strands during export.

The hair color is determined by the texture 7827.dds in generalbank.drm.

### ROTTR/SOTTR

The latter two games use a more involved system with not one, but two sets of strands for each hair part.
The first is a small number of "guide" strands, which are physics-simulated but invisible. The second is
a large number of "render" strands, which are visible but simply mirror the shape of nearby guide strands
(rather than being simulated themselves).

The hair parts have their color and physics settings defined in a separate file, so you should stick with
the existing names and not create additional parts. You can, however, delete any parts you don't need.

The guide strands have vertex weights linking them to the skeleton; these can again be edited by clicking
*Start Weight Painting* in the *Tomb Raider* Sidebar tab. Unlike TR2013, where each vertex has only one
weight for one fixed bone, ROTTR and SOTTR support up to four weights for arbitrary bones. This means you can
theoretically attach hair to other places than Lara's head.

The render strands have vertex weights linking them to guide strands, but these are not imported
into Blender and do not need to be manually configured before exporting. The addon calculates them
automatically.

You can change the color of each hair part as follows:

- Extract an outfit (for ROTTR) or a head model (for SOTTR).
- Open the .trXobjectref file in a hex editor and apply the "trXobjectref" template. You'll see it
  references a .trXdtp file.
- Open this .trXdtp file and apply the "trXobject" template.
- Find the `simpleComponents` entry, and within it, the `DYNAMICHAIR` entry.
- Expand the entry to find the .trXdtp file it references.
- Open the above .trXdtp file and apply the "trXdynamichaircomponent" template.
- Find the `groupConstants` array and, within it, the `hairStrandColors` field. This field defines two "root"
  colors and two "tip" colors. The game renders each hair strand by first picking a random root color and a random
  tip color interpolated between these defined colors, then paints the strand as a gradient from root to tip.
- Save the modified .trXdtp file and include it in your mod.

You can also remove the PureHair entirely by changing the `type` field of the `DYNAMICHAIR` entry to `_NONE`.
This then lets you replace it by regular polygon-based hair on the head mesh.

## External resource references

Most TR resources reference other resources. A .trXobjectref references an object. An object references
a skeleton and models. A model references materials, which in turn reference textures and shaders, and so on.

You can change a model's material references by simply applying different materials in Blender. However,
by using the binary templates, you can edit other references as well. For example, you could make an
object point to a different skeleton.

When changing such an external reference, you can make it point to any resource in the game,
*even if that resource is not included in the DRM*. The mod manager will automatically
add the referenced resource (and any resources it references in turn) to the DRM so that the game
won't crash.

This also means you can simply overwrite a resource by a copy of any other resource. For example,
you could overwrite a material by another and change just one texture reference, keeping the
shader references and the other textures.

## Text modding

You can mod the file pcx64-w\local\locals.bin ("pc-w" for TR2013), which contains all the text
displayed in the game: menu items, subtitles, outfit names/descriptions, and so on.

As indicated by its extension, it's a binary file, but the extractor automatically converts
it to JSON for convenience. Once extracted, you can change any text you like. To keep an overview,
you can also remove any entries you don't intend to modify.

Once you're done making changes, you can pack your (complete or partial) JSON file into your mod,
making sure to use the same folder structure and file name as produced by the extractor. The mod
manager will then create a new locals.bin that includes your changes.


## Sound modding

SOTTR uses the Wwise sound engine, which has a proprietary file format called .wem
(for Wwise Encoded Media). To create such files, you need the Wwise authoring tools,
which can be installed for free through the [Audiokinetic Launcher](https://www.audiokinetic.com/download/).
While it's possible to do the conversion using these tools alone, it's a bit cumbersome,
so you can use the modding toolset's WwiseSoundConverter.exe instead.

Once you have your .wem file, place it in your mod with the same folder structure as the original
(mod\\pcx64-w\\wwise\\...), and you're done.


## Mod packaging

Creating mods for installation with the manager is quite simple: just put the modified resources and files
into a folder or compressed archive (.zip/.7z/.rar) and you're done. There's no need to include any .drm files
(in fact, these aren't extracted in the first place).

> [!TIP]
> When testing your mod during development, it's easiest to drag-and-drop the folder straight onto the
> mod manager window to install it. Packing the mod into an archive is only necessary when you're ready
> to publish it.
>
> What's more: if you install from a folder, the manager will first automatically uninstall
> the existing mod with the same name (if present) so that you don't have to do this manually,
> saving you some time when iterating.

For textures, simply use DDS files; the manager will automatically convert them back to the game-specific texture
format during installation (again without loss of quality). However, you should make sure to generate mipmaps,
and ideally use the same compression as the original textures.

You can organize your resources in subfolders in any way you like. You can keep the original folder structure,
but you can also reorganize them or even just throw them all into the same single place.
This is not the case for files such as locals.bin, however — these have to keep the original folder structure.

Note that a resource may be referenced by multiple .drm resource collections. Modding a texture that you extracted
for just one outfit may well end up affecting other outfits too.


### Variations

The mod manager supports mods with variations, such as differently colored versions of the same outfit.
This is again quite simple to set up:

- Create a subfolder for each variation.
- Within each subfolder, create one or both of the following files:
  - variation.txt: a plaintext file containing a description of the variation.
  - variation.png: a preview image.
- Place the variation-specific resources and files in their respective subfolder (either as direct
  children or inside deeper subfolders).
- Place the resources and files that are common to all variations in the root of the mod or in
  a subfolder that's not (part of) a variation.

When installing a mod with variations, the manager will present users with a window where they
can choose the one they like.

> [!TIP]
> This window is also shown if the mod contains only one variation, which can be useful
> if you have no variations but still want to present users with e.g. a readme before installing.

## Appendix 1: Outfit reference

### Tomb Raider (2013)

(Courtesy of Raq on [tombraidermodding.com](https://tombraidermodding.com))

| Description | .drm file |
| ----------- | --------- |
| "Innocent" Cinematic. Used in the very first in-game cutscene, where Lara is swimming in the sea getting to the shore. | cine_v1_lara |
| "Innocent" Player. Only used in the Model Viewer. | v1_lara |
| "Inexperienced" Cinematic. Used during the entire beginning section of the game, both gameplay and cutscenes. | cine_v2_lara |
| "Inexperienced" Player. Used for gameplay right after the cutscene where Lara finds the radio and Sam's backpack. | v2_lara |
| "Advanced" Cinematic. Used for all cutscenes playing from the one where Lara successfully escapes the plane wreck falling down. | cine_v4_lara |
| "Advanced" Player. Used right after the above section. | v4_lara |
| "Survivor" Cinematic. Used for all cutscenes playing from the one where Roth and Lara survive the helicopter crash. | cine_v5_lara |
| "Survivor" Player. Used for gameplay right after the above section. | v5_lara |
| Aviatrix outfit | (cine_)v3_lara_aviatrix |
| Camouflage outfit | (cine_)v3_lara_camo |
| Engineer outfit | dlc_lara_engineer |
| Guerrilla outfit | (cine_)v3_lara_black |
| Mountaineer outfit | dlc_lara_mountaineer |
| Sure-Shot outfit | dlc_lara_modernarcher |

### Rise of the Tomb Raider

| Ingame name | .drm file prefix | Name in globalcrafting.drm |
| ----------- | ---------------- | -------------------------- |
| Ancient Vanguard | laracroft_armor_mongolsbane | Armor_MongolsBane |
| Apex Predator | laracroft_huntress_bloodred | Huntress_BloodRed |
| Battle Worn | laracroft_jacket_light_battleworn | LightJacket_BattleWorn |
| Blue Henley | laracroft_long_sleeve_blue | Henley_Blue |
| Classic Angel Of Darkness | laracroft_classic02 | Classic02 |
| Classic Chronicles Catsuit | laracroft_classic05 | Classic05 |
| Classic Croft Manor | laracroft_classic04 | Classic04 |
| Classic Tomb Raider II | laracroft_classic01 | Classic01 |
| Classic Tomb Raider II Bomber Jacket | laracroft_classic03 | Classic03 |
| Commando | laracroft_infiltrator_stalker | Infiltrator_Stalker |
| Dark Tank Top | laracroft_tanktop_aqua | TankTop_Dark |
| Desert Tank Top | laracroft | TankTop_Base |
| Expedition Jacket | laracroft_snow_gear | WinterJacket_Base |
| Gray Henley | laracroft_long_sleeve | Henley_Base |
| Hope's Bastion | laracroft_armor_vanguard | Armor_UnholyVanguard |
| Huntress | laracroft_huntress | Huntress_Base |
| Immortal Guardian | laracroft_armor | Armor_Base |
| Infiltrator | laracroft_infiltrator_deceiver | Infiltrator_Deceiver |
| Leather Jacket | laracroft_hoodie | Hoodie_Base |
| Nightshade | laracroft_huntress_thebeast | Huntress_TheBeast |
| Pioneer | laracroft_endurance | Endurance |
| Reimagined Antarctica Outfit | laracroft_classic06 | Classic06 |
| Remnant Jacket | laracroft_jacket_light | LightJacket_Base |
| Rust Henley | laracroft_long_sleeve_rust | Henley_Rust |
| Sacra Umbra | laracroft_armor_demonseed | Armor_DemonSeed |
| Shadow Runner | laracroft_infiltrator | Infiltrator_Base |
| Siberian Ranger | laracroft_infiltrator_drifter | Infiltrator_Drifter |
| Sparrowhawk | laracroft_huntress_snowwhite | Huntress_SnowWhite |
| Spirit Weaver | laracroft_spiritweaver | SpiritWeaver_Base |
| Ushanka Camo | laracroft_colddarkness | ColdDarkness |
| Valiant Explorer | laracroft_pringles | Pringles |
| Whiteout Jacket | laracroft_snow_gear_whiteout | WinterJacket_WhiteOut |
| Wraithskin | laracroft_witch | Witch_Base |

The .drm files containing the outfit icons (for the outfit selection menu at campsites)
can be found in the following folder:

pcx64-w\design\image resources\scaleform\sharedtextures\basecamp\trx_basecamp\paperdolls\outfits


### Shadow of the Tomb Raider

| Ingame name | .drm file prefix | Name in globalcollectibleinfo.drm |
| ----------- | ---------------- | --------------------------------- |
| Adventurer | paperdoll_piece_tr11_lara_jungle | Lara_Prologue |
| Angel Of Darkness | paperdoll_piece_tr11_lara_aod | Lara_Loyalty_AOD |
| Blue Henley | paperdoll_piece_tr11_lara_tr10_blue_henley | Lara_Loyalty_TR10_BlueHenley |
| Blue Heron Tunic | paperdoll_piece_tr11_lara_outsider | Lara_Outsider |
| Bomber Jacket | paperdoll_piece_tr11_lara_tr2_bomber | Lara_Loyalty_TR2BomberJacket |
| Boots Of The Empress Jaguar | paperdoll_piece_tr11_lara_stealth_01_legs | Stealth_01_Legs |
| Brocken | paperdoll_piece_tr11_lara_dlc_hightech | HighTech_01 |
| Brocken - Shadow | paperdoll_piece_tr11_lara_dlc_hightech_alt | DLC_Outfit_01 |
| Ch'Amaka's Greaves | paperdoll_piece_tr11_lara_assault_03_legs | Assault_03_Legs |
| Ch'Amaka's War Vest  | paperdoll_piece_tr11_lara_assault_03_torso | Assault_03_Torso |
| Commando | paperdoll_piece_tr11_lara_tr10_commando | Lara_Loyalty_TR10_Commando |
| Condor Cowl Of Urqu | paperdoll_piece_tr11_lara_stealth_02_torso | Stealth_02_Torso |
| Crimson Huntress | paperdoll_piece_tr11_lara_priest | Lara_CrimsonHuntress |
| Croft Fitness | paperdoll_piece_tr11_lara_workout_nostraps | DLC_Outfit_13 |
| Desert Tank | paperdoll_piece_tr11_lara_tr10_desert_tanktop | Lara_Loyalty_TR10_DesertTank |
| Dragon Scales | paperdoll_piece_tr11_lara_dlc_kukulkan_alt | Kukulkan_01 |
| Dragon Scales - Legend | paperdoll_piece_tr11_lara_dlc_kukulkan | DLC_Outfit_05 |
| Eveningstar's Boots | paperdoll_piece_tr11_lara_exploration_01_legs | Exploration_01_Legs |
| Eveningstar's Cape | paperdoll_piece_tr11_lara_exploration_01_torso | Exploration_01_Torso |
| Explorer | paperdoll_piece_tr11_lara_explorer | Lara_Explorer |
| Gray Henley | paperdoll_piece_tr11_lara_tr10_gray_henley | Lara_Loyalty_TR10_GrayHenley |
| Greaves Of Six Sky | paperdoll_piece_tr11_lara_assault_02_legs | Assault_02_Legs |
| Hide Boots Of Urqu | paperdoll_piece_tr11_lara_stealth_02_legs | Stealth_02_Legs |
| Hunter's Array | paperdoll_piece_tr11_lara_trinity | Trinity_01 |
| Hunter's Array - Apex | paperdoll_piece_tr11_lara_trinity_alt | DLC_Outfit_04 |
| Infiltrator | paperdoll_piece_tr11_lara_tr10_infiltrator | Lara_Loyalty_TR10_Infiltrator |
| Kantu's Boots | paperdoll_piece_tr11_lara_exploration_03_legs | Exploration_03_Legs |
| Kantu's Gilded Vest | paperdoll_piece_tr11_lara_exploration_03_torso | Exploration_03_Torso |
| Leather Jacket | paperdoll_piece_tr11_lara_tr10_london_jacket | Lara_Loyalty_TR10_LeatherJacket |
| Manko's Boots | paperdoll_piece_tr11_lara_stealth_03_legs | Stealth_03_Legs |
| Manko's Tunic | paperdoll_piece_tr11_lara_stealth_03_torso | Stealth_03_Torso |
| Mantle Of Six Sky | paperdoll_piece_tr11_lara_assault_02_torso | Assault_02_Torso |
| Midnight Sentinel | paperdoll_piece_tr11_lara_dlc_original_yaaxil | DLC_Outfit_11 |
| Midnight Sentinel - Farseer | paperdoll_piece_tr11_lara_dlc_original_yaaxil_alt | DLC_Outfit_07 |
| Nine Strides' Boots | paperdoll_piece_tr11_lara_exploration_02_legs | Exploration_02_Legs |
| Nine Strides' Harness | paperdoll_piece_tr11_lara_exploration_02_torso | Exploration_02_Torso |
| Ozcollo's Greaves | paperdoll_piece_tr11_lara_path_jaguar_legs | Jaguar_01_Legs |
| Ozcollo's Tunic | paperdoll_piece_tr11_lara_path_jaguar_torso | Jaguar_01_Torso |
| Quenti Palla's Greaves | paperdoll_piece_tr11_lara_path_eagle_legs | Eagle_01_Legs |
| Quenti Palla's Mantle | paperdoll_piece_tr11_lara_path_eagle_torso | Eagle_01_Torso |
| Remnant Jacket | paperdoll_piece_tr11_lara_tr10_remnant | Lara_Loyalty_TR10_RemnantJacket |
| Reptile Hide | paperdoll_piece_tr11_lara_dlc_predator | Predator_01 |
| Reptile Hide - Leviathan | paperdoll_piece_tr11_lara_dlc_predator_alt | DLC_Outfit_06 |
| Robes Of Puka Huk | paperdoll_piece_tr11_lara_crimson_huntress | Lara_Shaman |
| Scales Of Q | paperdoll_piece_tr11_lara_yaaxil | Yaaxil_01 |
| Scales Of Q - Feathered Serpent | paperdoll_piece_tr11_lara_yaaxil_alt | DLC_Outfit_03 |
| Serpent Guard | paperdoll_piece_tr11_lara_highpriest | Lara_WarriorPriest |
| Shadow Runner | paperdoll_piece_tr11_lara_tr10_shadowrunner_torso | Lara_Loyalty_TR10_ShadowRunner |
| Siberian Ranger | paperdoll_piece_tr11_lara_tr10_siberian_ranger | Lara_Loyalty_TR10_SiberianRanger |
| Sinchi Chiqa Battle Dress | paperdoll_piece_tr11_lara_dlc_rebelwarrior | Rebel_01 |
| Sinchi Chiqa Battle Dress - Honor Guard | paperdoll_piece_tr11_lara_dlc_rebelwarrior_alt | DLC_Outfit_02 |
| Survivor | paperdoll_piece_tr11_lara_tr9_yamatai | Lara_Loyalty_TR9_ShipwreckedSurvivor |
| Tactical Adventurer | paperdoll_piece_tr11_lara_tactical | Lara_BlueTank |
| Tactical Adventurer (Black) | paperdoll_piece_tr11_lara_classic | Lara_Classic |
| Tactical Adventurer Classic | paperdoll_piece_tr11_lara_tactical_alt | DLC_Outfit_12 |
| Three Fangs' Greaves | paperdoll_piece_tr11_lara_path_snek_legs | Snake_01_Legs |
| Three Fangs' Tunic | paperdoll_piece_tr11_lara_path_snek_torso | Snake_01_Torso |
| Tomb Raider 2 | paperdoll_piece_tr11_lara_tr2 | Lara_Loyalty_TR2 |
| Tunic Of The Exiled Fox | paperdoll_piece_tr11_lara_dlc_huntress | Huntress_01 |
| Tunic Of The Shorn One | paperdoll_piece_tr11_lara_quoriankasuit | DLC_Outfit_10 |
| Vest Of The Empress Jaguar | paperdoll_piece_tr11_lara_stealth_01_torso | Stealth_01_Torso |
| Wraithskin | paperdoll_piece_tr11_lara_tr10_wraithskin | Lara_Loyalty_TR10_Wraithskin |
| Yaway's Battle Tunic | paperdoll_piece_tr11_lara_assault_01_torso | Assault_01_Torso |
| Yaway's Wooden Greaves | paperdoll_piece_tr11_lara_assault_01_legs | Assault_01_Legs |
| (Main menu) | paperdoll_generator_lara_menu |  |

The .drm files containing the outfit icons (for the outfit selection menu at campsites)
can be found in the following folder:

pcx64-w\design\image resources\campsite\outfits


## Appendix 2: SOTTR Photo Mode

The .tr11anim files listed below can be found in tr11_lara.drm.

### Face

| Ingame name | .tr11anim ID |
| ----------- | ------------ |
| Neutral | 30901 |
| Smiling | 30900 |
| Happy | 30899 |
| Sly | 30898 |
| Sad | 30897 |
| Surprised | 30896 |
| Disgusted | 30895 |
| Angry | 30894 |
| Wonderment | 30893 |
| Displeased | 30892 |
| Smug | 33016 |
| Annoyed | 33018 |
| Innocent | 33017 |

### Body

| Ingame name | .tr11anim ID |
| ----------- | ------------ |
| Celebrating | 33015 |
| Exulting | 33014 |
| Victorious | 33013 |
| Flexing | 33006 |
| Framing High | 33005 |
| Framing Eye Level | 32998 |
| Thumbs Up | 33003 |
| Scouting | 33012 |
| Sneaking | 33001 |
| Kneeling | 32999 |
| Folded | 33011 |
| Balancing Left | 33004 |
| Balancing Right | 33007 |
| Predatory | 32997 |
| Successful | 33010 |
