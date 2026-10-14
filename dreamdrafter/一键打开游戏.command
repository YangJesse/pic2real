#!/bin/zsh
# 双击运行。请将本文件与 UnityDemo 文件夹放在一起。
set -euo pipefail

BASE_DIR="${0:A:h}"
PROJECT_DIR="$BASE_DIR/UnityDemo"
STATE_DIR="$PROJECT_DIR/Library/OneClickPlay"

fail() {
    print -u2 -- "启动失败：$1"
    if [[ -t 0 ]]; then read -r '?按回车关闭…'; fi
    exit 1
}

[[ -f "$PROJECT_DIR/Assets/Scenes/00_Workshop.unity" ]] || fail '没有找到 UnityDemo/Assets/Scenes/00_Workshop.unity。请保持脚本和 UnityDemo 文件夹在同一目录。'
VERSION=$(/usr/bin/awk '/^m_EditorVersion:/{print $2; exit}' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt")
UNITY_APP=""
for candidate in "/Applications/Unity/Unity-$VERSION/Unity.app" "/Applications/Unity/Hub/Editor/$VERSION/Unity.app"; do
    if [[ -x "$candidate/Contents/MacOS/Unity" ]]; then
        UNITY_APP="$candidate"
        break
    fi
done
[[ -n "$UNITY_APP" ]] || fail "未找到 Unity $VERSION，请先安装对应版本。"

# 只检测同一个工程，避免重复打开引起工程锁冲突。
UNITY_PID=$(/bin/ps -axo pid=,command= | /usr/bin/awk -v project="$PROJECT_DIR" '
    $2 ~ /\/Unity.app\/Contents\/MacOS\/Unity$/ &&
    (index($0 " ", "-projectpath " project " ") || index($0 " ", "-projectPath " project " ")) {pid=$1}
    END {if (pid) print pid}
')

# 首次添加辅助脚本时，Unity 可能等当前试玩结束才导入它。
# 此时只把已打开的工程放到前台，保留当前关卡与进度。
if [[ -n "$UNITY_PID" && ! -f "$PROJECT_DIR/Assets/Editor/QuickPlayLauncher.cs.meta" ]]; then
    /usr/bin/open -a "$UNITY_APP" || fail '无法切换到 Unity。'
    print -- '已切回当前 Unity 游戏。首次安装的自动启动功能会在下次打开工程时生效。'
    exit 0
fi

/bin/mkdir -p "$STATE_DIR"
/bin/rm -f "$STATE_DIR/result.txt"
print -r -- "start" > "$STATE_DIR/request.tmp"
/bin/mv -f "$STATE_DIR/request.tmp" "$STATE_DIR/request.txt"

print -- '正在打开《梦筑师》游戏，请稍候…'
if [[ -n "$UNITY_PID" ]]; then
    /usr/bin/open -a "$UNITY_APP" || fail '无法切换到 Unity。'
else
    /usr/bin/open -n -a "$UNITY_APP" --args -projectPath "$PROJECT_DIR" -buildTarget osxuniversal || fail '无法启动 Unity。'
fi

# 等待 Unity 导入完成并进入 Play；不会自动同意任何首次启动协议。
for (( attempt=0; attempt<240; attempt++ )); do
    if [[ -f "$STATE_DIR/result.txt" ]]; then
        RESULT=$(<"$STATE_DIR/result.txt")
        if [[ "$RESULT" == 'ready' ]]; then
            /usr/bin/open -a "$UNITY_APP"
            print -- '游戏已打开。点击 Game 画面，按 E 开始，WASD 移动。'
            exit 0
        fi
        fail "$RESULT"
    fi
    /bin/sleep 1
done
/bin/rm -f "$STATE_DIR/request.txt"
fail '等待超时。请查看 Unity 是否停在协议、保存提示或编译错误窗口；处理后再次双击本脚本。'
