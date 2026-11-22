# Oracle puppet head: the original sprite stack

The game does not draw the iterator head with a mesh. `OracleGraphics` builds it from
three sprites and hangs the side housings on top. This note records the numbers and
how the desktop pet now uses the same sprites.

## Sprites (shipped `rainWorld` atlas)

| Element | Frame (x, y, w, h) | Used for |
| --- | --- | --- |
| `Circle20` | 173, 132, 20, 20 | head, chin, phone discs |
| `pixel` | 184, 455, 1, 1 | eyes, neck |
| `LizardScaleA1` | 356, 322, 4, 14 | phone spike |

All three are exported to `Assets/Resources/IteratorArm/` by
`Assets/Editor/IteratorArmSpriteExporter.cs`.

## Code path: `OracleGraphics`

`InitiateSprites` (head slots start at `firstHeadSprite`, ten of them):

- `neckSprite`: `pixel`, `scaleX = 3` on Five Pebbles (4 otherwise), `anchorY = 0`;
- `HeadSprite = firstHeadSprite + 3`: `Circle20`, `scaleX = head.rad / 9`,
  `scaleY = head.rad / 11`, with `head.rad = 5` from
  `new GenericBodyPart(this, 5f, ...)`;
- `ChinSprite = +4`: `Circle20`, `scale = head.rad / 15`;
- `EyeSprite(0..1) = +5, +6`: `pixel` (white on Pebbles, `(0.02, 0, 0)` on Moon);
- `PhoneSprite(side, 0..2)`: `Circle20`, `Circle20`, `LizardScaleA1`, the last one with
  `anchorY = 0`, `scaleY = 0.8`, `scaleX = ±0.75`.

`DrawSprites`:

- `vector` = first body chunk, `vector5` = head position,
  `vector6 = DirVec(head, body)` (down), `vector7 = PerpendicularVector(vector6)`,
  `vector8` = look direction rotated into the body frame;
- neck: placed at the body chunk, `rotation = VecToDeg(vector6)`-style aim at the head,
  `scaleY = distance(body, head)`;
- head: at the head position, `rotation = VecToDeg(vector6)` so the long axis stays
  across the neck;
- chin: `Lerp(head, body, 0.15) + vector7 * (look.x * 2)`;
- eyes: `head + vector7 * Clamp(look.x * 3 + 2.5 * ±1, -5, 5) + vector6 * (1 - look.y * 3)`,
  `scaleX = 1 + look influence + (1 - eyesOpen)`, `scaleY = Lerp(1, 2, eyesOpen)`,
  `alpha = 0.5 + 0.5 * eyesOpen`;
- phones: `head + vector7 * Clamp(Lerp(7, 5, |look.x|) * ±1, -11, 11)`, discs with
  `scaleX = Lerp(3.5, 5, |look.x|) / 20`, `scaleY = 5.5 * (1 or 0.8) / 20`, and the
  spike aimed with `AimFromOneVectorToAnother(body, phone - vector6 * 40 - look * 10)`.

`ApplyPalette`: `SkinColor()` `(1, 0.4, 203/255)` for body chunks, neck, head, chin,
hands and feet; eyes stay white on Pebbles; the phone discs and spike take the arm
joint base and highlight colours.

## Desktop implementation

`Assets/Scripts/PuppetHeadGraphics.cs` (rewritten, same class name so the scene
binding survives):

- the legacy mesh shell, mesh eyes and the pre-placed child sprites are disabled;
- head disc, chin disc, neck bar and two phone groups (base disc, highlight disc,
  spike) are created as `SpriteRenderer`s and driven every frame with the numbers
  above, converted through `pixelsPerUnit = 100` and `detailScale = 1.35`;
- the eyes reuse the existing `PuppetAnimator` sockets, so blinking and the
  look offset keep coming from the pet's own animation while the sprite itself is the
  original `pixel`;
- sorting: neck 0, head 2, chin 3, eyes 4, phones 5, above the robe (-1) and below the
  sleeves (2 -> the sleeves are only 2, so the head overlaps them like the game's
  front layer);
- look comes from `SSOraclePetBrain.GazePoint` scaled by `AttentionWeight`, the same
  source `PuppetAnimator` already used.

## Deliberate differences

- `detailScale` enlarges every sprite so the head keeps its proportion against the
  desktop arm and robe, which are drawn at roughly 1.35× the game's pixel size.
- Eye alpha stays at 1 and the blink is done by squeezing the socket, because
  `PuppetAnimator` already owns the blink timing; the original blends alpha as well.
- The robe, sleeves and hands keep the project's existing art; only the head stack was
  part of this pass.

## Verification

- `Logs/puppet-sprite-export.log`: `LizardScaleA1` cropped at `4x14`.
- `Logs/puppet-build.log`: Unity `6000.4.8f1` build, `Build Finished, Result: Success`.
- Frames: `Builds/PuppetPreview/frames/head_idle*.png` (resting),
  `head_side*.png` (puppet moved to the right, look offset applied). Reference crop of
  the shipped game in `Builds/RefPuppet/puppet_zoom.png`.
