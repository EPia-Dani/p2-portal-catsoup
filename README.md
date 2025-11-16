# Portal Game - Unity Project

A Portal-inspired puzzle game built in Unity using the Universal Render Pipeline (URP). This project features a fully functional portal system with recursive rendering, physics-based teleportation, interactive objects, and optimized performance.

## 🎮 Overview

This is a first-person puzzle game where players use a portal gun to create blue and orange portals on designated surfaces. Players can walk through portals, throw objects through them, and solve puzzles using portal mechanics.

## 🏗️ Project Structure

```
Assets/
├── Scripts/
│   ├── Portal/          # Core portal system
│   ├── Player/          # Player controller and management
│   ├── Interact/        # Interactive objects (buttons, doors, elevators)
│   ├── Enemy/           # Enemy AI (turrets)
│   ├── UI/              # User interface systems
│   ├── Infrastructure/  # Game management and utilities
│   └── RefractionCubes/ # Special puzzle objects
├── Scenes/              # Game scenes (MainMenu, IntroScene, FinalLevel)
├── PortalAssets/        # Portal-specific 3D models and materials
└── Resources/           # Shaders, materials, sounds
```

## 🔑 Core Systems

### 1. Portal System

The portal system is the heart of this project, consisting of several interconnected components:

#### Portal Rendering (`PortalRenderer.cs`)
- **Recursive Rendering**: Portals can render portals within portals up to a configurable recursion limit (default: 2 levels)
- **Render Texture System**: Each portal uses a dedicated camera and render texture to display the view through its paired portal
- **Oblique Projection**: Uses oblique projection matrices to clip geometry at the portal plane, preventing objects from appearing behind portals
- **Frustum Culling**: Intelligent culling reduces unnecessary recursion when portals aren't visible
- **Performance Optimizations**:
  - Matrix caching to avoid redundant calculations
  - Adaptive recursion based on portal alignment (portals facing same direction = no recursion)
  - Visibility culling before rendering

**Key Components:**
- `PortalRenderer`: Main rendering component
- `PortalRenderTextureController`: Manages render textures
- `PortalViewChain`: Builds recursive view matrices
- `PortalVisibilityCuller`: Determines if portal should render

#### Portal Placement (`PortalGun.cs`, `PortalPlacement.cs`)
- **Surface Detection**: Raycasts to find valid portal surfaces (tagged as "Portal Wall")
- **Placement Validation**: Ensures portals don't overlap and are placed on valid surfaces
- **Dynamic Sizing**: Portals can be resized using mouse scroll (min: 0.2x, max: 1.0x)
- **Visual Feedback**: Shows placement bounds preview before shooting
- **Overlap Prevention**: Prevents placing portals too close to each other

**Key Components:**
- `PortalGun`: Handles shooting mechanics and placement logic
- `PortalPlacementCalculator`: Calculates valid placement positions
- `PortalOverlapGuard`: Prevents portal overlap
- `PortalSizeController`: Manages portal scaling

#### Portal Teleportation (`PortalTravellerHandler.cs`, `PortalTraveller.cs`)
- **Universal Velocity Transformation**: Transforms velocity vectors through portals using quaternion rotation
  - Includes 180° flip to convert "entering" to "exiting"
  - Preserves momentum and angular velocity
  - Handles scale differences between portals
- **Collision Management**: Temporarily disables collision with destination wall during teleport
- **Minimum Exit Velocity**: Prevents objects from getting stuck when teleporting from floor/ceiling portals to wall portals
- **Clone System**: Creates visual clones of held objects on the other side of portals

**Key Components:**
- `PortalTraveller`: Base class for objects that can travel through portals
- `PortalTravellerHandler`: Detects when objects cross portal boundaries
- `PortalTransformUtility`: Mathematical utilities for portal transformations
- `PortalCloneSystem`: Creates and manages clones for held objects

### 2. Player System

#### FPS Controller (`FPSController.cs`)
- **Movement**: Custom physics-based movement with acceleration and friction
  - Ground movement with configurable acceleration/friction
  - Air control for mid-air movement
  - Sprint multiplier for faster movement
  - Terminal velocity to prevent physics breaking
- **Portal Integration**: Extends `PortalTraveller` for seamless portal travel
  - Velocity transformation through portals
  - Camera rotation preservation
  - Smooth teleportation with momentum conservation
- **Health System**: 
  - 3 hit points (configurable)
  - Invulnerability frames after taking damage
  - Auto-heal after 10 seconds
  - Camera shake feedback on hit
  - Death handling with respawn
- **Surface Physics**: Responds to different surface types
  - Sliding surfaces (ice)
  - Bouncy surfaces
  - Destructive surfaces
  - Normal surfaces

#### Player Management (`PlayerManager.cs`)
- Singleton pattern for global player state
- Handles respawn and checkpoint systems
- Manages player death and respawn logic

### 3. Interaction System

#### Interactable Objects (`InteractableObject.cs`)
- **Pickup System**: Players can pick up and carry objects
  - Physics-based movement when held
  - Momentum preservation when dropped
  - Portal clone system integration
- **Portal Travel**: Objects can travel through portals
  - Velocity transformation
  - Scale preservation
  - Clone swapping for held objects
- **Surface Physics**: Objects respond to different surface types

#### Interactive Elements
- **Buttons** (`Button.cs`): Activates when pressed by player or objects
- **Doors** (`Door.cs`): Opens/closes based on button state
- **Elevators** (`Elevator.cs`): Moves between floors
- **Radios** (`Radio.cs`): Special interactable objects for puzzles
- **Cube Droppers** (`CubeDropper.cs`): Spawns cubes for puzzles

### 4. Enemy System

#### Turrets (`Turret.cs`)
- **AI Behavior**: Detects and shoots at player
- **Projectile System**: Fires projectiles that damage player
- **Portal Awareness**: Can be disabled by placing portals in front of them
- **Health System**: Can be destroyed by player actions

### 5. UI System

- **Crosshair** (`Crosshair.cs`): Shows portal placement feedback
- **Damage Overlay** (`DamageOverlay.cs`): Visual feedback when player takes damage
- **Death Screen** (`DeathScreenManager.cs`): Handles death UI
- **Screen Fade** (`ScreenFade.cs`): Smooth scene transitions
- **Menu System**: Main menu, settings, credits

### 6. Infrastructure

#### Game Bootstrap (`GameBootstrap.cs`)
- Initializes input system
- Sets up cursor state
- Ensures proper initialization order

#### Input System (`InputManager.cs`)
- Uses Unity's new Input System
- Singleton pattern for global access
- Handles player input (movement, shooting, interaction)

#### Scene Management (`SceneManager.cs`)
- Scene loading with fade transitions
- Scene reloading for respawn
- Main menu navigation

## 🎯 Key Technical Features

### Portal Rendering Pipeline

1. **Render Setup**: Each portal has a dedicated camera and render texture
2. **View Chain Building**: Calculates camera positions/orientations for each recursion level
3. **Recursive Rendering**: Renders from deepest level to surface (back-to-front)
4. **Oblique Clipping**: Uses oblique projection to clip geometry at portal plane
5. **Texture Binding**: Render texture is applied to portal surface material

### Velocity Transformation

The system uses a universal velocity transformation algorithm:

```csharp
// 1. Capture velocity before teleport
Vector3 velocity = currentVelocity;

// 2. Scale based on portal size difference
velocity *= scaleRatio;

// 3. Transform through portal rotation (with 180° flip)
Quaternion flipLocal = Quaternion.AngleAxis(180f, Vector3.up);
Quaternion relativeRotation = toPortal.rotation * flipLocal * Quaternion.Inverse(fromPortal.rotation);
velocity = relativeRotation * velocity;

// 4. Apply minimum exit velocity if needed
velocity = ApplyMinimumExitVelocity(fromPortal, toPortal, velocity);
```

This ensures:
- Momentum is preserved through portals
- Objects maintain correct trajectory
- No velocity loss during teleportation
- Smooth transitions between portals

### Clone System

When a player holds an object near a portal:
1. A visual clone appears on the other side
2. Clone follows player movement
3. When player teleports or drops object, clone swaps with real object
4. Prevents objects from disappearing during portal travel

### Performance Optimizations

Based on profiling, the system includes several optimizations:

1. **GPU Optimizations** (URP Settings):
   - Disabled shadows (major GPU bottleneck)
   - Reduced light count to 1
   - Disabled reflection probes
   - Reduced LUT size
   - Result: 90 FPS → 180-200+ FPS (2-2.5x improvement)

2. **CPU Optimizations**:
   - Matrix caching in `PortalRenderer`
   - Frustum culling before rendering
   - Adaptive recursion based on portal alignment
   - Visibility culling for off-screen portals

3. **Rendering Optimizations**:
   - Configurable texture size (default: 1024x1024)
   - Configurable recursion limit (default: 2)
   - Early exit when portals aren't visible

## 🎨 Assets and Resources

- **Portal Models**: Portal gun, portal surfaces, test chamber assets
- **Materials**: Portal shaders, surface materials
- **Audio**: FMOD integration for sound effects
- **UI**: SlimUI package for modern UI elements

## 🚀 Getting Started

### Prerequisites
- Unity 2021.3 or later
- Universal Render Pipeline (URP)
- FMOD (for audio)

### Setup
1. Open project in Unity
2. Ensure URP is configured in Project Settings
3. Check that Input System is enabled
4. Load `MainMenu` scene to start

### Controls
- **WASD**: Move
- **Mouse**: Look around
- **Left Click**: Shoot blue portal
- **Right Click**: Shoot orange portal
- **Scroll Wheel**: Resize portal
- **E**: Interact/Pick up objects
- **Shift**: Sprint
- **Space**: Jump

## 📝 Code Architecture

### Namespaces
- `Portal`: Portal system components
- `Input`: Input system wrapper
- `UI`: UI components

### Design Patterns
- **Singleton**: `InputManager`, `PlayerManager`, `ScreenFadeManager`
- **Component Pattern**: Modular components for portal system
- **Observer Pattern**: Portal events and notifications
- **Strategy Pattern**: Different surface physics behaviors

### Key Classes

**Portal System:**
- `PortalManager`: Central portal management
- `PortalRenderer`: Rendering logic
- `PortalGun`: Placement and shooting
- `PortalTraveller`: Base class for portal travel

**Player System:**
- `FPSController`: Player movement and control
- `PlayerPickup`: Object interaction
- `PlayerManager`: Global player state

**Interaction:**
- `InteractableObject`: Base class for interactive objects
- `Button`, `Door`, `Elevator`: Specific interactables

## 🔧 Configuration

### Portal Settings (`PortalManager`)
- `textureSize`: Render texture resolution (256-4096)
- `recursionLimit`: Maximum recursion depth (1+)
- Portal colors and materials

### Player Settings (`FPSController`)
- Movement speed and acceleration
- Jump force and gravity
- Health and invulnerability
- Mouse sensitivity

### Performance Settings
- Adjust texture size for performance vs quality
- Reduce recursion limit for lower-end hardware
- Enable/disable various optimizations

## 🐛 Known Issues / Limitations

1. **Portal Recursion**: Limited to 2 levels by default (can be increased but impacts performance)
2. **Portal Size**: Portals can be resized but must maintain aspect ratio
3. **Surface Requirements**: Portals can only be placed on surfaces tagged "Portal Wall"
4. **Clone System**: Only works for held objects, not all portal travelers

## 📚 Further Reading

For more details on specific systems, see:
- `Assets/Scripts/Portal/` - Portal system implementation
- `Assets/Scripts/Player/` - Player controller
- `Assets/Scripts/Interact/` - Interaction system

## 🎓 Learning Resources

This project demonstrates:
- Advanced rendering techniques (recursive portals, oblique projection)
- Physics-based movement systems
- Complex spatial transformations
- Performance optimization strategies
- Unity URP integration
- Modern C# patterns and practices

## 📄 License

This project is for educational purposes.

---

**Note**: This is a complex system with many interconnected components. When modifying code, be aware of dependencies between systems, especially the portal rendering and teleportation systems.
