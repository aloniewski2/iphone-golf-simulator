import SceneKit
import simd
import XCTest
@testable import GolfArcade

final class VisualAssetTests: XCTestCase {
    @MainActor
    func testPoloTrianglesRespectExactClothingSeams() {
        let mesh = GolferSkin.mesh
        let sleeve = AvatarSize.shoulderHalfWidth + AvatarSize.upperArm * 0.56
        for index in Set(mesh.triangles[0]) {
            let p = mesh.vertices[Int(index)]
            XCTAssertGreaterThanOrEqual(p.y, AvatarSize.hipHeight + 0.12 - 0.0001)
            XCTAssertLessThanOrEqual(p.y, GolferSkin.rest[.neck].y + 0.4201)
            XCTAssertLessThanOrEqual(abs(p.z), sleeve + 0.0001)
        }
        XCTAssertLessThan(mesh.vertices.count, 60_000)
    }

    @MainActor
    func testRenderedResortSurfaceMatchesPhysicsAndHasBoundedGeometry() {
        for hole in Course.sunwardResort.holes {
            let geometry = CourseArt.playableSurface(hole)
            XCTAssertEqual(geometry.materials.count, 7, "Deep rough has a distinct material")
            let source = geometry.sources(for: .vertex)[0]
            XCTAssertLessThan(source.vectorCount, 220_000, "Hole \(hole.number)")
            // Check sampled vertices of the actual render mesh, not a second terrain model.
            source.data.withUnsafeBytes { bytes in
                for index in stride(from: 0, to: source.vectorCount, by: 31) {
                    let offset = source.dataOffset + index * source.dataStride
                    let x = bytes.loadUnaligned(fromByteOffset: offset, as: Float.self)
                    let y = bytes.loadUnaligned(fromByteOffset: offset + 4, as: Float.self)
                    let z = bytes.loadUnaligned(fromByteOffset: offset + 8, as: Float.self)
                    let surface = hole.surface(at: CoursePoint(x: Double(x), d: -Double(z)))
                    if surface.lie != .outOfBounds {
                        XCTAssertEqual(Double(y), surface.heightYards, accuracy: 0.0001)
                    } else {
                        XCTAssertEqual(y, CourseArt.surfaceHeight(CoursePoint(x: Double(x), d: -Double(z)), hole: hole), accuracy: 0.0001)
                    }
                }
            }
        }
    }

    func testShortGameFamiliesStayCompactWithoutChangingAddressContact() {
        let full = AvatarAnimations.swingArc(degrees: 150, club: .wedge)
        let pitch = AvatarAnimations.swingArc(degrees: 150, club: .wedge, type: .pitch)
        let chip = AvatarAnimations.swingArc(degrees: 150, club: .wedge, type: .chip)
        XCTAssertLessThan(chip.handCenter.y, pitch.handCenter.y)
        XCTAssertLessThan(pitch.handCenter.y, full.handCenter.y)
        for type in [ShotType.full, .chip, .pitch, .bunker] {
            let pose = AvatarAnimations.swingArc(degrees: 0, club: .wedge, type: type)
            XCTAssertLessThan(simd_distance(pose.clubHead, AvatarSize.ball), 0.001)
        }
        let chipFinish = AvatarAnimations.swingArc(degrees: -150, club: .wedge, type: .chip)
        XCTAssertEqual(chipFinish[.rightAnkle], AvatarAnimations.address[.rightAnkle])
    }
}
