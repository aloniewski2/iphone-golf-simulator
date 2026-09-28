#!/bin/zsh
# usage: run_grip.sh TAG JSON
cd "$(dirname $0)"
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P grip_views.py -- "$2" 2>&1 | grep -E "Error|Traceback"
cd .. && /private/tmp/tennis-preview-runtime/bin/python tools/tile.py grip_iter/$1.png grip_iter/$1_[0-9].png
