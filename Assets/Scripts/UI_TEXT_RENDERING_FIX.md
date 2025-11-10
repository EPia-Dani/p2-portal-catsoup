# UI Text Not Rendering - Troubleshooting Guide

If your text appears in the Unity Editor but not in-game, check these common issues:

## ✅ Quick Fixes (Most Common Issues)

### 1. **Canvas Scale is Zero** ⚠️ CRITICAL
**Problem**: Canvas RectTransform scale is set to (0, 0, 0)  
**Fix**: 
- Select your Canvas in the Hierarchy
- In the Inspector, find the RectTransform component
- Set Scale to (1, 1, 1) - NOT (0, 0, 0)

### 2. **Canvas Render Mode**
**Problem**: Canvas Render Mode might be wrong  
**Fix**:
- Select Canvas → Inspector → Canvas component
- Set **Render Mode** to **"Screen Space - Overlay"** (for UI that should always be visible)
- OR **"Screen Space - Camera"** (assign your Main Camera if using this mode)
- OR **"World Space"** (only if you want 3D UI in the world)

### 3. **Text Color Matches Background**
**Problem**: Text color is white on white background (or transparent)  
**Fix**:
- Select your Text (TMP_Text) component
- In Inspector, check the **Color** field
- Set it to a visible color (e.g., White #FFFFFF or Black #000000)
- Make sure **Alpha** is 255 (not 0)

### 4. **Canvas Sorting Order**
**Problem**: Another Canvas is covering your death screen  
**Fix**:
- Select your Death Screen Canvas
- In Inspector → Canvas component
- Increase **Sort Order** (e.g., set to 100 to be on top)

### 5. **Canvas/Text GameObject is Disabled**
**Problem**: The GameObject is disabled in the hierarchy  
**Fix**:
- Check the checkbox next to Canvas/Text GameObject name in Hierarchy
- Make sure it's enabled (checkbox checked)

### 6. **Text Font Asset Missing**
**Problem**: TMP_Text has no font assigned  
**Fix**:
- Select your Text component
- In Inspector → TMP_Text component
- Assign a **Font Asset** (use default if none assigned)
- Unity should auto-create one, but you can create via: Right-click → Create → TextMeshPro → Font Asset

### 7. **Canvas Scaler Settings**
**Problem**: Canvas Scaler might be scaling incorrectly  
**Fix**:
- Select Canvas → Canvas Scaler component
- Set **UI Scale Mode** to **"Scale With Screen Size"**
- Set **Reference Resolution** to your target resolution (e.g., 1920x1080)
- Set **Match** to 0.5 (or adjust as needed)

### 8. **Text Size is Too Small**
**Problem**: Font size is 0 or very small  
**Fix**:
- Select Text component
- Set **Font Size** to a visible size (e.g., 36 or higher)

### 9. **RectTransform Position/Anchors**
**Problem**: Text is positioned off-screen  
**Fix**:
- Select Text GameObject
- In RectTransform, check **Anchored Position**
- Make sure it's within screen bounds
- Try setting anchors to center: Click anchor preset → Hold Alt → Click center preset

### 10. **Canvas Layer**
**Problem**: Canvas is on wrong layer  
**Fix**:
- Select Canvas
- Set **Layer** to **"UI"** (Layer 5)

## 🔍 Debugging Steps

1. **Check if Canvas is rendering at all**:
   - Add a simple Image/Button to the Canvas
   - If Image/Button shows but Text doesn't → Text-specific issue
   - If nothing shows → Canvas issue

2. **Check Console for errors**:
   - Look for TMP_Text warnings about missing fonts
   - Look for Canvas warnings

3. **Test in different resolutions**:
   - Game view → Aspect Ratio dropdown → Try different resolutions
   - Some scaling issues only appear at certain resolutions

4. **Verify Canvas is active**:
   - In Hierarchy, make sure Canvas GameObject has checkbox checked
   - Make sure parent objects are also enabled

## 🎯 Recommended Canvas Setup for Death Screen

1. **Create Canvas**:
   - Right-click Hierarchy → UI → Canvas
   - Name it "DeathScreenCanvas"

2. **Canvas Settings**:
   - Render Mode: **Screen Space - Overlay**
   - Sort Order: **100** (to be on top)
   - Pixel Perfect: **Checked** (optional)

3. **Canvas Scaler**:
   - UI Scale Mode: **Scale With Screen Size**
   - Reference Resolution: **1920 x 1080**
   - Match: **0.5**

4. **Create Death Screen Panel**:
   - Right-click Canvas → UI → Panel
   - Name it "DeathScreenPanel"
   - Set RectTransform anchors to stretch (click anchor preset → Alt+Click stretch)
   - Set color to semi-transparent black (for background)

5. **Create Text**:
   - Right-click DeathScreenPanel → UI → Text - TextMeshPro
   - Name it "DeathMessageText"
   - Set anchors to center
   - Set Font Size: **72**
   - Set Color: **White** (#FFFFFF)
   - Set Alignment: **Center**
   - Set Text: **"You Died"**

6. **Create Buttons**:
   - Right-click DeathScreenPanel → UI → Button - TextMeshPro
   - Create two buttons: "ContinueButton" and "RestartButton"
   - Position them below the text

7. **Assign to DeathScreenManager**:
   - Select DeathScreenManager GameObject
   - Drag DeathScreenPanel to "Death Screen Panel" field
   - Drag DeathMessageText to "Death Message Text" field
   - Drag buttons to respective fields

## ⚡ Quick Test

To quickly test if your Canvas is working:
1. Create a simple red Image on the Canvas
2. If the red image shows → Canvas works, issue is with Text
3. If red image doesn't show → Canvas setup issue

