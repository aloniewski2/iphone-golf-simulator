#!/bin/zsh
# ACCESSORIES: run the native hero tests (VisualOverhaulNativeTests, MatchHeroLockerTests, LockerMirrorTests) from a scratch xcodegen project on the booted simulator.
#   native_tests.sh            tests of the working tree  -> logs/native_current.log
#   native_tests.sh baseline   tests of the git INDEX (checked-in state, no accessories)  -> logs/native_baseline.log
# Needs a booted iPhone 17 Pro simulator. A hung `xcodebuild test` is killed once its verdict line is printed.
set -e
REPO=${0:A:h:h:h}; MODE=${1:-current}; W=${TMPDIR:-/tmp}/acc-native-$MODE; LOG=$REPO/work/accessories/logs/native_$MODE.log
rm -rf $W; mkdir -p $W
if [[ $MODE == baseline ]]; then
  ROOT=$W/src; mkdir -p $ROOT
  (cd $REPO && git ls-files -z GolfArcade GolfArcadeTests GolfArcadeUITests project.yml | xargs -0 git checkout-index --prefix=$ROOT/)
else ROOT=$REPO; fi
sed -e "s#CODE_SIGN_ENTITLEMENTS: GolfArcade#CODE_SIGN_ENTITLEMENTS: $ROOT/GolfArcade#" -e "s#INFOPLIST_FILE: GolfArcade#INFOPLIST_FILE: $ROOT/GolfArcade#" -e "s#SWIFT_OBJC_BRIDGING_HEADER: GolfArcade#SWIFT_OBJC_BRIDGING_HEADER: $ROOT/GolfArcade#" $ROOT/project.yml > $W/project.yml
xcodegen generate --spec $W/project.yml --project-root $ROOT --project $W > $W/xcodegen.log 2>&1
( xcodebuild test -project $W/GolfArcade.xcodeproj -scheme GolfArcade -destination 'platform=iOS Simulator,id=88A5F840-AF09-4E3F-AF23-089D85AF4F25' -derivedDataPath $W/dd \
   ${=ONLY:--only-testing:GolfArcadeTests/VisualOverhaulNativeTests -only-testing:GolfArcadeTests/MatchHeroLockerTests -only-testing:GolfArcadeTests/LockerMirrorTests} \
   CODE_SIGNING_ALLOWED=NO > $LOG 2>&1 ) &
PID=$!
until grep -qE "^\*\* TEST (SUCCEEDED|FAILED) \*\*" $LOG 2>/dev/null || ! kill -0 $PID 2>/dev/null; do sleep 10; done
sleep 5; pkill -P $PID 2>/dev/null || true; kill $PID 2>/dev/null || true
grep -E "Test Case .* (failed|passed)|Executed|TEST (SUCCEEDED|FAILED)" $LOG | grep -E "failed|Executed|TEST" | tail -40
