#!/bin/sh
# Builds the extension from this folder, installs it for the current user and turns it on.
set -eu

UUID=spotify-top-bar-lyrics@melementi.github.io
cd "$(dirname "$0")"

mkdir -p dist
glib-compile-schemas extension/schemas
gnome-extensions pack extension --force --out-dir=dist --extra-source=lib
gnome-extensions install --force "dist/$UUID.shell-extension.zip"

if gnome-extensions enable "$UUID" 2>/dev/null; then
    gdbus call --session --dest org.gnome.Shell.Extensions --object-path /org/gnome/Shell/Extensions --method org.gnome.Shell.Extensions.ReloadExtension "$UUID" >/dev/null 2>&1 || true
    echo "Installed and enabled. If this replaced an older version, log out and back in to load the new one."
    exit 0
fi

# A running GNOME Shell only finds newly installed extensions after the next login, so it cannot enable this one
# yet; putting it on the enabled list makes it start at that login.
current=$(gsettings get org.gnome.shell enabled-extensions)
case "$current" in
    *"'$UUID'"*) ;;
    "@as []" | "[]") gsettings set org.gnome.shell enabled-extensions "['$UUID']" ;;
    *) gsettings set org.gnome.shell enabled-extensions "${current%]}, '$UUID']" ;;
esac
echo "Installed. Log out and back in to load it; it is already set to start then."
