# Five Pebbles body, gown, and arms

## Evidence inspected

- Managed artifact: `D:\SteamLibrary\steamapps\common\Rain World\RainWorld_Data\Managed\Assembly-CSharp.dll`, SHA-256 `b6be1d4e18ce219d21091b51564cb6a11c1e4106b41de903eb8e58849cb16fdb`, MVID `90dcf104-fbdf-4d42-b147-5089d56af681`.
- REA inspected the managed `OracleGraphics` members; ILSpy was used to verify the exact nested method bodies in the same installed assembly.
- The earlier room recording is Looks to the Moon and remains general room-scale evidence only. The newer `C:\\Users\\47317\\Downloads\\output.mp4` is treated as Five Pebbles footage: its orange gown and pink head match SS. It is a 720x720, 24 fps, 18.12-second Bilibili re-encode, so use it for visible proportions, overlap, and timing; use the installed source for exact geometry and per-frame rules.
- In the newer clip, sampled at 0.5s, 7.5s, 11.5s, and 17.5s, SS keeps a short, narrow gown directly under the head. One hand/arm can project sideways while the other hangs closer to the gown, so the silhouette is asymmetric. The arm base appears on the left wall early, travels along the upper edge in the middle, and returns to the left wall near the end. The main umbilical still leaves the lower room edge independently of the arm. This clip does not show the full four-edge travel.
- The installed `SS_AI` room settings include `ZeroG`; Unity's `roomGravity` is therefore set to zero for this room adaptation. The source algorithm still reads `room.gravity` per update.

## Five Pebbles source behavior

- SS creates one `OracleGraphics.Gown` with 11 divisions on both axes and one untextured `Futile_White` triangle grid. Moon and Saint Pebbles use the `MoonCloakTex` branch; SS does not.
- `Gown.IdealPosForPoint` uses the first body chunk as its origin and the normalized direction from body chunk 1 to chunk 0. Width interpolates from 5 to 11 game pixels; the axis offset interpolates from +5 to -18 pixels and is modified by the source sine profile.
- `Gown.Update` stores last position, current position, and velocity per point. Per source update it adds velocity to position, damps velocity by `0.999`, applies `0.9 * room.gravity` downward, adds the `0.02` directional guide, caps each point's distance from its ideal point at `lerp(0,9,t)`, and adjusts velocity between the four cardinal neighbours by `0.05`. There is no spring-to-target, hem rounding, pinned bottom row, diagonal constraint, or decorative fold shading.
- SS gown vertex colors are `HSL2RGB(lerp(0.08,0.02,t²), lerp(1,0.8,t), 0.5)`. The indexing in `InitiateSprite` is transposed relative to `DrawSprite`, so the visible color ramp follows the gown length.
- Oracle body chunks use `Circle20`, radius 6 pixels each, with a 9-pixel connection and `SkinColor() = (1, 0.4, 203/255)`. The SS Gown is drawn over these chunks.
- Each hand is a `GenericBodyPart` with radius 2, surface friction `0.5`, and air friction `0.98`. It is updated and constrained to a 15-pixel radius around body chunk 0, then gets the source gravity, body-axis and side drift.
- Each SS arm is `TriangleMesh.MakeLongMesh(7, pointyTip:false)`: seven samples, four vertices per sample, the source 26-triangle strip topology, width 4 pixels, a source `Custom.Bezier` curve from body chunk 0 to the simulated hand, and alternating `Gown.Color(0.4)` / `Gown.Color(0)` vertex colors. The curve, topology and mesh vertex formulas are reproduced in `PuppetBodyGraphics`.
- Oracle hands use the `haloGlyph-1` sprite and `SkinColor()`. The project atlas already contains that named source sprite; both scene hand renderers now reference it directly.
- The source draw sequence places body chunks first, the neck next, the Gown above them, then arm/hand, head, chin, eyes and phones. Unity sorting values preserve that order.

## Desktop mapping

- `PuppetBodyGraphics` disables the authored robe renderer and legacy sleeve renderers. It does not sample `Assets/Robe.png` or `Assets/Sleeve.png`.
- Body chunk centers are mapped to the animated Unity torso and a 9-pixel lower offset. The original game's room collision, Oracle body physics, and independent body-chunk histories do not exist in this desktop scene; its head-to-torso axis and fixed-step simulation are the current adaptation seam.
- Gown and hands are stepped at 40 Hz in the desktop build, with render interpolation between simulated positions. The SS_AI ZeroG setting maps to `roomGravity = 0`.
- The old Unity scene had `RobeBody` 24.5 pixels below the head; moving it to a 9-pixel gap still hid the neck because the gown reaches 5 pixels above body chunk 0. The current anchor is 14 pixels below the head, matching the source head's maximum offset: `ConnectToPoint(body chunk 0 + 6 px, connection radius 8 px)`. The head is back at source `head.rad = 5`; its lower edge plus the gown's +5 px upper edge leave a short visible neck stub. `OracleGraphics` draws the 3 px neck behind the gown, so this is the source occlusion relationship rather than a neck layer moved in front.
- The source `GenericBodyPart` constructor resets hands to the first body chunk plus a random unit vector (one pixel), then simulates them. The prior Unity pose initialized hands 8 pixels sideways and 7 pixels down. They now start at the body anchor and acquire their pose through the source drift and connection rules.
- `OracleArm.Joint.Update` steers its terminal joint around body chunk 0 while using chunk 1 and `GetToDir` for its back-side offset. `ArmBackAttachment` previously added another 11.5 pixels downward from the Unity body anchor; that authored offset is now zero, placing the socket on the body chunk anchor. The mechanical arm remains behind the gown in Unity's sorting order.
- `Gown.Color` returns Rain World's sRGB HSL palette. This Unity project uses Linear color space, so mesh vertex colors now convert that palette to linear before upload; without the conversion the rendered gown shifted from the source orange/red ramp toward yellow/coral.

## Verification record

- Source checks: installed SS `OracleGraphics.Gown`, arm `DrawSprites`, `ApplyPalette`, Oracle constructor and `GenericBodyPart` methods.
- Room check: installed `world/ss-rooms/ss_ai_settings.txt` lists the `ZeroG` effect.
- Windows build with Unity `6000.4.8f1`: succeeded through the Editor's direct Windows Player build command. `Builds/PuppetBodyPreview/NeckRatioBuild.log` ends with `Build Successful` and process exit code 0; it reported existing obsolete API/unused-field warnings and no C# compile errors.
- Transparent-window captures from the rebuilt `Iterator_Build` player: center (`Builds/PuppetBodyPreview/NeckRatioCenter.png`) and side (`Builds/PuppetBodyPreview/NeckRatioSide.png`). The comparison (`Builds/PuppetBodyPreview/SSNeckComparison.png`) uses the 00:07.5 frame from the user's clip and scales the Unity crop to the same gown height, so it compares internal proportions rather than absolute screen size. The short neck now remains visible at both poses. The clip is compressed and watermarked; use it for silhouette and overlap, not subpixel color scoring.
- A scripted fast mouse drag did not move the puppet in the captured frame, so drag/release behavior is not marked as verified by this pass. The source-driven static side pose does render, but it does not replace an interactive drag check.
- SS visual comparison is now available from the newer `output.mp4` clip. Compression and watermark obscure subpixel edge comparison, but the clip is sufficient to check torso-to-head spacing, arm/hand silhouette, and the phases of arm motion.
