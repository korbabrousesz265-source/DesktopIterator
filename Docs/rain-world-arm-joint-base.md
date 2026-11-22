# Oracle arm joints and carriage: sprite restoration

This note records where the joint and base detail of the iterator arm comes from in the
shipped game and how the desktop pet now uses the same pixels. It continues
[rain-world-arm-cable-reconstruction.md](rain-world-arm-cable-reconstruction.md).

## Source artifacts

- Game install: `D:\SteamLibrary\steamapps\common\Rain World`, version `v1.11.8`
  (`RainWorld_Data\StreamingAssets\GameVersion.txt`).
- Managed assembly: `RainWorld_Data\Managed\Assembly-CSharp.dll`, decompiled with
  `ilspycmd 11.2.0`.
- Sprite sheet: `RainWorld_Data\resources.assets` → `TextAsset` `rainWorld` (TexturePacker
  JSON, 120 262 bytes) and `Texture2D` `rainWorld` (464×512, RGBA32).
  The sheet is a white mask: `Futile` tints each element with the room palette, so every
  element pixel is `(255,255,255,alpha)`.
- The project already carries both files at `Assets/rainWorld.png` and `Assets/rainWorld.txt`.

Atlas rects (TexturePacker coordinates, top-left origin) of the elements the arm uses:

| Element | x | y | w | h | Used for |
| --- | --- | --- | --- | --- | --- |
| `Circle20` | 173 | 132 | 20 | 20 | joint discs, carriage side discs |
| `deerEyeB` | 452 | 474 | 5 | 5 | bolt caps, support knees |
| `MirosLegSmallPart` | 194 | 428 | 4 | 25 | toothed strut under every elbow |
| `pixel` | 184 | 455 | 1 | 1 | cogs, rods, piston bars |

## Code path in the game

`OracleGraphics` (nested types `ArmJointGraphics` and `ArmBase`) owns the arm renderer.

### `ArmJointGraphics`

- `totalSprites = 15`; `meshSegs = totalLength / 10`; `smallArmLength = totalLength / 4`.
- Sprite slots: `CogSprite` = cogs 0–2, `MetalPartSprite` = 3–6, `SegmentSprite` = long
  triangle meshes, `JointSprite(1,*)` at `firstSprite + 7/8`, `JointSprite(0,*)` at
  `firstSprite + 13/14`.
- `InitiateSprites`: `MetalPartSprite(0)` is `MirosLegSmallPart` with `anchorY = 1`,
  `scaleX = 1.5 / 1.0 / 0.8` by joint index and `scaleY = 2 / 1 / 0.5`;
  `MetalPartSprite(1)` and `(2)` are `pixel` bars with `anchorY = 0`, `scaleX = 2` on the
  first joint and `1` afterwards; `MetalPartSprite(3)` is `deerEyeB`.
  Joint discs are `Circle20` at `(7 - index * 0.5) / 10` (pivot) and `(6 - index * 1.5) / 10`
  (elbow), each with a `deerEyeB` bolt on top. Cogs are `pixel` bars with
  `scaleY = 18 - index * 2`, `scaleX = 5 - index * 0.5`.
- `DrawSprites` places the pivot disc `12 px` along the arm for joint 0 and `2 px` for the
  rest, the elbow disc exactly on `ElbowPos`, the cogs at `AimToElbow + 60° * cog`,
  `MirosLegSmallPart` hanging from the elbow back to the 20 % chord point, one `pixel` rod
  from the elbow towards the 80 % chord point with `scaleY = totalLength / 4`, a second rod
  from the next joint towards that rod end with `scaleY = distance + 10`, and a `deerEyeB`
  at the rod end.
- `Update` turns the cogs by the change of the chord length:
  `delta = (lastChord - chord) / totalLength`, `cogRotat += delta * (±1) * 2`, sign by
  joint parity.
- `ApplyPalette` sets `metalColor = Lerp(blackColor, texture(5,5), 0.12)` for discs, cogs and
  metal parts, and `Lerp(metalColor, HighLightColor, 0.5)` for the bolts.

### `ArmBase`

- `totalSprites = 13`: three `TriangleMesh("Futile_White")` brackets, two piston supports per
  side with a knee bolt, and two `Circle20` discs per side.
- `DrawSprites` starts at the first joint, subtracts `BaseDir * 10 px`, and builds the
  12-vertex bracket through `UpdateMesh` with `(width, height, innerWidth)` equal to
  `(30, 17, 20)`, `(24, 15, 30)` and `(30, 17, 20)` for the three layers.
- Supports attach at `pos + dir * 11 px ± perp * 17 px`, solve
  `Custom.InverseKinematic(foot, lerp(joint0, elbow0, 0.25), 25, 45, ±1)` and are `pixel`
  bars with `scaleX = 2`; the knee carries a `deerEyeB`. The side discs are `Circle20` at
  `0.5` and `0.45` scale, the smaller one offset by `(-1, +1) px`.
- `ApplyPalette`: the outer bracket layer uses `Lerp(metalColor, BaseColor, 0.8)`, the middle
  layer `Lerp(metalColor, HighLightColor, 0.8)`, the supports and the smaller disc keep
  `metalColor`.

## What the desktop project now does

- `Assets/Editor/IteratorArmSpriteExporter.cs` crops exactly those four elements out of
  `Assets/rainWorld.png` into `Assets/Resources/IteratorArm/` (point filter, no mipmaps, no
  compression, 100 px per unit). Menu: `Tools/Iterator Arm/Export Joint And Base Sprites`.
- `Assets/Scripts/MechanicalArm/OracleArmSprites.cs` (execution order 60) creates one
  `SpriteRenderer` per element and lays them out with the numbers above, converting Futile
  pixels with `pixelsPerUnit = 100`. It is added automatically by `OracleArmGraphics.Awake`,
  so the scene needs no new component.
- `Assets/Scripts/MechanicalArm/OracleArmGraphics.cs` keeps only the code-drawn shells and the
  puppet cuff; the previous procedural mount, bearings, bearing lips and sleeves were removed
  because those parts are the sprite layers now.
- Sorting orders: rods `-14`, cogs `-13`, elbow discs `-12`, elbow bolts `-11`, shell mesh
  `-10`, pivot discs `-8`, pivot bolts `-7`, carriage plate `-6`, supports `-5`, base discs
  `-4`, puppet socket `-3`. Everything stays under the robe (`-1`) and above the umbilical
  (`-16 … -14`).

## Deliberate differences from the game

The desktop shell is roughly three times thicker than the original 12 px band, so three
adaptations keep the original proportions instead of the original absolute sizes:

1. `detailScale` (default `1.35`) enlarges the discs, bolts, cogs, struts, carriage plate and
   supports together with the shell.
2. The carriage plate sits *inside* the screen edge (`baseInset`, `baseHeight * 0.8`) instead
   of behind a room wall, otherwise the whole bracket would be clipped away. The piston pair
   is fitted to the distance it has to cover (`span * 1.03`), so the legs do not cross.
3. The two `pixel` rods are trimmed to stay under the plates: the near rod is limited to 90 %
   of the distance to its chord guide, the far rod to the second link length. The sprites and
   their directions are unchanged.

`pixelsPerUnit`, `detailScale`, `baseInset`, the three base layers' size fields, every
sorting order and the three palette colours are exposed in the Inspector.

## Verification

- `Logs/arm-sprite-export.log`: the four elements were cropped (`20x20`, `5x5`, `4x25`,
  `1x1`).
- `Logs/arm-sprite-build.log`: Unity `6000.4.8f1` player build, `Build Finished, Result:
  Success`, no compiler errors.
- Captures from the built player through `Assets/Scripts/Diagnostics/PreviewCapture.cs`
  (`-previewCapture <folder> -previewPuppet x,y`):
  `Builds/ArmSpritePreview/frames/01_idle*.png` (resting), `02_side*.png` (puppet at the right
  side), `03_corner*.png` (puppet in the lower-right corner). Frames were composited over a
  neutral grey because the window clears to transparent black.

## Remaining unknowns

- The footage supplied earlier still does not resolve whether the two rod tips carry their own
  bolt in every pose; the code says they do, and this pass follows the code.
- The game's palette is computed from the room darkness; the desktop palette is a fixed
  approximation (`metalColor`, `plateColor`, `plateHighlightColor`).
- The rail, drag and cable behaviour were not touched by this pass.
