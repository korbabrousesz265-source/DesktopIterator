# Umbilical trunk: the original segment banding

Rain World does not draw the main cord as a single thick ribbon. It draws a thin
long mesh and then stacks two `CentipedeSegment` sprites on every sample, which is
where the light and dark segment lines come from. This note records the source
numbers and how the desktop pet now uses the same sprites.

## Atlas element

`CentipedeSegment` in the shipped `rainWorld` atlas (JSON coordinates, top-left
origin): `x 348, y 109, w 18, h 15`. The alpha is a rounded plate: the first two and
last two rows are notched in the middle, the eleven rows between them are solid
across the full 18 px. Exported to `Assets/Resources/IteratorArm/CentipedeSegment.png`
by `Assets/Editor/IteratorArmSpriteExporter.cs`.

## Code path: `OracleGraphics.UbilicalCord`

- `coord` holds 80 samples; the distance pass keeps neighbours exactly `10 px`
  apart, so the rest length is 79 × 10 = 790 px.
- Connections, in `SetStuckSegments` and `Update`:
  - sample 0 is pinned to a room tile centre — `MiddleOfTile(24, 2)` for Five
    Pebbles, `(75, 38)` in RM and `(118, 6)` in CL with MSC;
  - samples `last-21 … last-19` are pulled towards
    `Lerp(joint1.pos, joint1.ElbowPos(joint2.pos), 0.4 / 0.47 / 0.54) +
    PerpendicularVector * 8 px` with weight `1 / 0.5 / 0.5`, so the cord runs past
    the first arm link;
  - the last sample is held within `80 px` of `bodyChunks[0]`, the puppet shoulder.
- `InitiateSprites`:
  - sprite 0: `TriangleMesh.MakeLongMesh(80)`;
  - `SegmentSprite(i, 0)`: `CentipedeSegment`, `scaleX 0.5`, `scaleY 0.3`;
  - `SegmentSprite(i, 1)`: `CentipedeSegment`, `scaleX 0.4`, `scaleY 0.15`.
- `DrawSprites`:
  - the mesh ribbon uses `num = 1.2`, i.e. a 2.4 px wide strip, with four vertices per
    sample offset by ±1.2 px across the cord;
  - both band sprites sit exactly on the sample, rotated by
    `Custom.VecToDeg((dirPrev + dirNext).normalized) + 90`. Since
    `VecToDeg(v) = atan2(v.x, v.y)`, that puts the sprite's 18 px axis along the cord
    and its 15 px axis across it: each band is 9 px long and 4.5 px across, with the
    smaller light band 7.2 × 2.25 px on top.
- `ApplyPalette`:
  - ribbon `metalColor` (`Lerp(blackColor, texture(5,5), 0.12)`);
  - band 0 `Lerp(BaseColor, metalColor, 0.5)`;
  - band 1 `Lerp(HighLightColor, metalColor, 0.35)`.

So the cord is a 2.4 px dark string carrying a 9 × 4.5 px band every 10 px, each band
with a lighter 7.2 × 2.25 px core: a caterpillar of light-and-dark segments, not a
smooth tube.

## Desktop implementation

`Assets/Scripts/PuppetCords/OracleUmbilicalGraphics.cs`:

- the trunk is now a plain 80-row ribbon mesh, half width `trunkHalfWidthPx = 1.2`
  scaled by `detailScale`, coloured `trunkColor` (the `metalColor` stand-in);
- bands are placed by arc length every `bandSpacingPx = 18` × `detailScale`. In
  the `00:42.00` footage the nearly vertical run above the floor has a pitch of
  about 13 px over a visible room height of about 540 px. The old desktop build
  drew a pair every 19–20 px on a 1440 px screen. The new pitch is 0.243 world
  units, about 35 px on that screen, with the sprite dimensions held constant;
- rotations use `Atan2(dir.y, dir.x)`, which is the Unity equivalent of
  `VecToDeg(dir) + 90`: local +X follows the cord;
- sorting: ribbon `-17`, dark band `-16`, light band `-15`, fine cords `-14`, arm
  shell `-10`;
- the previous procedural "collar" profile mesh and its highlight rows are gone.

## Length and connection observations

- The original cord is fixed at both ends (room tile / shoulder leash) and passes the
  first arm link, with a rest length of 790 px inside a 1200 × 800 room — about 0.65
  of the viewport diagonal.
- The desktop cord is sized from the viewport so a dragged puppet can always reach a
  far corner: `OracleUmbilical.slackFactor` (default 1.07) times the diagonal. That
  is longer than the original ratio, so the settled cable folds into several loops
  between the floor mount and the arm guide. Lowering the factor towards 0.4–0.5
  gives the original single lazy curve, at the cost of the cord going taut when the
  puppet is dragged far away.
- The desktop still pins the cord at the bottom edge, at the first arm elbow, and at a
  receiver near the head, which mirrors the three original attachment points.

## Verification

- `Logs/cord-sprite-export.log`: `CentipedeSegment` cropped at `18x15`.
- `Logs/desktop-build.log`: Unity `6000.4.8f1` build, `Build Finished, Result:
  Success`, no compiler errors.
- Frames from the built player (`-previewCapture`, `-previewDelay`, `-previewPuppet`):
  `Builds/CordPreview/frames/cord_rest*.png` (1.4 s), `cord_settled*.png` (6 s) and
  `band_closeup.png`, the 5× crop of a settled stretch.

## Remaining unknowns

- The band's exact rotation sign cannot be told apart on a sprite with fourfold
  notched corners; the desktop uses the mirror-equivalent orientation.
- The fine data cords are still desktop line renderers; only the main trunk was part
  of this pass.
