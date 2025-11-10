# Death System Setup Guide

This guide explains how to set up the death zone and death screen system in your Unity scene.

## Components Overview

1. **DeathZone.cs** - A trigger zone that kills the player when entered
2. **PlayerManager.cs** - Manages player state, death, and respawn
3. **DeathScreenManager.cs** - Handles the death screen UI
4. **ScreenFade.cs** - Handles fade to black/fade in transitions

## Setup Instructions

### Step 1: Add PlayerManager to Scene

1. Create an empty GameObject in your scene (name it "PlayerManager")
2. Add the `PlayerManager` component to it
3. In the Inspector:
   - Assign the Player GameObject (or leave empty to auto-detect)
   - Optionally assign a RespawnPoint Transform (or leave empty to use player's starting position)
   - Set `Respawn At Point` to true if you want respawn at checkpoint, false to restart scene

### Step 2: Create Death Zone

1. Create a GameObject (e.g., a Cube or Plane) where you want the death zone
2. Add a Collider component (Box Collider, Sphere Collider, etc.)
3. **Important**: Check "Is Trigger" on the Collider
4. Add the `DeathZone` component
5. In the Inspector:
   - Set `Target Tag` to "Player" (or leave empty to trigger for any object)
   - Set `Death Delay` if you want a delay before death triggers

### Step 3: Create Death Screen UI

1. Create a Canvas in your scene (if you don't have one)
2. **Create Screen Fade Overlay** (for fade transitions):
   - Right-click Canvas → UI → Image
   - Name it "ScreenFade"
   - Select it and in Inspector:
     - Set Image Color to Black (#000000)
     - Set Alpha to 0 (transparent)
     - Add the `ScreenFade` component
     - Set `Fade Duration` to 0.5 (or your preferred duration)
     - Set `Fade Color` to Black
   - In RectTransform: Set anchors to stretch (Alt+Click stretch preset) so it covers full screen
3. Create a Panel as a child of the Canvas (name it "DeathScreenPanel")
4. Style the panel (e.g., semi-transparent black background)
5. Add child UI elements:
   - **Death Message Text** (TMP_Text): Display "You Died" or similar
   - **Continue Button**: Button to respawn at checkpoint
   - **Restart Button**: Button to return to main menu
6. Create an empty GameObject as a child of the Canvas (name it "DeathScreenManager")
7. Add the `DeathScreenManager` component to it
8. In the Inspector:
   - Assign the DeathScreenPanel
   - Assign the Death Message Text
   - Assign the Continue Button
   - Assign the Restart Button
   - **Assign the ScreenFade** GameObject (drag ScreenFade to the `Screen Fade` field)
   - Customize the `Death Message` text
   - Set `Pause On Death` to true if you want the game to pause when dead
   - Set `Fade Out Duration` (default: 0.5 seconds) - how long to fade to black
   - Set `Fade In Duration` (default: 0.5 seconds) - how long to fade in after respawn

### Step 4: Tag Your Player

1. Select your Player GameObject
2. In the Inspector, set the Tag to "Player" (or create a custom tag)
3. Make sure the DeathZone's `Target Tag` matches

## How It Works

1. When the player enters a DeathZone trigger:
   - DeathZone calls `PlayerManager.OnPlayerDeath()`
   - PlayerManager triggers death
   - **Screen fades to black** (fade out transition)
   - DeathScreenManager shows the death screen UI after fade completes

2. When player clicks "Continue":
   - DeathScreenManager hides the UI
   - **Screen fades in** (fade in transition)
   - PlayerManager respawns the player at the respawn point after fade completes
   - Player control is re-enabled

3. When player clicks "Restart":
   - Returns to main menu (no fade needed as scene changes)

## Customization

- **Respawn Points**: You can dynamically set respawn points by calling `PlayerManager.SetRespawnPoint(Transform)` from other scripts (e.g., checkpoint scripts)
- **Death Delay**: Set a delay on DeathZone if you want a brief moment before death triggers
- **Death Message**: Customize the message shown on the death screen
- **Pause Behavior**: Toggle `Pause On Death` to pause/unpause the game when dead

## Example: Creating a Checkpoint System

To create checkpoints that update the respawn point:

```csharp
public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = PlayerManager.Instance;
            if (playerManager != null)
            {
                playerManager.SetRespawnPoint(transform);
                Debug.Log("Checkpoint activated!");
            }
        }
    }
}
```


