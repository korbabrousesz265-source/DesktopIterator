# Rain World SS arm and cable reconstruction notes

## Reference identity

- Artifact: the installed game's `Assembly-CSharp.dll`
- SHA-256: `b6be1d4e18ce219d21091b51564cb6a11c1e4106b41de903eb8e58849cb16fdb`
- MVID: `90dcf104-fbdf-4d42-b147-5089d56af681`
- REA member inventory Evidence: `ev_2cbd3d76b8be05c91a7fa40963610cdad53384ee2deeac330540dd599386ccce`

## Observed in the managed artifact

- `OracleArm..ctor` creates four linked joints with lengths 300, 150, 90, and 30 pixels. Its standard room frame uses tile centers (10,31), (38,31), (38,3), and (10,3), and begins at frame position 10002.5 (the position routine wraps the frame value by four).
- `OracleArm.OnFramePos` interpolates along the four frame sides and rounds the corners over 10% of an edge. `OracleArm.BaseDir` blends arm direction around the same corners.
- The Oracle-specific `Joint.Update` uses inverse kinematics and the game's shared terrain-collision routines. `ArmJointGraphics` builds its parts from flat sprites and long triangle meshes rather than one rail-shaped primitive.
- `UbilicalCord` stores an 80-sample main cord and 14 fine cords with 20 samples each. Its Verlet-like state uses 0.995 damping, shared terrain collision, and room-specific stuck points tied to arm joints.
- Cable rendering uses long meshes and sprite helpers. The fine-cord palette and arm colors are derived from the Oracle palette and room darkness.

REA Evidence used:

- Managed members: `ev_a619d4ad06a1032625a912138ce344149c8362a158e9fbd667fb7c0b0e1f4e3b`
- Call edges: `ev_dede81f5faeefb5bbb3cbe320c00818a39bfd5ffbaf21eeb28227b39a6e0e635`
- Field declarations: `ev_b318ee238796a91e0d8372bb44bc5c4521c6a992b08698fd3c393e18963d0fc4`

## Reconstruction decisions and limits

- The Unity scene has no Rain World room tile map. The four source room anchors are mapped into a centered 48-by-35-tile frame using the project's orthographic camera. The main cable uses a camera-edge anchor and the arm joint chain as pin points.
- The cable checks Unity 2D colliders when a terrain layer mask is assigned and always stays inside the visible camera rectangle. This gives the current scene useful constraints without importing Rain World's room engine.
- The geometry follows the source's flat, layered mesh construction. The color values are a subdued SS-inspired palette approximation; the original game's live palette/darkness calculation is not available in this Unity scene.
- REA inspection was static. The original game was not launched, and this Unity scene has not been rendered in the editor during this pass, so appearance and timing still require an in-editor comparison.

## Arm-only follow-up

- In the supplied Look to the Moon footage, the links near the puppet move at `00:09.50–00:10.00` while the carriage holds near the lower-right corner. The carriage turns and then slides along the bottom at `00:10.50–00:12.00`. Use `00:24` and `00:50` to compare folded silhouettes. The footage does not resolve the exact puppet-side pin or the identity of all dark lines visible around `02:30`.
- REA member and call evidence `ev_854dbac61145420f17bff9536478b442327a1e5dfd46b79e4e4c16ac71d99780` identifies a distinct `ArmBase` renderer with supports and meshes, and separate link, joint, and cog drawing in `ArmJointGraphics`. `OracleArm.Update` reads separate puppet and base targets and updates the carriage movement state. These static observations help interpret the footage without assigning each visible line to a renderer.
- The desktop adaptation keeps the rail on the viewport edge, blends carriage orientation through corners, and uses a reach threshold so the links can move before the carriage slides. The exposed side socket and the guide shoes are desktop designs. The existing trunk and fine-cord routes are outside this pass.

## Folding spans

- The archived assembly creates four `OracleArm.Joint` instances. The first follows the base. `ArmJointGraphics.DrawSprites` uses the next joint as its endpoint, or the second body chunk for the final span. `Joint.ElbowPos` computes alternating bend points within the spans and uses a separate rule at the end. The bends at `00:24` and `02:30` therefore do not imply extra independently simulated joints. REA managed evidence: `ev_1a3347cbc97e867a66bd170a427caca2ff7227bd07db10684cbb9d9ad1d4c52a`.
- In the desktop design each of the first three spans has a longer near piece and a shorter far piece. The socket span uses equal pieces. Four geometric elbows change their angles while the eight visible pieces keep their lengths. The elbow shells blend into the plates; exposed pins identify the four main pivots.
- The carriage uses separate start and stop distances to its target along the rail. Small changes and unsettled targets leave it in place. A held drag can start it immediately when the arm runs out of reach.

## Arm shells and bearings

- In the supplied footage, the first span narrows away from the carriage and widens again near the first main bearing. The pale contour itself curves at `00:24.20`, `00:49.84–00:50.16`, and `02:30.20`. The dark silhouette alongside it has a separate edge. The dark pins and round pale caps are visible, though the footage does not establish 3D geometry or a fixed lighting direction.
- `ArmJointGraphics.DrawSprites` samples two cubic curves meeting at the computed elbow, shares their direction there, and varies the mesh width along the samples. `InitiateSprites` creates layered round joint sprites and `ApplyPalette` colors those layers. REA managed evidence: `ev_a7d59887278f387e08c0eff2d9ec659688959946e432463371f29b3961f84f96`.
- The desktop renderer uses an original tapered mesh with a wide curve on the first span and small fillets on the others. The bearings use a soft two dimensional gradient. The root shell passes through the existing pivots and elbow as the arm folds.
- Detail pass: the separate `ArmBase` supports and the round joint layers were used as references for how the parts read in motion. The desktop carriage has two beveled guide shoes, an open recess, and stays following the root arm. The arm ends meet the gray caps through short sleeves, and the caps sit inside shaded bearing rims. These parts are generated by the desktop renderer.

## Joint and carriage sprite pass (supersedes the generated detail above)

- The joint and base layers are no longer generated geometry. `ArmJointGraphics` and
  `ArmBase` were decompiled from `Assembly-CSharp.dll` and the sprites they name
  (`Circle20`, `deerEyeB`, `MirosLegSmallPart`, `pixel`) were cropped out of the shipped
  `rainWorld` atlas into `Assets/Resources/IteratorArm/`. See
  [rain-world-arm-joint-base.md](rain-world-arm-joint-base.md) for the atlas rects, the
  per-sprite placement numbers, the restoration code and the verification frames.
- The desktop guide shoes, bearing rims and sleeves were removed; the carriage is now the
  `ArmBase` bracket plus its two `pixel` piston supports and `Circle20` side discs.

## Puppet-side attachment

- In the supplied footage, the short terminal span disappears under the torso when entering from the side at `00:12.12` and from below at `00:50.16`. The inside of the attachment is occluded. In the inspected build, `ArmJointGraphics.DrawSprites` interpolates `bodyChunks[1]` as the final endpoint. `OracleGraphics` allocates the arm sprite range before the body and robe and adds those sprites in order to the midground. REA managed evidence: `ev_b2c62321a0675733e1c04e0de98215f90967a6717181ed20d54ad0fc115bba68`.
- In Unity, `ArmBackAttachment` is a child of the animated torso at the lower back. Both drag reach and the arm solve use that point. The last span draws behind the robe, which hides the actual endpoint. The former side cuff and extra tip disc were removed.

## Carriage pivot and support hinges

- In the supplied Look to the Moon footage (game version unverified), the plate and root of the first span meet at the lower edge at `00:50.16`. At `02:30.20` the narrow supports can be seen beside the root. Their individual pins are partly obscured.
- `OracleGraphics.ArmBase.DrawSprites` computes the plate origin from the root joint and the rail normal. It places the feet on either side, targets a point one quarter of the way toward the first elbow, and solves the two knees on opposite sides. REA managed evidence: `ev_65b90c859ea28b1d443d69010fda063da7534d0b36229dc17a14104794d6c60c`.
- Unity keeps the rail contact at the viewport edge and places the pivot inside the plate. The arm mesh begins at that pin and both hinge feet sit on the plate. Their shared target follows the first cubic shell. Each hinge keeps a constant outer link length and adjusts its thinner inner link only when the desktop pose falls outside the nominal travel.

## Main umbilical cable

- In the supplied Looks to the Moon room footage, the dark banded cable rises from near the middle of the floor at `00:22.00`, `00:34.00`, and `00:42.00`. Its exit stays fixed while the carriage moves between walls. The large return curves change sides and occasionally cross in projection. The last join is partly hidden by the arm and head effects. The footage's exact game build is unverified.
- In the installed managed artifact, `OracleGraphics.UbilicalCord` has 80 main samples with 10 px between neighbours, for 790 px of nominal length. `Update` integrates velocity with damping, subtracts room gravity, and applies a slight upward bias strongest near the floor. `SetStuckSegments` pins sample zero to a tile (default `24,2`, separate branches in `RM` and `CL`). Around sample 60, it guides the cable off the span from arm joint 1 to its elbow. The last main sample is kept within reach of the first body chunk. The fine cords bridge from the last main sample to the head.
- `InitiateSprites` creates a long mesh for the core and two differently scaled `CentipedeSegment` sprites per main sample. `DrawSprites` orients them across the local cable direction. `ApplyPalette` colors the core with the arm metal and tints the two overlays separately. REA managed evidence `ev_5a5209396684a8cbec94fc4dc1860a0c23c66b608148fd57a118a43dc17928e2`.
- The desktop exit sits at 52% of the viewport width along the floor. The trunk has a short upright outlet, a mechanical guide, and a movable receiver beside the head. The viewport determines the resting length once so the cable supplies the full arm travel. Fixed-step integration and bend constraints carry the slack through large folds. The jacket and repeated collars use an original vertex-colored mesh.

## Fine cords behind the head

- The supplied Looks to the Moon room footage (build unverified) shows a colored bundle gathering behind the head at `00:22.00` and `00:34.00`. At `00:42.00` the glow partly hides the junction. Individual strands cannot be counted from those frames. The long luminous line leaving the halo is distinct from the short physical bundle.
- `OracleGraphics.UbilicalCord` allocates 14 fine cords with 20 points each. The length parameter is selected once per cord, roughly half in the 50–65 px range and half in the 50–200 px range, biased toward the shorter end in the second range. The distance constraint uses that parameter divided by 20 for each adjacent pair.
- `Update` pins point zero to the final trunk point and point 19 to the animated head position. The first few interior points receive a bias along the trunk exit tangent. The last two interior points receive a bias opposing `lookDir` plus a per-cord random direction. `InitiateSprites` creates one `TriangleMesh.MakeLongMesh` per cord, using the utility white region. `DrawSprites` uses a 0.5 px half-width. `ApplyPalette` chooses metal, red mixed with metal, or blue mixed with metal. REA managed evidence `ev_6125190f966a5d6dfb599a29f84bce72df07f9c0ea965491ca4a89470f624480`.
- In the `00:33.20–00:34.80` sequence, the bundle moves while both ends remain gathered. The head glow obscures individual trajectories. The inspected `Update` integrates each point's velocity, maps speeds of 2–6 px per game step to damping of 0.999–0.9, and subtracts `Room.gravity * 0.9` from its vertical velocity.
- The desktop uses evenly distributed quantiles from each of the two overlapping source ranges. The long distribution starts at 50 px as in the short distribution and uses the source power curve. The nominal arc length uses 19 intervals at one twentieth of the length parameter for a 20-point cord. The footage's individual random draw is unknown.
- The desktop solver advances the 20 points of each cord at 120 Hz in the desktop's weightless frame. It accumulates momentum, damps faster movement, applies the exit and head-direction biases near the two moving sockets, and projects distances between neighbours. A coupled distance solve removes residual stretch after the local passes. The trunk tip follows within the reach of the shortest fine cord, and both fine-cord endpoints are pinned even on render frames between ticks.
- Rendering uses narrow untextured triangle strips with a flat color per cord. The strips join continuously at the simulated points and draw behind the head.

