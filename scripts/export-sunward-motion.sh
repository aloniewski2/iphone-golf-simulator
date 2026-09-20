#!/bin/zsh
set -euo pipefail
cd "$(dirname "$0")/.."
motion_build_dir=$(mktemp -d /private/tmp/sunward-motion.XXXXXX)
xcrun swiftc -O -module-cache-path "$motion_build_dir/cache" -o "$motion_build_dir/export" \
 GolfArcade/Domain/Club.swift GolfArcade/Domain/SwingMetrics.swift \
 GolfArcade/Domain/SwingImpact.swift GolfArcade/Domain/PoseFrame.swift GolfArcade/Domain/GolferAppearance.swift GolfArcade/Domain/Player.swift \
 GolfArcade/Shot/FlightPath.swift GolfArcade/Shot/ShotEngine.swift \
 GolfArcade/Mock/BallFlight.swift GolfArcade/Mock/ArmSwing.swift \
 GolfArcade/Game/Course.swift GolfArcade/Game/CourseShot.swift GolfArcade/Game/GolfInteractions.swift \
 GolfArcade/Game/ShotRecommendation.swift GolfArcade/Game/CourseRound.swift \
 GolfArcade/Tracking/HandGestureRecognizer.swift GolfArcade/Tracking/PlayerCalibration.swift \
 GolfArcade/Swing/BallAddress.swift GolfArcade/Avatar/BodyPose3D.swift \
 GolfArcade/Avatar/AvatarAnimations.swift GolfArcade/Avatar/AuthoredGolfMotion.swift scripts/SunwardMotionAuthor.swift scripts/ExportSunwardMotion.swift
"$motion_build_dir/export" GolfArcade/Resources/Sunward/SunwardMotion.json
