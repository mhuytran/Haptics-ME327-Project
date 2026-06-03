**About our project**

For our ME327 project, our Unity rhythm game titled "trum-pal triumph" was developed. This repository consists of the Unity code and the Arduino code that is flashed onto the Teensy 
microcontroller that enables it to communicate with the Unity code. The key functions of the rhythm game include trumpet valve timing, visual note motion, and Teensy-driven haptic hardware. 

The core flow of scripts used in the Unity game is as follows:

1. `NoteSpawner` creates notes from either random trumpet fingerings or a JSON song chart.
2. `FlyingNote` moves each note from its spawn point to the valve target using time-based interpolation.
3. `TeensySerialInput` reads live valve distance data from the Teensy and converts it into stable valve press states.
4. `RhythmGameManager` compares the player’s current valve mask against each note’s required fingering and timing window.
5. `HapticFeedbackManager` sends haptic commands back to the Teensy for pre-cues, successful hits, holds, misses, and emergency shutdowns.
6. `TrumpetValveAnimator`, `NoteGlowPulse`, and UI scripts make the interaction visible through valve movement, glow effects, smash effects, score, health, combo, and game-over state.

**Animation System**

The moving notes are handled mainly by [FlyingNote.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/FlyingNote.cs). Each note stores a spawn position, target position, spawn time, and target hit time. Every frame it computes:

```csharp
travelFraction = Mathf.InverseLerp(spawnTime, targetHitTime, Time.time)
```

Then it moves the note with `Vector3.Lerp`. This makes note travel independent of frame rate and ties the animation directly to rhythm timing.

Valve animation is handled by [TrumpetValveAnimator.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/TrumpetValveAnimator.cs). It reads a normalized press amount from `ValveInputState`, filters noise with dead zones and debounce timers, then moves the valve from its rest position toward a pressed offset using `Mathf.MoveTowards`. This creates smooth mechanical motion while preventing ToF sensor jitter from making the valve visually twitch.

Hit effects are handled by [NoteGlowPulse.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/NoteGlowPulse.cs). It creates runtime materials for note glow and generates flat ring/streak meshes for tap hits, hold hits, and hold completion bursts.

**Gameplay Algorithms**

The main scoring algorithm is in [RhythmGameManager.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/RhythmGameManager.cs). It uses a bitmask for valve fingerings:

- Valve 1: bit `1 << 0`
- Valve 2: bit `1 << 1`
- Valve 3: bit `1 << 2`

When the player presses a valve, the manager compares the current valve mask against active notes. It picks the matching note with the smallest timing error. If the timing error is within the `perfectWindow`, the hit is perfect. If it is within the `goodWindow`, the hit is good. If the note passes the `missWindow`, it becomes a miss.

Multi-valve notes are grouped with `fingeringGroupId`, so a fingering like valves `1+2` spawns multiple lane visuals but resolves as one musical event. Hold notes start on a correct press, require the fingering to remain held, and complete after `targetHitTime + holdDuration`.

The combo multiplier doubles at expanding thresholds: it starts at `1x`, then increases after the combo reaches the configured base threshold, with each later threshold doubling. Health decreases on misses and can recover during low-health combo play.

**Teensy Communication and Integration**

The Teensy bridge is mainly [TeensySerialInput.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/TeensySerialInput.cs), [HapticFeedbackManager.cs](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/Scripts/HapticFeedbackManager.cs), and [trumpal_final_code.ino](c:/Users/huyjo/Folder/Documents/Haptics%20327%20Unity%20Code/My%20project/Assets/trumpal_final_code/trumpal_final_code.ino).

Unity opens a serial connection at `115200`, auto-detects COM ports, and reads Teensy messages on a background thread. The Teensy sends comma-separated telemetry like:

```text
V1:0,V2:1,V3:0,D1:80,D2:62,D3:79,TC1:0,TC2:6,TC3:7,S1:0.00,E1:1.00
```

Unity parses this into valve states, raw ToF distances, mux channels, solenoid duty, and ERM duty. To make the hardware feel seamless, the Unity side does the important input conditioning:

- Startup ToF auto-calibration samples released valve distances.
- Press thresholds are derived from rest distance minus a configurable delta.
- Hysteresis separates press and release thresholds.
- Debounce timers require a state to remain stable before Unity accepts it.
- Out-of-range calibration can be rejected.
- Live re-baselining can repair stale rest-distance values during play.
- A post-calibration guard prevents startup sensor settling from creating false presses.

Outgoing commands are filtered and formatted by `HapticFeedbackManager`. Gameplay uses:

```text
PRECUE,lane
TAPCOMPLETE,lane,PERFECT|GOOD
HOLDSTART,lane
HOLDCOMPLETE,lane
MISS,lane
X
```

The Teensy firmware keeps actuator behavior non-blocking. ERM ramps, solenoid pulses, timed auto-off, ToF reads, and serial command parsing all run through `millis()` state checks rather than blocking delays. That is what lets the game continue receiving sensor telemetry while haptic feedback is happening.

**Summary of What Each C# Script in Unity does**

- `RhythmGameManager.cs`: Core scoring, timing windows, combo, health, note resolution, game over, and haptic event dispatch.
- `NoteSpawner.cs`: Spawns random fingerings or song-chart notes, syncs chart timing to audio, maps trumpet pitches to valve masks.
- `FlyingNote.cs`: Moves notes toward valve targets, handles pre-cues, tap/hold resolution, misses, and hold tails.
- `TeensySerialInput.cs`: Serial connection manager, Teensy telemetry parser, ToF calibration, debounce, valve state generation.
- `HapticFeedbackManager.cs`: Sends safe command strings to Teensy for gameplay haptics and bench tuning.
- `ValveInputState.cs`: Shared static input state for keyboard, Teensy, debug valve amounts, distances, solenoid duty, and ERM duty.
- `TrumpetValveAnimator.cs`: Animates trumpet valve transforms from stable input state.
- `TeensyHardwarePinout.cs`: Defines Unity lane to Teensy channel, mux, solenoid, and ERM pin mapping.
- `SolenoidPulseSettings.cs`: Stores clamped solenoid duty, phase, and duration settings.
- `UnitySolenoidTester.cs`: Inspector/keyboard haptic testing and tuning helper.
- `KeyValveInput.cs`: Maps keyboard `A/S/D` to valves 1/2/3.
- `GameplayUIFeedback.cs`: Score, health, combo, power overlay, feedback text, and game-over UI.
- `NoteGlowPulse.cs`: Note glow plus tap/hold smash mesh effects.
- `NoteDiscVisual.cs`: Keeps note discs camera-facing and visually consistent in size.
- `TrumpetLaneFourDividers.cs`: Draws lane divider lines between spawn points and valve targets.
- `HomeScreenManager.cs`: Starts scenes and renders leaderboard rows.
- `HomeTrumpetDancer.cs`: Optional home-screen valve animation pattern.
- `LeaderboardStore.cs`: Saves leaderboard data and CSV attempt logs.
- `GameOverLeaderboardSubmitter.cs`: Handles player name entry and score submission.
- `EmergencyAbortController.cs`: Ctrl+Shift+X shutdown, hardware all-off, abort flag, return home.
- `GameAbortState.cs`: Tracks whether the current run was emergency-aborted.
- `DebuggingSceneManager.cs`: Password-gated reset/debug scene logic.
- `ValveRenderRepair.cs`: Repairs missing/disabled valve renderers at runtime.
- `ButtonHoverGlow.cs`: Button hover, disabled-state, and scale animation.
- `LeaderboardRowShine.cs`: Medal shine/glow effects for top leaderboard rows.
- `SilverTextShine.cs`: Animated silver text material effect.
- `TitleGoldGlowWave.cs`: Animated title shine/glow/scale effect.
- `TMPInputPlaceholderOnFocus.cs`: Hides/shows TMP input placeholder on focus.
- `ConstantScreenSize.cs`: Scales objects by camera distance.
- `PerspectiveFixedScreenSize.cs`: Maintains fixed projected screen size for perspective objects.
- `PowerMarginLayout.cs`: Positions power overlay border images.
- Editor scripts: Add custom inspector buttons/help for haptics, note spawning, and Teensy calibration.
- `trumpal_final_code.ino`: Final Teensy firmware for ToF telemetry, ERM pre-cues, solenoid push-off, tuning commands, and serial protocol.
- `trumpal_teensy_code.ino`: Teensy firmware copy/variant with the same Unity-facing haptic and ToF command surface.
- `i2c_scanner.ino`: Standalone Teensy I2C scanner for finding the TCA9548A mux and VL6180X sensors.
