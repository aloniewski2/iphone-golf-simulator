#!/bin/zsh
# Host-side production logic checks. Not a substitute for physical iPhone tracking tests.
set -euo pipefail
cd "$(dirname "$0")/.."
golf_check_dir=$(mktemp -d /private/tmp/golf-core-check.XXXXXX)
xcrun swiftc -D DEBUG -O -o "$golf_check_dir/check" \
 GolfArcade/Domain/Club.swift GolfArcade/Domain/SwingMetrics.swift \
 GolfArcade/Domain/SwingImpact.swift GolfArcade/Domain/PoseFrame.swift \
 GolfArcade/Shot/FlightPath.swift GolfArcade/Shot/ShotEngine.swift \
 GolfArcade/Mock/BallFlight.swift GolfArcade/Mock/ArmSwing.swift \
 GolfArcade/Game/Course.swift GolfArcade/Game/CourseShot.swift \
 GolfArcade/Game/ShotRecommendation.swift GolfArcade/Game/CourseRound.swift \
 GolfArcade/Tracking/HandGestureRecognizer.swift GolfArcade/Tracking/PlayerCalibration.swift \
 GolfArcade/Swing/BallAddress.swift GolfArcade/Avatar/BodyPose3D.swift \
 GolfArcade/Avatar/AvatarAnimations.swift scripts/CoreRegression.swift
"$golf_check_dir/check"
node scripts/verify-golfer.mjs
