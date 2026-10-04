# PROCESS — Claude × Blender (Male HeroBase chin + fingers)

**Why this exists:** Past Goal Mode runs soft-fail because the agent edits without seeing the mesh, remeshes, or chases the wrong % gate. Make Claude drive Blender with a **tight inspect → measure → local edit → render → eye-gate** loop.

---

## Ranked processes (for THIS Micro1e)

### 1) Best for Sonnet Desktop / Claude Code — Official Blender connector + vision loop
**Feasibility: highest** if you use Claude Desktop (or Claude Code with MCP).

- Anthropic tutorial: https://claude.com/resources/tutorials/using-the-blender-connector-in-claude  
- Blender Lab MCP (install via connector / Blender Lab page linked from that tutorial)  
- Needs: Claude Desktop, Blender **4.2+** (you already have ~5.2), open `.blend`, start BlenderMCP in Add-ons

**Loop:** open ArtDir blend → Claude inspects scene via MCP → runs `bpy` → viewport/render screenshots → Claude (vision) compares to plates → iterate until CHIN + FINGERS PASS.

**Pros:** Official, scene-aware, no copy-paste scripts.  
**Cons:** Needs Desktop + live Blender session; browser Claude.ai cannot do this.

### 2) Best for Cursor / Codex Goal Mode — Headless bpy scripts + fixed cameras + XOR sil gate
**Feasibility: high** (what you already partly run). Make it rigid.

Pattern:
1. Lock cameras (already in blend) + plate masks
2. Agent writes **one** scoped script: vertex group / vertex indices for chin OR hands only
3. `blender -b file.blend -P script.py` → write proofs to `proof/`
4. Numeric sil ≤5% + PNG sheet for Adnan eye-check
5. **One defect per script** (chin OR fingers, not both in one blind pass)

**Pros:** Deterministic, CI-able, works in Goal Mode without MCP.  
**Cons:** No live viewport; agent must read saved PNGs (or you eye-check).

### 3) Community MCP for any LLM client — `mcp-for-blender` (ahujasid)
https://github.com/ahujasid/blender-mcp · PyPI `mcp-for-blender`  
Addon socket + MCP server; Claude/Cursor execute code in open Blender.

Use when Official connector isn’t available in your client. **Don’t run two MCP bridges on one Blender.**

### 4) Typed-tool MCP agents (heavier)
- https://github.com/RFingAdam/mcp-blender — many tools + render→vision refine loop (often Ollama)  
- https://github.com/ra100/blender-claude-plugin — Claude Code skills + official Blender MCP first  
- https://github.com/Gaius114/blender-claude-mcp — lightweight HTTP + EEVEE→PNG visual loop  

Useful if you want auto refine sessions; overkill for a 2-defect micro goal unless MCP #1 fails.

### 5) Avoid for this pass
- Tripo / Rodin / text-to-3D regen (drifts from locked topology)  
- Voxel remesh / dyntopo “to fix fingers”  
- Chasing ≤2% silhouette (front/back plate conflict; Micro1e is ≤**5%**)  
- Blind multi-thousand-line Goal Mode without intermediate PNGs

---

## Recommended SOP (paste into Sonnet after MCP is up)

```
Mode: Blender MCP live on ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend

Phase A — INSPECT (no edits)
- Confirm object Body_M, height ~1.70m, A-pose
- Screenshot: front, left, right, 3q, chin closeup, both hands
- Overlay/compare mentally to male_body_bald.jpg + male_head_detail.jpg
- List vertex regions you’ll touch (chin band vs distal hands). Nothing else.

Phase B — CHIN only
- Topology-preserving: move existing verts / sculpt brushes with mask; NO remesh
- Re-screenshot chin side + front vs male_head_detail
- Stop when chin ball + under-chin→neck match plate; upper face may stay blank

Phase C — FINGERS only
- Separate soft-plastic fingers + thumb mass to match bald plate hands
- No mitt paddle, no claw spikes, no new topology islands if avoidable
- Re-screenshot hands front/3q/side

Phase D — VERIFY
- Full body front/back/left/right/3q + Unity FBX refresh
- Bald sil ≤5% front+back (report %). Do not invent 2%
- Confirm SHOULDERS/WAIST/ARMS_SIDE/FEET_SIDE unchanged
- Write proof/01e_chin_fingers_m.png + proof/03_unity_m.png + MALE_FORM_RESULTS.md
- Reply: GATE: MALE_FORM PASS|FAIL + failed lines
```

---

## Make Goal Mode feasible without MCP

Add a tiny harness under `ArtDir/hero/base_lock/tools/` (agent may create):

| Script | Job |
|--------|-----|
| `render_gate_views.py` | Always dumps the same camera set → `proof/_gate/` |
| `mask_chin.py` / `mask_hands.py` | Select/store vertex indices once; edits only those |
| `sil_xor.py` | XOR/union vs frozen bald masks; print % |

Rule for the agent: **edit → render_gate_views → read PNGs → decide next edit**. Never stack 5 sculpt theories without a render.

---

## Setup checklist (Official Claude ↔ Blender)

1. Claude Desktop → Customize → Connectors → add **Blender**  
   Guide: https://claude.com/resources/tutorials/using-the-blender-connector-in-claude  
2. Install Blender MCP add-on from Blender Lab (drag install link twice per guide)  
3. Open `HeroBase_Male_Silhouette.blend`  
4. Preferences → Add-ons → BlenderMCP → **Start MCP server**  
5. Paste Micro1e + this SOP  

Optional Cursor path: install community [ahujasid/blender-mcp](https://github.com/ahujasid/blender-mcp) / `mcp-for-blender` and point Cursor MCP config at it — same inspect→edit→screenshot loop.

---

## Sources (real)

- Official connector tutorial: https://claude.com/resources/tutorials/using-the-blender-connector-in-claude  
- ahujasid blender-mcp: https://github.com/ahujasid/blender-mcp  
- mcp-for-blender PyPI: https://pypi.org/project/mcp-for-blender/  
- ra100 blender-claude-plugin (skills + official MCP): https://github.com/ra100/blender-claude-plugin  
- RFingAdam mcp-blender (refine loop): https://github.com/RFingAdam/mcp-blender  
- Blender forum lesson (vision after every step): https://devtalk.blender.org/t/3d-agent-blender-ai-assistant-built-a-multi-agent-system-on-top-of-blenders-mcp-and-python-api-and-sharing-architecture-learnings-for-the-lab-discussion/44260  

---

## Bottom line for Adnan

1. **Wire Official Blender connector** (or ahujasid MCP) so Sonnet can see/edit the open scene.  
2. Force **chin then fingers** with a screenshot after each.  
3. Keep **headless gate scripts** as the binary proof for Unity + % — MCP is for steering; proofs still land in `proof/`.  
4. Never remesh; never 2% gate.
