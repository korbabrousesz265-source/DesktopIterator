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

