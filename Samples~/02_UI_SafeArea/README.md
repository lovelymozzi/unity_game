# 02 UI — SafeArea

Demonstrates `SafeAreaFitter` on a panel.

## Run
1. Import the sample from Package Manager.
2. Open `SafeAreaScene.unity`.
3. Press Play. In Game view, switch resolution to "iPhone 14 Pro" or "Pixel 7" — panel respects notch.

## What to verify
- Panel resizes when changing aspect/notch device in Game view dropdown.
- Rotating between Portrait/Landscape (in Game view) updates anchors.

## Combine with LetterboxController
Attach `LetterboxController` to the Canvas (set `enableLetterbox = true`) to maintain reference aspect at any window size. SafeAreaFitter + LetterboxController stack cleanly.
