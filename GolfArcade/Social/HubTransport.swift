import Foundation

/// One connection to the Club Hub service. A protocol so tests run the whole hub over an in-memory service.
@MainActor protocol HubTransport: AnyObject {
    var onOpen: (() -> Void)? { get set }
    var onMessage: ((Data) -> Void)? { get set }
    /// The socket closed or failed to open. The code is the WebSocket close code when there is one (4401 = rejected, 4429 = too fast).
    var onClose: ((Int?) -> Void)? { get set }
    func connect(_ url: URL)
    func send(_ data: Data)
    func close()
}

/// URLSession's WebSocket. Callbacks arrive off the main thread and hop onto it; a stale socket's late callbacks are ignored.
@MainActor final class WebSocketHubTransport: NSObject, HubTransport, URLSessionWebSocketDelegate {
    var onOpen: (() -> Void)?
    var onMessage: ((Data) -> Void)?
    var onClose: ((Int?) -> Void)?
    private var session: URLSession?
    private var task: URLSessionWebSocketTask?
    private var generation = 0

    func connect(_ url: URL) {
        close()
        generation += 1
        let config = URLSessionConfiguration.default
        config.waitsForConnectivity = false; config.timeoutIntervalForRequest = 15
        let session = URLSession(configuration: config, delegate: self, delegateQueue: nil)
        let task = session.webSocketTask(with: url)
        self.session = session; self.task = task
        task.resume()
        receive(task, generation)
    }

    func send(_ data: Data) {
        guard let task, let text = String(data: data, encoding: .utf8) else { return }
        task.send(.string(text)) { _ in }
    }

    func close() {
        generation += 1
        task?.cancel(with: .normalClosure, reason: nil); task = nil
        session?.invalidateAndCancel(); session = nil
    }

    private func receive(_ task: URLSessionWebSocketTask, _ generation: Int) {
        task.receive { [weak self] result in
            Task { @MainActor [weak self] in
                guard let self, self.generation == generation else { return }
                switch result {
                case .success(let message):
                    switch message {
                    case .string(let text): self.onMessage?(Data(text.utf8))
                    case .data(let data): self.onMessage?(data)
                    @unknown default: break
                    }
                    self.receive(task, generation)
                case .failure:
                    // (a refused or dropped socket: didCompleteWithError / didClose report it with the code)
                    break
                }
            }
        }
    }

    nonisolated func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didOpenWithProtocol protocol: String?) {
        Task { @MainActor [weak self] in
            guard let self, self.task === webSocketTask else { return }
            self.onOpen?()
        }
    }
    nonisolated func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask, didCloseWith closeCode: URLSessionWebSocketTask.CloseCode, reason: Data?) {
        let code = closeCode.rawValue
        Task { @MainActor [weak self] in
            guard let self, self.task === webSocketTask else { return }
            self.finish(code)
        }
    }
    nonisolated func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        guard error != nil else { return }
        Task { @MainActor [weak self] in
            guard let self, self.task === task else { return }
            self.finish(nil)
        }
    }
    private func finish(_ code: Int?) {
        task = nil; session?.invalidateAndCancel(); session = nil; generation += 1
        onClose?(code)
    }
}
