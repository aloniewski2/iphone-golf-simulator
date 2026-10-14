        /// LOBBY_PARITY: Unity stills of the Ready stance under the same light rig the Swift lobby / locker uses, one per look case and framing, for the SceneKit side-by-side calibration.
        /// Run with graphics enabled:  LP_OUT=<dir> Unity -batchmode -projectPath <clone> -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.CaptureLobbyParity
        public static void CaptureLobbyParity()
        {
            int code = 0;
            try
            {
                string output = Environment.GetEnvironmentVariable("LP_OUT"); if (string.IsNullOrEmpty(output)) throw new ArgumentException("LP_OUT required");
                Directory.CreateDirectory(output);
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var camera = new GameObject("ParityCamera").AddComponent<Camera>(); camera.fieldOfView = 32; camera.nearClipPlane = .05f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.93f, .62f, .48f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.86f, .88f, .96f) * .21f;
                void Light(float intensity, Color color, float elevation, float azimuth)
                {
                    var l = new GameObject("ParityLight").AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.color = color;
                    float e = elevation * Mathf.Deg2Rad, a = azimuth * Mathf.Deg2Rad; l.transform.position = new Vector3(-Mathf.Sin(a) * Mathf.Cos(e) * 6, Mathf.Sin(e) * 6, Mathf.Cos(a) * Mathf.Cos(e) * 6); l.transform.LookAt(new Vector3(0, .8f, 0));
                }
                Light(1.05f, new Color(1, .92f, .82f), 40, 38); Light(.37f, new Color(.84f, .9f, 1), 18, -55); Light(.52f, new Color(1, .96f, .9f), 35, 160);
                const int W = 720, H = 1000;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); rt.antiAliasing = 4; rt.Create(); camera.targetTexture = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false); var previous = RenderTexture.active;
                var cases = new (string id, string skin, string shirt, string shorts, string shoes)[] {
                    ("default", null, null, null, null),
                    ("s1", "FFE0C4", null, null, null),
                    ("s2", "E6AE7E", "FF6B4A", "1E2A6E", "9EE63A"),
                    ("s3", "B7744A", "2F6FD6", "F4F0E6", "FFFFFF"),
                    ("s4", "74432C", "7BC043", "23344D", "E04F3A"),
                    ("s5", "3E2317", "F5C542", "FFFFFF", "2A2A2A")
                };
                var report = new StringBuilder();
                foreach (string sex in new[] { "Male", "Female" })
                    foreach (var c in cases)
                    {
                        var root = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + ".prefab"));
                        var driver = root.GetComponent<HeroTennisDriver>(); var look = root.GetComponent<MatchHeroLook>();
                        driver.ResolvePerformanceClips();
                        typeof(MatchHeroLook).GetMethod("OwnMaterials", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(look, null);
                        root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Racket_Classic").localScale *= TennisRules.HeroRacketScale;
                        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()) { skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true; }
                        if (c.skin != null) { ColorUtility.TryParseHtmlString("#" + c.skin, out var tone); look.SetSkin(tone); }
                        if (c.shirt != null || c.shorts != null || c.shoes != null) look.SetKit(c.shirt, c.shorts, c.shoes);
                        var ready = driver.slots.First(s => s.id == HeroTennisDriver.Clip.Ready).clip;
                        SamplePose(root, ready, 0);
                        var sources = Collect(root); foreach (var source in sources) Pose(root, source);
                        var bounds = new Bounds(); bool has = false;
                        foreach (var source in sources.Where(p => p.kind == "body" || p.kind == "face")) foreach (var v in source.pos) { if (!has) { bounds = new Bounds(v, Vector3.zero); has = true; } else bounds.Encapsulate(v); }
                        float neckY = FindBone(root.transform, "Neck").position.y; var headBounds = new Bounds(); bool hf = true;
                        foreach (var p in sources.Where(s => s.kind == "body" || s.kind == "face")) foreach (var v in p.pos) if (v.y > neckY + .02f) { if (hf) { headBounds = new Bounds(v, Vector3.zero); hf = false; } else headBounds.Encapsulate(v); }
                        var drawMeshes = new List<Mesh>();
                        foreach (var source in sources) { source.renderer.enabled = false; var node = new GameObject("UnityCPUSkin_" + source.name); node.transform.SetParent(root.transform, false); var mesh = Object.Instantiate(source.mesh); node.AddComponent<MeshFilter>().sharedMesh = mesh; node.AddComponent<MeshRenderer>().sharedMaterials = source.materials; drawMeshes.Add(mesh); }
                        float scale = 1.387f / bounds.size.y;
                        root.transform.localScale = Vector3.one * scale; root.transform.rotation = Quaternion.Euler(0, -.35f * Mathf.Rad2Deg, 0);
                        for (int part = 0; part < sources.Count; part++) { drawMeshes[part].vertices = sources[part].pos; drawMeshes[part].normals = sources[part].nrm; drawMeshes[part].RecalculateBounds(); }
                        float headCentre = headBounds.center.y * scale;
                        foreach (var framing in new[] { "body", "head" })
                        {
                            if (framing == "body") { camera.transform.position = new Vector3(0, .9f, 3.4125f); camera.transform.LookAt(new Vector3(0, .76f, 0)); }
                            else { camera.transform.position = new Vector3(0, headCentre + .04f, 1.15f); camera.transform.LookAt(new Vector3(0, headCentre, 0)); }
                            camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                            File.WriteAllBytes(Path.Combine(output, sex.ToLower() + "_" + c.id + "_" + framing + ".png"), tex.EncodeToPNG());
                        }
                        report.AppendLine($"{sex} {c.id} scale={scale:F5} headCentre={headCentre:F4} skin={c.skin} shirt={c.shirt} shorts={c.shorts} shoes={c.shoes}");
                        Object.DestroyImmediate(root);
                    }
                RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

