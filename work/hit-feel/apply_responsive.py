# Responsiveness edits (swing prediction, push motion, 120 Hz input, Game Mode). Exec'd by apply_mixed.py, which supplies
# `edit` and `ALTERNATIVES`. Whole new or untouched-elsewhere files (SteeringFilter.swift, SportsMotion.swift, InputRate.cs)
# are copied, not edited here.

N = 'Unity/Assets/Scripts/Game/NativeSportsSession.cs'
edit(N, 'public int skin=2,token,fps=60;', 'public int skin=2,token,fps=60,inputHz=60;')
edit(N, '            Application.targetFrameRate=m.external ? 60 : FrameRate.Target(m.fps,Screen.currentResolution.refreshRateRatio.value);',
        '            InputRate.Stop();\n            Application.targetFrameRate=m.external ? 60 : FrameRate.Target(m.fps,Screen.currentResolution.refreshRateRatio.value);')
edit(N, '            Ready=true; Emit(eventType,"Gameplay camera rendered");',
        '            Ready=true; Emit(eventType,"Gameplay camera rendered");\n'
        '            // The match is up and the scene has set its own frame rate: swing input goes to 120 Hz now if asked (InputRate).\n'
        '            if(tennis || golf) InputRate.Begin(gameObject,external,lastInputHz); else InputRate.Stop();')
edit(N, 'case "end": PresentationStinger.Stop();', 'case "end": InputRate.Stop(); PresentationStinger.Stop();')
edit(N, '        IEnumerator Load(Message m) {\n', '        IEnumerator Load(Message m) {\n            lastInputHz=m.inputHz;\n')
edit(N, '        IEnumerator Load(Message m) {', '        /// The match\'s requested input rate, kept for the display (re)attach that starts it.\n        int lastInputHz=60;\n        IEnumerator Load(Message m) {')
edit('Unity/Assets/Scripts/Game/NativeSportsSession.Hub.cs',
     '            Application.targetFrameRate = m.external ? 60 : FrameRate.Target(m.fps, Screen.currentResolution.refreshRateRatio.value);',
     '            InputRate.Stop();\n            Application.targetFrameRate = m.external ? 60 : FrameRate.Target(m.fps, Screen.currentResolution.refreshRateRatio.value);')

G = 'Unity/Assets/Scripts/Tennis/TennisFrameGovernor.cs'
edit(G, '''        float elapsed, settle = Settle;
        int frames, late;

        void Update()
        {
            if (Application.isEditor || Time.timeScale == 0) return;
            float dt = Time.unscaledDeltaTime;
            if (settle > 0) { settle -= dt; return; }
            float slot = 1f / Mathf.Max(30, Application.targetFrameRate);''',
'''        float elapsed, settle = Settle, sinceDrawn;
        int frames, late, inputRate;

        void Update()
        {
            if (Application.isEditor || Time.timeScale == 0) return;
            // With 120 Hz swing input (InputRate) only every other loop is drawn: judge the frames the TV gets, and start
            // the window again whenever the loop rate changes.
            if (inputRate != GolfArcade.Game.InputRate.Generation) { inputRate = GolfArcade.Game.InputRate.Generation; elapsed = 0; frames = late = 0; sinceDrawn = 0; settle = Mathf.Max(settle, 1); }
            sinceDrawn += Time.unscaledDeltaTime;
            if (!UnityEngine.Rendering.OnDemandRendering.willCurrentFrameRender) return;
            float dt = sinceDrawn; sinceDrawn = 0;
            if (settle > 0) { settle -= dt; return; }
            float slot = UnityEngine.Rendering.OnDemandRendering.renderFrameInterval / (float)Mathf.Max(30, Application.targetFrameRate);''')

S = 'GolfArcade/Unity/SportsSession.swift'
edit(S, '''    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
''', '''    var haptics = (UserDefaults.standard.object(forKey:"arcade.hapticsEnabled") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(haptics,forKey:"arcade.hapticsEnabled") }
    }
    /// Settings → Controls → Swing prediction (on unless turned off): a strong swing counts the moment its rotation peaks
    /// (SteeringFilter.peakConfirm, read when the next match or court direction starts).
    var swingPrediction = (UserDefaults.standard.object(forKey:"sports.swingPrediction") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(swingPrediction,forKey:"sports.swingPrediction") }
    }
    /// Settings → Controls → 120 Hz swing input (on unless turned off): on a TV the match loop reads the swing at 120 Hz
    /// while the TV is still drawn at 60 (Unity InputRate).
    var fastInput = (UserDefaults.standard.object(forKey:"sports.fastInput") as? Bool) ?? true {
        didSet { UserDefaults.standard.set(fastInput,forKey:"sports.fastInput") }
    }
    /// 120 only when it is on, this phone's panel reaches 120 (ProMotion) and Low Power Mode (which caps it at 60) is off.
    private var inputHz: Int {
        let panel = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .first { $0.session.role == .windowApplication }?.screen.maximumFramesPerSecond ?? 60
        return fastInput && panel >= 120 && !ProcessInfo.processInfo.isLowPowerModeEnabled ? 120 : 60
    }
''')
edit(S, '"fps":highFrameRate ? 120 : 60,', '"fps":highFrameRate ? 120 : 60,"inputHz":inputHz,')

M = 'GolfArcade/Unity/TennisMenu.swift'
edit(M, '        case .controls: ["controls", "range", "hand", "relock", "timing"]',
        '        case .controls: ["controls", "swingPrediction", "fastInput", "range", "hand", "relock", "timing"]')
edit(M, '''        case "controls": s.touch.toggle()
''', '''        case "controls": s.touch.toggle()
        case "swingPrediction": s.swingPrediction.toggle()
        case "fastInput": s.fastInput.toggle()
''')
edit('GolfArcade/Unity/IslandScreens.swift',
     '''        case "timing": return Item(title: "Swing timing check", detail: "Re-measure your TV's picture delay at the start of your next match.", control: .action("Run next match", danger: false))
''', '''        case "timing": return Item(title: "Swing timing check", detail: "Re-measure your TV's picture delay at the start of your next match.", control: .action("Run next match", danger: false))
        case "swingPrediction": return Item(title: "Swing prediction", detail: "A strong swing counts the moment it peaks, not a moment after. From your next match; turn off to compare.", control: .toggle(s.swingPrediction))
        case "fastInput": return Item(title: "120 Hz swing input", detail: "Reads your swing twice as often during a match on a TV, which stays at a steady 60. From your next match; turn off to compare.", control: .toggle(s.fastInput))
''')
edit('GolfArcade/Unity/ArcadeScreens.swift',
     '''        case "timing": ("Swing timing check", "Run next match")
''', '''        case "timing": ("Swing timing check", "Run next match")
        case "swingPrediction": ("Swing prediction", s.swingPrediction ? "On" : "Off")
        case "fastInput": ("120 Hz swing input", s.fastInput ? "On" : "Off")
''')

edit('GolfArcade/Unity/SportsInfo.plist', '''	<key>CADisableMinimumFrameDurationOnPhone</key>
	<true/>
''', '''	<key>CADisableMinimumFrameDurationOnPhone</key>
	<true/>
	<!-- iOS Game Mode: a game gets the CPU and GPU first and background work is held back (iOS 18+); the category is what
	     tells iOS this is a game, the two keys opt in explicitly (GCSupportsGameMode iOS 18, LSSupportsGameMode iOS 26). -->
	<key>GCSupportsGameMode</key>
	<true/>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.sports-games</string>
	<key>LSSupportsGameMode</key>
	<true/>
''')
