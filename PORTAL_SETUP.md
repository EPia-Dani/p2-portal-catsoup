# Portal System Setup Guide

## How It Works

Each portal has:
- **A camera** that renders the view FROM the linked portal
- **A render texture** that stores that view
- **A material** that displays the render texture on a quad/plane

When you look through Portal A, you see what Portal B's camera sees (mirrored). When you walk through Portal A, you appear at Portal B.

---

## Step-by-Step Editor Setup

### 1. Create the Portal Material

1. Right-click in your Assets folder → **Create → Material**
2. Name it `Portal_Mat`
3. Select it and in the Inspector:
   - Change **Shader** to `Custom/Portal`

**Note:** The render texture will be assigned automatically at runtime by the `Portal` script.

---

### 2. Create Portal A (Blue Portal)

1. Create an empty GameObject in the **root of the scene** (NOT under Player): `BluePortal`
2. **Add Components:**
   - Add `Portal` script (drag `Portal.cs` onto it)
   - Add `BoxCollider` component:
     - Check **Is Trigger**
     - Set size to `(1, 2, 0.1)` (width, height, depth)

3. **Create Screen Quad (child of BluePortal):**
   - Right-click BluePortal → **3D Object → Quad**
   - Name it `Screen`
   - In Inspector for Screen:
     - Scale: `(1, 2, 1)`
     - Position: `(0, 0, 0)`
     - Material: drag `Portal_Mat` onto the `Material` slot

4. **Configure Portal Component:**
   - Drag the `Screen` quad's `MeshRenderer` into the `screenRenderer` field
   - Leave `viewCamera` empty (script creates it automatically)
   - `textureWidth`: 512
   - `textureHeight`: 512
   - `renderMask`: all layers (or exclude `Portal` layer if feedback occurs)
   - `linkedPortal`: leave empty for now

---

### 3. Create Portal B (Orange Portal)

Repeat **Step 2**, but name it `OrangePortal` and place it somewhere else in the scene (e.g., on a different wall).

---

### 4. Link the Portals

1. Select **BluePortal** in the hierarchy
2. In the Inspector, find the `Portal` component
3. Drag **OrangePortal** into the `linkedPortal` field
4. Select **OrangePortal** in the hierarchy
5. Drag **BluePortal** into the `linkedPortal` field

Now each portal knows about the other.

---

### 5. Add PortalGun to the Player Camera

1. Select your **Main Camera** (under Player)
2. Add the `PortalGun` script to it
3. In the Inspector:
   - **Portals** size: 2
   - Element 0: drag `BluePortal` into it
   - Element 1: drag `OrangePortal` into it
   - **shootMask**: select layers you want portals placed on (e.g., "Default", "Wall")
   - **shootDistance**: 1000

---

## Clean Scene Hierarchy

```
Scene Root
├── Environment (or Walls)
│   └── Cube (wall)
├── BluePortal (root level)
│   ├── Screen (Quad with Portal_Mat)
│   └── PortalViewCam (auto-created)
├── OrangePortal (root level)
│   ├── Screen (Quad with Portal_Mat)
│   └── PortalViewCam (auto-created)
└── Player
    └── Main Camera (with PortalGun script)
```

This is much cleaner! Portals are not nested under the player, so:
- They can be placed anywhere in the world
- Multiple players could share the same portal pair
- No hierarchy clutter under the player

---

## What Happens at Runtime

1. **Press Left Mouse (Fire1):**
   - Raycast from player camera center
   - Hit a surface → BluePortal moves there
   - BluePortal's camera positions itself to see through OrangePortal

2. **Press Right Mouse (Fire2):**
   - Same but for OrangePortal

3. **Walk Through BluePortal:**
   - Your rigidbody touches BluePortal's collider
   - You're teleported to OrangePortal's position
   - Your velocity is mirrored

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Portals appear black | Ensure `Portal_Mat` uses `Custom/Portal` shader |
| Can't see anything | Check that `Portal` component's `screenRenderer` field is assigned |
| Portals won't place | Check `shootMask` includes the target layer |
| Camera feedback/recursion | Put portal quads on a `Portal` layer, exclude from `renderMask` |
| Teleport not working | Ensure `BoxCollider` is set to **Is Trigger** |
| Portals still in hierarchy after firing | Portals start inactive, `PortalGun` activates them on first shot |

---

## Controls

- **Left Click** (ShootBlue action): Place/move BluePortal
- **Right Click** (ShootOrange action): Place/move OrangePortal
- **Walk through**: Teleports you to the other portal
