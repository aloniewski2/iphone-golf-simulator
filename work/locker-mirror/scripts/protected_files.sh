#!/bin/zsh
# Prints "sha256  path" for every file the LOCKER_MIRROR job must not touch (gate line 4).
# usage: protected_files.sh > sha_before.txt   (run again later into sha_after.txt and diff)
cd "$(dirname "$0")/../../.." || exit 1
{
  ls Unity/Assets/Characters/MatchHeroes/Male/*.fbx Unity/Assets/Characters/MatchHeroes/Female/*.fbx 2>/dev/null     # every clip FBX: Serve, Walk, Run*, Forehand ...
  ls Unity/Assets/Characters/MatchHeroes/*/*.fbx.meta 2>/dev/null
  ls Unity/Assets/Resources/Tennis/Shaders/*.shader                                                                   # TennisCharacter, TennisCloth, ...
  echo Unity/Assets/Scripts/Tennis/MatchHeroLook.cs
  echo Unity/Assets/Editor/MatchHeroKit.cs
  ls Unity/Assets/Scripts/Course/*.cs                                                                                  # the golf course
  ls Unity/Assets/Resources/Tennis/HeroDetail/*.png                                                                    # the source look maps (copied, never edited)
  ls work/emotes-intro 2>/dev/null | sed 's|^|work/emotes-intro/|'
} 2>/dev/null | sort -u | while read -r f; do [ -f "$f" ] && shasum -a 256 "$f"; done
