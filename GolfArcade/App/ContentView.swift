import SwiftUI

struct ContentView: View {
    var body: some View {
        ZStack {
            LinearGradient(
                colors: [Color(red: 0.03, green: 0.16, blue: 0.12), .black],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()

            VStack(spacing: 16) {
                Image(systemName: "figure.golf")
                    .font(.system(size: 64))
                    .foregroundStyle(.mint)
                Text("Golf Arcade")
                    .font(.largeTitle.bold())
                Text("Your body is the controller.")
                    .foregroundStyle(.secondary)
            }
            .foregroundStyle(.white)
        }
    }
}

#Preview {
    ContentView()
}

