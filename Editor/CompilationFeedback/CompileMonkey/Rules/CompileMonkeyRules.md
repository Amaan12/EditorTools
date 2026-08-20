# CompileMonkey — Unity Editor Compilation Overlay

Create a Unity Editor package called **CompileMonkey** i.e. the current folder

## Goal

Transform Unity compilation into an entertaining experience by displaying a fullscreen Scene View overlay featuring CodeMonkey (Hugo) and playing audio during compilation.

This is an **Editor-only package**. It must not create GameObjects, Canvas objects, prefabs, scene objects, or runtime components.

Everything should be implemented using Unity Editor APIs.

---

## Features

### Compilation Started

When Unity starts compiling:

* Detect compilation start using Editor APIs.
* Play looping compilation music.
* Display a fullscreen overlay over the Scene View.
* Overlay should completely obscure the Scene View.

Overlay appearance:

* Semi-transparent or solid black background.
* Large centered CodeMonkey image ("Compiling" state).
* Optional animated text:

  * "Compiling..."
  * animated dots
  * spinner animation

The overlay should remain visible while Unity is compiling.

---

### Compilation Finished

When compilation ends:

* Stop compilation music.
* Play a completion SFX.

Determine compilation result:

#### Error State

If compilation produced errors:

* Show Error CodeMonkey image.
* Display error count.
* Red tint or red accent.
* Remain visible for configurable duration.

#### Warning State

If compilation produced warnings but no errors:

* Show Warning CodeMonkey image.
* Display warning count.
* Yellow accent.
* Remain visible for configurable duration.

#### Success State

If compilation completed with no errors or warnings:

* Show Success CodeMonkey image.
* Display success message.
* Green accent.
* Remain visible for configurable duration.

---

### Fade Out

After the result state is displayed:

* Fade overlay alpha from 1 → 0.
* Fade duration configurable.
* Default:

  * Display duration: 2 seconds
  * Fade duration: 0.5 seconds

When fade completes:

* Hide overlay entirely.
* Restore normal Scene View.

---

## Technical Requirements

### Editor Only

All code must be inside an Editor assembly.

No:

* MonoBehaviours
* Prefabs
* Runtime UI
* Runtime Canvases
* Scene objects

---

### Scene View Overlay

Use:

```csharp
SceneView.duringSceneGui
```

Draw using:

```csharp
Handles.BeginGUI();
Handles.EndGUI();
```

Render:

* fullscreen background
* centered image
* status text
* fade effects

Overlay must be drawn above Scene View rendering.

---

### Compilation Detection

Use Unity compilation callbacks:

```csharp
CompilationPipeline.compilationStarted
CompilationPipeline.compilationFinished
```

or equivalent editor-safe mechanisms.

---

### Audio

Play audio entirely from editor code.

Support:

* looping compilation music
* completion SFX

Use editor-safe audio playback APIs.

---

### Asset Management

Create a ScriptableObject:

```csharp
CompileMonkeySettings
```

Fields:

```csharp
Texture2D compilingImage;
Texture2D successImage;
Texture2D warningImage;
Texture2D errorImage;

AudioClip compileMusic;
AudioClip successSfx;
AudioClip warningSfx;
AudioClip errorSfx;

float resultDisplayTime;
float fadeDuration;
```

The package should automatically locate the settings asset using:

```csharp
AssetDatabase.FindAssets("t:CompileMonkeySettings")
```

Avoid hardcoded asset paths.

---

## State Machine

```text
Idle
  ↓
Compiling
  ↓
Success
  ↓
FadeOut
  ↓
Idle
```

```text
Idle
  ↓
Compiling
  ↓
Warning
  ↓
FadeOut
  ↓
Idle
```

```text
Idle
  ↓
Compiling
  ↓
Error
  ↓
FadeOut
  ↓
Idle
```

Use an enum:

```csharp
enum CompileMonkeyState
{
    Idle,
    Compiling,
    Success,
    Warning,
    Error,
    Fading
}
```

---

## Desired User Experience

Compilation begins:

* Scene View immediately darkens.
* Large CodeMonkey image appears.
* Music starts.

Compilation succeeds:

* Success image appears.
* Victory SFX plays.
* Overlay remains for 2 seconds.
* Overlay fades away.

Compilation has warnings:

* Warning image appears.
* Warning SFX plays.
* Overlay fades away after delay.

Compilation fails:

* Error image appears.
* Error count displayed.
* Error SFX plays.
* Overlay fades away after delay.

The package should feel like a polished streamer-style reaction overlay integrated directly into the Unity Editor.
