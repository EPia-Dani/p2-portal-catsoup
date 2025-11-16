# 🌀 **Portal Game**

*A Portal-inspired puzzle game built in Unity*

## 👥 **Development Team**

- **Your Name Here**

## 🌲 **Game Description**

You are a test subject equipped with a portal gun. Create blue and orange portals on designated surfaces to navigate through test chambers, solve puzzles, and survive hostile turrets.

Use portals to traverse impossible distances, redirect objects, and manipulate the environment to reach your goal.

> *"Now you're thinking with portals."*

## ✅ **Requirements Checklist**

### Core Features

| # | Feature | Points | Description | Status |
|---|---------|--------|-------------|--------|
| 1 | **Escenari** | 0.5p | Playable scenario using provided assets, Portal-style puzzle level | ✅ |
| 2 | **Portal Gun** | 1p | Gun that generates portals on valid surfaces only. First mouse button creates blue portal, second creates orange. Preview shows if portal can be placed before releasing button. Portals must be fully placed without being cut or blocked. | ✅ |
| 3 | **Portals** | 1p | Portals display view from complementary portal (window effect). Player perspective changes when moving in front of portal. | ✅ |
| 4 | **Teleport** | 0.5p | Player can enter and teleport through portals. Exit direction depends on entry direction (diagonal entry = diagonal exit in same relative direction). | ✅ |
| 5 | **Companion Cubes** | 1p | Cube dispenser activated by button. Cubes activate buttons when placed on them. | ✅ |
| 6 | **Gravity Gun** | 1p | Can pick up cubes by clicking from distance. Picked-up cubes float in front of weapon. Click again to shoot cube. Second mouse button drops cube to ground. | ✅ |
| 7 | **Teleport Cubes** | 0.5p | Cubes thrown into portal teleport and exit from other side with same position, direction, and velocity as expected. | ✅ |
| 8 | **Resizing** | 0.5p | Blue portal preview can be resized with mouse wheel (50% to 200% of normal size). Cubes teleporting between portals adapt their size proportionally (50% portal = 50% cube size, 200% portal = 200% cube size). | ✅ |
| 9 | **Turrets** | 1p | Enemy turrets shoot red laser that kills player instantly on contact. Turrets deactivate if hit by cube or another turret. Turrets can be picked up with gravity gun (laser shoots forward when held). Turrets die if hit by another turret's laser. | ✅ |

**Total Core Points: 6.5p**

### 💀 **BONUS POINTS** (Maximum 3 points)

*Only evaluated if all core features are fully implemented.*

| Feature | Description | Status |
|---------|-------------|--------|
| 🚪 **Doors / Keys** | Doors between rooms that only open when cube is placed on button | ✅ |
| 🌋 **Dead Zones** | Lava zones in scenario that kill player automatically on contact | ✅ |
| 🏀 **Physic Surfaces** | Surfaces with different physics behaviors: | ✅ |
| &nbsp;&nbsp;&nbsp;• **Bouncing** | Cubes/turrets bounce when touching surface | ✅ |
| &nbsp;&nbsp;&nbsp;• **Sliding** | Cubes/turrets slide like ice when touching surface | ✅ |
| &nbsp;&nbsp;&nbsp;• **Destroying** | Cubes/turrets are destroyed when touching surface | ✅ |
| ☠️ **Game Over / Retry** | Game Over screen appears when player dies, option to retry | ✅ |
| 🧭 **Checkpoint** | Checkpoints allow player to continue from checkpoint if they die | ✅ |
| 🔊 **Sound** | Game sound design: gravity gun, doors, portal creation, checkpoints, buttons, death, turrets, etc. | ✅ |
| 🔷 **Refraction Cube** | Lasers in scene can be redirected by refraction cube. Reflected laser reaches switch to open door. | ✅ |
| 🛡️ **Blocking Cube** | Companion cubes block lasers | ✅ |
| 🎯 **Crosshair** | Mouse cursor shows portal color if portal is created, empty if not yet placed | ✅ |

## ⭐ **EXTRA FEATURES** (Beyond Requirements)

Features implemented that weren't required but enhance the game experience:

| Feature | Description |
|---------|-------------|
| 🔊 **FMOD Audio Integration** | Full FMOD Studio integration for professional spatial audio. Portal warp sounds, interactive sound effects (buttons, doors, objects), ambient audio, and dynamic audio events. Radio minigame countdown system with timed audio cues. |
| 💥 **Mesh Destruction System** | Objects break into fragments when destroyed. Supports both prefab-based fractures and runtime-generated primitive fragments with explosion forces and velocity inheritance. |
| 🎬 **Credits Scene** | Full credits scene with scrolling text, video timeline system with timed clips and subtitles, audio fade, and smooth transitions. |
| 🏛️ **Chamber 00 Recreation** | 1:1 recreation of Portal's Test Chamber 00 using IntroAnimation system. Preset portal placement, automatic door opening, and authentic intro sequence. |
| 📻 **Radio Minigame** | Radio counter system in FinalLevel tracks destroyed radios (target: 10). Plays countdown audio ("9 to go", "8 to go", etc.) and final completion message. Automatically loads next scene when target reached. |
| 📏 **Player/Object Resizing** | Both player and objects resize proportionally when passing through portals of different sizes. Player scale affects hold distance, movement, and all interactions. |
| ⚡ **Momentum Preservation** | Full velocity transformation system preserves momentum, angular velocity, and relative orientation through portals. Objects maintain their trajectory and speed. |
| 🏢 **Elevator System** | Functional elevator component that moves between floors, activated by buttons or triggers. |
| 🩸 **Damage Overlay** | Visual red flash overlay when player takes damage with configurable duration and alpha. Provides immediate feedback. |
| 🎭 **Screen Fade System** | Smooth fade in/out transitions between scenes. Singleton system with configurable fade duration and color. Works with scene loading and respawn. |
| ✨ **Portal Animations** | Portal appear/disappear animations with scaling effects. Portals animate from 0 to target size when placed, with configurable animation curves. |
| 👥 **Clone System** | Visual clones of held objects appear on the other side of portals. Clones swap with real objects when player teleports or drops objects. |
| ❤️ **Health System** | Player health system (3 HP) with invulnerability frames after taking damage, auto-heal after delay, and death handling with respawn. |
| 📳 **Camera Shake** | Camera vibration feedback when player takes damage. Configurable duration and magnitude for impact feedback. |
| 🧊 **Surface Physics Integration** | Player controller responds to different surface types: sliding (ice), bouncy (enhanced jump), and destructive surfaces. |
| 🎨 **Portal Gun Bobbing** | Subtle vertical and rotational bobbing animation for portal gun when held, adds life to the weapon. |
| 🔄 **Portal Overlap Prevention** | Smart system prevents portals from being placed too close to each other on the same surface, with visual feedback. |
| 🎯 **Dynamic Portal Bounds** | Portal placement bounds visualization shows preview ellipse before shooting. Small portals skip bounds validation for flexible placement. |

## 🔥 **Technical Tricks & Optimizations**

### Portal Rendering Optimizations

**Frustum Culling:**
- Per-level frustum culling checks if next portal in recursion chain is visible
- Uses expanded bounds (1.2x) and multiple fallback checks:
  - Standard AABB test
  - Portal center check
  - Corner visibility check for close portals
- Early exit when deeper recursion levels aren't visible

**Recursion Logic:**
```csharp
// Hardcoded recursion limits based on portal angle
float dot = Vector3.Dot(portal1.forward, portal2.forward);
if (dot > 0.95f) return 1;        // Same direction: no recursion
if (Mathf.Abs(dot) < 0.3f) return 2;  // 90°: max 2 levels
return fullLimit;                // Opposite: full recursion
```

**Oblique Projection:**
- Calculates clip plane at portal exit
- Uses `CalculateObliqueMatrix()` to clip geometry behind portal
- Prevents rendering artifacts and objects appearing through portals

### Velocity Transformation

**Universal Algorithm:**
```csharp
// 1. Capture velocity before teleport
Vector3 velocity = currentVelocity;

// 2. Scale based on portal size difference
velocity *= scaleRatio;

// 3. Transform with 180° flip (entering → exiting)
Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);
velocity = relativeRotation * velocity;

// 4. Apply minimum exit velocity if needed (non-vertical → vertical)
velocity = ApplyMinimumExitVelocity(fromPortal, toPortal, velocity);
```

**Key Trick:** The 180° flip around portal's local up axis converts "entering" velocity to "exiting" velocity, preserving relative orientation.

### Clone System

**How it works:**
1. When object is held near portal, create visual clone on other side
2. Clone follows player movement using portal transformation math
3. When player teleports or drops object, swap real object with clone
4. Prevents objects from disappearing during portal travel

**Trick:** Uses `PortalTransformUtility.TransformThroughPortal()` to calculate clone position/rotation, ensuring perfect synchronization.

### Surface Physics

**Physics Material System:**
- Automatically creates/updates `PhysicsMaterial` based on surface type
- **Bouncy:** High bounce coefficient with configurable combine mode
- **Sliding:** Low friction coefficient (0-1 range)
- **Destructive:** Destroys cubes on contact with velocity threshold
- Filters by interactable type (radios, cubes, etc.)

**Player Integration:**
- Player controller reads surface physics on collision
- Adjusts friction and acceleration based on surface type
- Enhanced jump on bouncy surfaces

### Performance Optimizations

**GPU Optimizations (URP Settings):**
- Disabled realtime shadows (major GPU bottleneck)
- Reduced light count to 1
- Disabled reflection probes
- Reduced LUT size
- **Result:** 90 FPS → 180-200+ FPS (2-2.5x improvement)

**CPU Optimizations:**
- Matrix caching in `PortalRenderer`
- Frustum culling before rendering
- Adaptive recursion based on portal alignment
- Visibility culling for off-screen portals
- Configurable texture size (256-4096) and recursion limit

### Portal Placement Math

**Surface Math:**
- Projects hit point to surface center using bounds
- Calculates up vector from camera orientation (prevents portals from being upside-down)
- Clamps portal position to surface bounds (with skin offset)
- Small portals (< 0.5x) skip bounds validation for flexible placement

**Overlap Detection:**
- Checks distance between portal centers
- Uses portal half-sizes to calculate minimum separation
- Prevents portals from overlapping on same surface

## 🔊 **Sound & Atmosphere**

- FMOD integration for spatial audio
- Portal warp sounds on teleportation
- Interactive sound effects for buttons, doors, and objects
- Ambient audio for atmosphere

## 🧩 **Technical Overview**

- **Engine:** Unity 3D (URP)
- **Language:** C#
- **Architecture:** Component-based with namespace organization
- **Performance:** Optimized for portal rendering (180-200+ FPS)
- **Key Systems:**
  - Portal rendering with recursive view chains
  - Physics-based movement and interaction
  - Surface physics system
  - Clone system for portal preview

### 🔥 *"The cake is a lie... but the portals are real."*
