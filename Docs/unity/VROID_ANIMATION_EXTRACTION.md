# VRoid Studio animation extraction

Use this runbook for named motions from the locally installed VRoid Studio. For a batch, build the
asset index and export the Unity project once. Deliver only the requested clips. For Cheryll in
Unity, use a Humanoid `.anim` when available. Bake an FBX when the deliverable calls for FBX.

## Extract with Python

Run [the extractor](../../Unity/Tools/extract_vroid_animations.py) from the repository root with
Python 3. It uses AssetStudioMod and AssetRipper and needs no Python packages.

```powershell
python Unity/Tools/extract_vroid_animations.py --list walk
python Unity/Tools/extract_vroid_animations.py stroll walk_00
python Unity/Tools/extract_vroid_animations.py "stroll#240" --output "C:/Temp/VRoid-clips"
```

Use `--list` without a filter for all clips. The list shows each clip's name, PathID, and source
file. Pass exact clip names to extract them in one command. If a name is ambiguous, quote
`"name#PathID"` in PowerShell. By default, extracted `.anim` files go to
`Downloads/VRoidAnimations` in your user folder. Choose a new output directory when a file with
the same name already exists.

### Import directly into Unity

Set `--output` to a folder under `Unity/JewelryShop/Assets`. The extractor creates the folder and
writes the selected clips there. Use `VRoid_AIM/Locomotion` for walk clips or a sibling such as
`VRoid_AIM/Gestures` for gestures.

```powershell
python Unity/Tools/extract_vroid_animations.py stroll walk_00 --output "Unity/JewelryShop/Assets/Avatar/VRoid_AIM/Locomotion"
```

Refresh the Unity Project window if the files have yet to appear. To use a clip on Cheryll,
select the intended state in `CheryllPreview.controller` and assign the clip in **Motion**.

### Example — Extract and name one Stroll clip

Run this from the repository root.

```powershell
python Unity/Tools/extract_vroid_animations.py stroll --output "Unity/JewelryShop/Assets/Avatar/VRoid_AIM/Locomotion"
```

The script creates `Assets/Avatar/VRoid_AIM/Locomotion/stroll.anim`. The first argument is the
original VRoid clip name. The output option sets the folder.

In Unity's **Project** window, select `stroll.anim`, press **F2**, and enter `VRoid_Stroll`.
Unity keeps the `.anim` extension and its asset reference when you rename it there.
To use the clip for normal walking, drag `VRoid_Stroll` to **Motion** on the `Walk` state in
`CheryllPreview.controller`.

### Use tools or a VRoid installation from another folder

The script finds standalone VRoid Studio and reuses tools in `%TEMP%/Vroid-AssetStudio-v020`.
Use `--bundle` for Steam or another installation. Use `--studio` and `--ripper` for the tool
executables from the release links below.

```powershell
python Unity/Tools/extract_vroid_animations.py stroll `
  --bundle "C:/path/to/VRoidStudio_Data/data.unity3d" `
  --studio "C:/Tools/AssetStudioMod/AssetStudioModCLI.exe" `
  --ripper "C:/Tools/AssetRipper/AssetRipper.GUI.Free.exe"
```

The script handles Humanoid clip extraction. Use the FBX section below for baking and round-trip
verification.

## Locate and index

1. Find `VRoidStudio_Data/data.unity3d` beside the executable. The standalone installation checked
   for Stroll was `%LOCALAPPDATA%/Programs/VRoidStudio/2.16.0` and used Unity `6000.0.62f1`.
   Steam installations can be located via **Installed Files → Browse**.
2. Reuse the existing Windows tools when available. The tested versions were
   [aelurum AssetStudioMod CLI 0.20.0](https://github.com/aelurum/AssetStudioMod/releases/tag/v0.20.0)
   and [AssetRipper 2.0.0](https://github.com/AssetRipper/AssetRipper/releases/tag/2.0.0).
   The [FrandreJoestar fork](https://github.com/FrandreJoestar/AssetStudioMod) documents Animator
   export in its GUI. The aelurum CLI supports Unity 6 and Animator export.
3. Put tool output in a temporary folder. Set `$bundle` to the full path of `data.unity3d`.
   Set `$studio` to the CLI executable and `$index`, `$cache`, and `$unpacked` to output paths.

   ```powershell
   & $studio $bundle -m info --load-all --export-asset-list xml --decompress-to-disk --cache-dir $cache -o $index
   ```

4. Read `$index/assets.xml`. Match `AnimationClip` names case-insensitively. Inspect
   Photo Booth `AnimatorController` assets when display names differ from clip names. Record each
   clip's `Name`, `PathID`, and `Source`, then group the requested clips by source file.
   On 2.16.0, `stroll` was clip `240` in `sharedassets0.assets`, and `@stroll` was Animator
   `28805` in `resources.assets`.

## Recover Humanoid clips

1. Unpack the bundle once. Copy the identified source `.assets` file and its matching `.resS`
   into an isolated input folder. For Stroll this was `sharedassets0.assets` and
   `sharedassets0.assets.resS`. Unpacking the main bundle uses several GB of temporary disk space.

   ```powershell
   & $studio $bundle -m extract --decompress-to-disk --cache-dir $cache -o $unpacked
   ```

2. Start AssetRipper with `--headless --port 24187`. Set `$sourceAsset` to the full path of the
   isolated `.assets` file and `$exportFolder` to an empty output directory. Use the GUI's
   **Load File** and **Export Unity Project** actions or the equivalent local requests.

   ```powershell
   curl.exe --fail --data-urlencode "Path=$sourceAsset" http://127.0.0.1:24187/LoadFile
   curl.exe --fail --data-urlencode "Path=$exportFolder" http://127.0.0.1:24187/Export/UnityProject
   ```

3. Select the requested `.anim` files in `ExportedProject/Assets/AnimationClip`. Import them into
   Unity and verify `AnimationClip.humanMotion`, positive duration, and actual bone movement on
   Cheryll. For a walk loop, check `isLooping`, verify that the clip adds no root displacement,
   and confirm the controller returns to Idle on input release. Delete temporary Unity probe
   assets through the editor after verification.

## FBX export

For an Animator with bound transform curves, try AssetStudioMod Animator export with
`-m animator --filter-by-pathid <animator-id> --fbx-animation auto`. Verify that the FBX contains
transform keyframes. For Stroll, both `auto` and `all` produced an FBX with zero animation curves,
so the successful route was the recovered Humanoid `.anim`.

To bake that clip as FBX, sample it on a temporary Cheryll at 120 fps using an
`AnimationClipPlayable` with a manually evaluated graph. Capture the bind-pose hierarchy and
animated transform for each Humanoid bone. Key those transforms on a Blender armature and export
only the armature with **Bake Animation** enabled and **Add Leaf Bones** disabled. Re-import the
FBX with Rig **Humanoid**, check the clip duration and looping, and play it on Cheryll again.
Stroll passed this round trip at 1.208 seconds. The recovered `.anim` retained the original
Humanoid curves.

## Cheryll animation folders

- `Assets/Avatar/Animations/Cheryll/Locomotion` holds the active Idle and Walk clips.
- `Assets/Avatar/Animations/Cheryll/Gestures` holds the Staff Console gesture clips.
- `Assets/Avatar/Animations/Cheryll/Poses` holds pose clips.
- `Assets/Avatar/VRoid_AIM` holds reusable VRoid export files such as `Cheryll_Stroll.fbx`.

Use Unity asset moves to retain GUIDs when organizing clips. `CheryllPreview.controller` resolves
the active Walk state to `Locomotion/Walk.anim`, which is the recovered Stroll clip.
