# TR Reboot Mod Tools

This repository contains modding tools for the Tomb Raider Reboot games: Tomb Raider (2013),
Rise of the Romb Raider, and Shadow of the Tomb Raider.

[Download from NexusMods](https://www.nexusmods.com/shadowofthetombraider/mods/185?tab=files)

[Documentation](https://github.com/arcusmaximus/TrRebootModTools/blob/main/Documentation.md)

## Mod manager

A tool for installing/uninstalling mods. Temporarily disabling is also possible.

![Screenshot of the mod manager](Screenshots/shadow/manager.png)

## Extractor

A tool for extracting resources from the game.

![Screenshot of the extractor](Screenshots/shadow/extractor.png)

## Hook tool

A tool that hooks into a running game and does the following:

- Logs the in-archive files and (for SOTTR) animations accessed by the game in real time.
- Watches a mod folder for changes and automatically reinstalls it, making it possible to
  see those changes ingame without restarting.
- Allows editing material parameters and seeing the results instantly ingame.

![Screenshot of the hook tool](Screenshots/hooktool.png)

## Blender addon

Lets you import and export meshes, skeletons, and animations. Custom cloth physics are also possible.

![Screenshot of the Blender addon](Screenshots/shadow/blender.png)

## Binary templates

Template files for inspecting and modifying some file types with [ImHex](https://imhex.werwolv.net/) or [010 Editor](https://www.sweetscape.com/).
Can be used to tweak shader parameters in materials, for example.

![Screenshot of a binary template](Screenshots/template.png)

# Credits
alphaZomega for their [file format descriptions](https://www.nexusmods.com/riseofthetombraider/mods/20) and Ekey for their open source [CDCE TIGER Tool](https://github.com/Ekey/CDCE.TIGER.Tool). A large part of the knowledge used to create these tools comes from there.

Jostar, Raq, and swiz19 for beta testing.

Jostar for the mod manager icon.
