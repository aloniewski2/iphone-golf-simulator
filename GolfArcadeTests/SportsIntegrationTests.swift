import XCTest
@testable import GolfArcade

final class SportsIntegrationTests:XCTestCase {
    func testPhysicalShiftMovesRightWithoutSwing() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        for i in 1...100 { XCTAssertNil(f.step(position:0.2,rate:0.2,time:Double(i)/100,valid:true)) }
        XCTAssertGreaterThan(f.target,0.4); XCTAssertEqual(f.phase,.steering)
    }
    func testSwingFreezesSteeringAndEmitsOnce() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        var hits=0
        for i in 1...50 { if f.step(position:Double(i)/100,rate:10,time:Double(i)/100,valid:true) != nil { hits+=1 } }
        XCTAssertEqual(hits,1); XCTAssertEqual(f.target,0,accuracy:0.001)
    }
    func testRecoveryRebasesWithoutJump() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        for i in 1...50 { _=f.step(position:0.4,rate:10,time:Double(i)/100,valid:true) }
        for i in 51...100 { _=f.step(position:0.15,rate:0,time:Double(i)/100,valid:true) }
        XCTAssertEqual(f.phase,.steering); XCTAssertEqual(f.target,0,accuracy:0.001)
    }
    func testTrackingLossRequiresCalibration() {
        var f=SteeringFilter(); f.calibrate(position:0,time:0)
        _=f.step(position:2,rate:0,time:1,valid:false)
        _=f.step(position:2,rate:0,time:2,valid:true)
        XCTAssertEqual(f.phase,.trackingLost); XCTAssertEqual(f.target,0)
        f.calibrate(position:2,time:3); XCTAssertEqual(f.phase,.steering)
    }
    func testOutOfOrderSamplesDoNotMove() {
        var f=SteeringFilter(); f.calibrate(position:0,time:10)
        XCTAssertNil(f.step(position:4,rate:12,time:9,valid:true)); XCTAssertEqual(f.target,0)
    }
    func testOldPlayerJSONKeepsIdentityAndDefaults() throws {
        let id=UUID()
        let json="{\"id\":\"\(id)\",\"name\":\"Saved player\",\"colorIndex\":1,\"handedness\":\"left\"}"
        let p=try JSONDecoder().decode(Player.self,from:Data(json.utf8))
        XCTAssertEqual(p.id,id); XCTAssertEqual(p.handedness,.left)
        XCTAssertFalse(p.standardFemale); XCTAssertEqual(p.standardSkin,2)
    }
    func testNewCharacterFieldsRoundTrip() throws {
        var p=Player(name:"Test",colorIndex:0); p.standardFemale=true; p.standardSkin=5
        XCTAssertEqual(try JSONDecoder().decode(Player.self,from:JSONEncoder().encode(p)),p)
    }
}
