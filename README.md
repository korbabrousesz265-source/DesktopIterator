# DesktopIterator

Unity desktop pet project inspired by Five Pebbles (SS) from *Rain World*.

## Project

- Unity Editor: `6000.4.8f1`
- Main scene: `Assets/Scenes/MainScene.unity`
- The first behavior pass covers idle, noticing the cursor, watching it, and returning to idle.
- Attention timing and movement values are editable on `SSOraclePetBrain` and `PuppetAnimator` in the scene.
- The arm and cable pass uses the source's four joint lengths, rounded room frame, 80-point umbilical, and 14 fine cords. See `Docs/rain-world-arm-cable-reconstruction.md` for source evidence and scene limitations.

Open this folder as a Unity project. `MainScene` is enabled in the Windows build settings.
