#!/bin/sh
# Adds PhotoX to the Linux application menu for the current user (prompt_04 §2): the icon in the hicolor theme and a
# desktop entry that starts the given build.
#
#   dotnet publish src/PhotoX.App -c Release -r linux-x64 --self-contained -o ~/PhotoX
#   packaging/linux/install-desktop-entry.sh ~/PhotoX
set -eu
app_dir=$(cd "${1:?usage: $0 <folder that contains the PhotoX executable>}" && pwd)
[ -x "$app_dir/PhotoX" ] || { echo "No PhotoX executable in $app_dir" >&2; exit 1; }
repo=$(cd "$(dirname "$0")/../.." && pwd)
data=${XDG_DATA_HOME:-$HOME/.local/share}
for s in 16 24 32 48 64 128 256 512; do
  mkdir -p "$data/icons/hicolor/${s}x${s}/apps"
  cp "$repo/assets/logo/photox-icon-$s.png" "$data/icons/hicolor/${s}x${s}/apps/photox.png"
done
mkdir -p "$data/icons/hicolor/scalable/apps" "$data/applications"
cp "$repo/assets/logo/photox-icon.svg" "$data/icons/hicolor/scalable/apps/photox.svg"
sed "s|@EXEC@|\"$app_dir/PhotoX\"|" "$repo/packaging/linux/photox.desktop" > "$data/applications/photox.desktop"
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database "$data/applications" || true; fi
echo "Installed $data/applications/photox.desktop"
