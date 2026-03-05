#!/bin/bash
# Unity 项目文件结构整理脚本
# 运行前必须关闭 Unity Editor！
# 在 memory/ 目录下运行: bash reorganize_assets.sh

set -e  # 任何错误立即停止

ASSETS="Assets"

# 辅助函数：移动文件和它的.meta
move_with_meta() {
  local src="$1"
  local dst_dir="$2"
  if [ -f "$src" ]; then
    mv "$src" "$dst_dir/"
    echo "  ✓ $src"
  fi
  if [ -f "${src}.meta" ]; then
    mv "${src}.meta" "$dst_dir/"
  fi
}

# 辅助函数：移动整个文件夹和它的.meta
move_dir_with_meta() {
  local src="$1"
  local dst_dir="$2"
  if [ -d "$src" ]; then
    mv "$src" "$dst_dir/"
    echo "  ✓ $src/"
  fi
  if [ -f "${src}.meta" ]; then
    mv "${src}.meta" "$dst_dir/"
  fi
}

echo "=== 开始整理 Unity 项目文件结构 ==="
echo ""

# ──────────────────────────────────────────
# 1. 创建新文件夹结构
# ──────────────────────────────────────────
echo "📁 创建新目录结构..."
mkdir -p "$ASSETS/_Global/Scripts"
mkdir -p "$ASSETS/_Global/Prefabs"
mkdir -p "$ASSETS/_Global/Config"
mkdir -p "$ASSETS/Scenes"
mkdir -p "$ASSETS/Wakeup/Scripts"
mkdir -p "$ASSETS/Wakeup/Art"
mkdir -p "$ASSETS/Wakeup/Audio"
mkdir -p "$ASSETS/Archive/Scripts"
mkdir -p "$ASSETS/Archive/Art"
mkdir -p "$ASSETS/Archive/Shaders"
mkdir -p "$ASSETS/Corridor/Scripts"
mkdir -p "$ASSETS/Corridor/Art"
mkdir -p "$ASSETS/Corridor/LightingData"
mkdir -p "$ASSETS/Narration"
mkdir -p "$ASSETS/Bootstrap"
echo "  ✓ 目录创建完成"
echo ""

# ──────────────────────────────────────────
# 2. _Global/Scripts — 全局管理脚本
# ──────────────────────────────────────────
echo "📦 整理全局脚本..."
move_with_meta "$ASSETS/Wakeup_Project/Script_2/GlobalMentalState.cs"    "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/GlobalProgressManager.cs" "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/GlobalUIManager.cs"       "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/AutoStartGame.cs"         "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/ChapterEvaluator.cs"      "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Core/GameEventPayloads.cs"                         "$ASSETS/_Global/Scripts"
move_with_meta "$ASSETS/Core/CursorService.cs"                             "$ASSETS/_Global/Scripts"
echo ""

# ──────────────────────────────────────────
# 3. _Global/Prefabs
# ──────────────────────────────────────────
echo "📦 整理全局 Prefabs..."
move_with_meta "$ASSETS/GlobalProgressManager.prefab" "$ASSETS/_Global/Prefabs"
move_with_meta "$ASSETS/Prefabs/ChoiceBtn.prefab"     "$ASSETS/_Global/Prefabs"
move_with_meta "$ASSETS/Prefabs/player.prefab"        "$ASSETS/_Global/Prefabs"
echo ""

# ──────────────────────────────────────────
# 4. _Global/Config
# ──────────────────────────────────────────
echo "📦 整理 Config..."
move_with_meta "$ASSETS/Config/GameBalance.asset"      "$ASSETS/_Global/Config"
move_with_meta "$ASSETS/Config/GameBalanceConfig.cs"   "$ASSETS/_Global/Config"
move_with_meta "$ASSETS/Config/EndingData.asset"       "$ASSETS/_Global/Config"
move_with_meta "$ASSETS/Config/EndingData.cs"          "$ASSETS/_Global/Config"
echo ""

# ──────────────────────────────────────────
# 5. Scenes — 所有 .unity 场景文件
# ──────────────────────────────────────────
echo "🎬 整理场景文件..."
move_with_meta "$ASSETS/0_Bootstrap.unity"     "$ASSETS/Scenes"
move_with_meta "$ASSETS/MainMenu.unity"        "$ASSETS/Scenes"
move_with_meta "$ASSETS/Wakeup_room.unity"     "$ASSETS/Scenes"
move_with_meta "$ASSETS/Archive_room.unity"    "$ASSETS/Scenes"
move_with_meta "$ASSETS/Corridor.unity"        "$ASSETS/Scenes"
move_with_meta "$ASSETS/Corridor_room1.unity"  "$ASSETS/Scenes"  # 注意：同时需要移动lightmap
move_with_meta "$ASSETS/Corridor_room2.unity"  "$ASSETS/Scenes"
move_with_meta "$ASSETS/Corridor_room3.unity"  "$ASSETS/Scenes"
move_with_meta "$ASSETS/Corridor_room4.unity"  "$ASSETS/Scenes"
move_with_meta "$ASSETS/Corridor_room5.unity"  "$ASSETS/Scenes"
move_with_meta "$ASSETS/0_Bootstrap/Corridor_room1.unity" "$ASSETS/Scenes"
echo ""

# ──────────────────────────────────────────
# 6. Wakeup — Wakeup 场景相关
# ──────────────────────────────────────────
echo "🛏️ 整理 Wakeup 资源..."
# Scripts
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/WakeupDialogueData.cs"    "$ASSETS/Wakeup/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/WakeupDialogueUI.cs"      "$ASSETS/Wakeup/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/WakeupLetter.cs"          "$ASSETS/Wakeup/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/WakeupSequenceManager.cs" "$ASSETS/Wakeup/Scripts"
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/WakeupDialogueData.cs"    "$ASSETS/Wakeup/Scripts"
# Data assets
move_with_meta "$ASSETS/Wakeup_Project/Script_2/Wakeup/IntroDialogue.asset"      "$ASSETS/Wakeup/Scripts"
move_with_meta "$ASSETS/WakeupTimeline.playable"                                   "$ASSETS/Wakeup"
# Art
move_dir_with_meta "$ASSETS/Wakeup_Project/Scenes_2/bedroom_fbx" "$ASSETS/Wakeup/Art"
move_dir_with_meta "$ASSETS/Wakeup_Project/Scenes_2/wallr_fbx"   "$ASSETS/Wakeup/Art"
move_dir_with_meta "$ASSETS/Wakeup_Project/Scenes_2/door_fbx"    "$ASSETS/Wakeup/Art"
# Audio
move_dir_with_meta "$ASSETS/Wakeup_audio" "$ASSETS/Wakeup/Audio"
[ -d "$ASSETS/Wakeup/Audio/Wakeup_audio" ] && mv "$ASSETS/Wakeup/Audio/Wakeup_audio"/* "$ASSETS/Wakeup/Audio/" 2>/dev/null || true
[ -d "$ASSETS/Wakeup/Audio/Wakeup_audio" ] && rm -rf "$ASSETS/Wakeup/Audio/Wakeup_audio"
echo ""

# ──────────────────────────────────────────
# 7. Archive — Archive 场景相关
# ──────────────────────────────────────────
echo "📜 整理 Archive 资源..."
# Scripts
move_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scripts/CausalController.cs"      "$ASSETS/Archive/Scripts"
move_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scripts/CausalLightController.cs" "$ASSETS/Archive/Scripts"
move_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scripts/CausalModel.cs"           "$ASSETS/Archive/Scripts"
move_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scripts/CausalView.cs"            "$ASSETS/Archive/Scripts"
# Art
move_dir_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scenes/dora_fbx"  "$ASSETS/Archive/Art"
move_dir_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scenes/room_fbx"  "$ASSETS/Archive/Art"
move_dir_with_meta "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scenes/frame_fbx" "$ASSETS/Archive/Art"
# Shaders
move_with_meta "$ASSETS/CausalEntropyShader.shader" "$ASSETS/Archive/Shaders"
echo ""

# ──────────────────────────────────────────
# 8. Corridor — Corridor 场景相关
# ──────────────────────────────────────────
echo "🚪 整理 Corridor 资源..."
# Scripts
move_with_meta "$ASSETS/Corridor_Project/Script_1/CorridorDoorManager.cs" "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/CorridorManager.cs"     "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/CorridorPlayer.cs"      "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/DoorAction.cs"          "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/IInteractable.cs"       "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/InteractHighlight.cs"   "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/NarrationTrigger.cs"    "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/TeleportTrigger.cs"     "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_Project/Script_1/ApiConnectionTest.cs"   "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Rooms/Room_1/FastDoorController.cs"               "$ASSETS/Corridor/Scripts"
# Extra corridor scripts
move_with_meta "$ASSETS/Corridor_rooms/Script_3/AITextureRequester.cs"    "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_rooms/Script_3/MemoryItem.cs"            "$ASSETS/Corridor/Scripts"
move_with_meta "$ASSETS/Corridor_rooms/Script_3/ScreenFader.cs"           "$ASSETS/Corridor/Scripts"
# Art
move_dir_with_meta "$ASSETS/Rooms/Room_1/书架_fbx"      "$ASSETS/Corridor/Art"
move_with_meta "$ASSETS/Corridor_Project/Scenes_1/door.mat"         "$ASSETS/Corridor/Art"
move_with_meta "$ASSETS/Corridor_Project/Scenes_1/New Material.mat" "$ASSETS/Corridor/Art"
# Prefabs (Corridor_Segment 保留在 _Global/Prefabs 更合适)
move_with_meta "$ASSETS/Prefabs/Corridor_Segment.prefab" "$ASSETS/_Global/Prefabs"
# LightingData
move_dir_with_meta "$ASSETS/Corridor_room1" "$ASSETS/Corridor/LightingData"
echo ""

# ──────────────────────────────────────────
# 9. Narration
# ──────────────────────────────────────────
echo "📢 整理 Narration 资源..."
move_with_meta "$ASSETS/0_Bootstrap/Narration/MainNarrationDB.cs" "$ASSETS/Narration"
move_with_meta "$ASSETS/0_Bootstrap/Narration/NarrationData.cs"   "$ASSETS/Narration"
move_with_meta "$ASSETS/0_Bootstrap/Narration/NarratorManager.cs" "$ASSETS/Narration"
echo ""

# ──────────────────────────────────────────
# 10. Bootstrap — 启动逻辑
# ──────────────────────────────────────────
echo "🚀 整理 Bootstrap 脚本..."
move_with_meta "$ASSETS/0_Bootstrap/DevPreload.cs"       "$ASSETS/Bootstrap"
move_with_meta "$ASSETS/0_Bootstrap/MainMenuManager.cs"  "$ASSETS/Bootstrap"
move_with_meta "$ASSETS/0_Bootstrap/MentalStatsUI.cs"    "$ASSETS/Bootstrap"
move_with_meta "$ASSETS/0_Bootstrap/SimpleSceneLoader.cs" "$ASSETS/Bootstrap"
echo ""

# ──────────────────────────────────────────
# 11. 删除临时调试脚本
# ──────────────────────────────────────────
echo "🗑️  删除临时调试脚本..."
for f in DiagnoseUI.cs EmergencyGUI.cs FixEndingUI.cs; do
  if [ -f "$ASSETS/$f" ]; then
    rm "$ASSETS/$f"
    rm -f "$ASSETS/$f.meta"
    echo "  ✓ 删除 $f"
  fi
done
# 删除项目内的 task.md（不属于 Assets）
rm -f "$ASSETS/Wakeup_Project/task.md" "$ASSETS/Wakeup_Project/task.md.meta"
echo ""

# ──────────────────────────────────────────
# 12. 清理空文件夹
# ──────────────────────────────────────────
echo "🧹 清理空文件夹..."
# 删除旧文件夹（已经空了）
rm -rf "$ASSETS/Wakeup_Project/Script_2/Wakeup"
rm -rf "$ASSETS/Wakeup_Project/Script_2"
rm -rf "$ASSETS/Wakeup_Project/Scenes_2"
rm -rf "$ASSETS/Wakeup_Project"
rm -rf "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scripts"
rm -rf "$ASSETS/0_Bootstrap/Scripts/Archive_Project/Scenes"
rm -rf "$ASSETS/0_Bootstrap/Scripts/Archive_Project"
rm -rf "$ASSETS/0_Bootstrap/Scripts/Interfaces"
rm -rf "$ASSETS/0_Bootstrap/Scripts"
rm -rf "$ASSETS/0_Bootstrap/Narration"
rm -rf "$ASSETS/0_Bootstrap"
rm -rf "$ASSETS/Corridor_Project/Script_1"
rm -rf "$ASSETS/Corridor_Project/Scenes_1"
rm -rf "$ASSETS/Corridor_Project"
rm -rf "$ASSETS/Corridor_rooms/Script_3"
rm -rf "$ASSETS/Corridor_rooms/Scenes_3"
rm -rf "$ASSETS/Corridor_rooms"
rm -rf "$ASSETS/Rooms/Room_1"
rm -rf "$ASSETS/Rooms"
rm -rf "$ASSETS/Core"
rm -rf "$ASSETS/Config"
rm -rf "$ASSETS/Prefabs"
# 删除对应的 .meta 文件
find "$ASSETS" -name "*.meta" | while read meta; do
  base="${meta%.meta}"
  if [ ! -e "$base" ]; then
    rm "$meta"
  fi
done
echo "  ✓ 清理完成"
echo ""
echo "=== ✅ 整理完成！现在可以重新打开 Unity ==="
echo ""
echo "新结构："
find Assets -maxdepth 2 -type d -not -path "*/\.*" -not -path "*/TextMesh*" -not -path "*/TutorialInfo*" -not -path "*/Settings*" | sort
