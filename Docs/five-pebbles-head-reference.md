# Five Pebbles head reference

## Inspected game build

- The locally installed `Assembly-CSharp.dll` has MVID `90dcf104-fbdf-4d42-b147-5089d56af681`. REA managed evidence for `OracleGraphics` is `ev_6554854e5342d8bdb716a36bedfe85b2b52550acf8b04a316f2ea948f01a5764`.
- `SkinColor` returns `(1, 0.4, 0.79607844)` for Pebbles and Saint Pebbles. `ApplyPalette` applies it to the head, chin, neck and body chunks. The side units take metal colors from arm palette methods. The supplied Looks to the Moon video, for example at `00:34.00`, shows the shared layering and movement under a different color branch.
- A [Five Pebbles chamber screenshot](https://steamcommunity.com/workshop/filedetails/?id=3410388115) from the workshop author's gameplay also shows a saturated pink head and neck, pale eyes and gray side units. The screenshot is from a mod demonstration and is used only as a visual check.
- The constructor gives the head body part a 5 px radius. `InitiateSprites` applies `rad / 9` and `rad / 11` to the two axes of the `Circle20` head sprite and `rad / 15` to the chin. For a 20 px source square this is about 11.1 × 9.1 px before transparency and the separate chin. Eyes and neck use the utility `pixel` sprite. The side units combine circle sprites and `LizardScaleA1`.
- `Update` connects the head to a point 6 px above the first body chunk along the body axis, with an 8 px connection radius. `DrawSprites` draws the nominal 3 px-wide neck from the first body chunk to the animated head, rotates it along that line, and uses the current distance for its length. The fine-cord endpoint follows the same animated head position.

## Desktop rendering

- The old circle sprite has a brown tint serialized in `MainScene`. At runtime `PuppetHeadGraphics` replaces the head subtree's sprite rendering with a beveled rose panel, separate gray side housings and pale eyes on the existing gaze and blink transforms. The new shape and palette are independently authored for this desktop puppet.
- The untextured neck mesh follows the torso collar and the head seat every frame. It draws behind the robe. The fine cords retain their head anchor and draw behind the face.
