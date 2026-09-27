# Antigravity & AI Agent Guidelines: GameDev Hub & Direct Native MCP

You are connected to **GameDev Hub (Pirateslop Mission Control)** running locally at `http://localhost:3888`.
GameDev Hub acts as the single source of truth (SSoT) for game architecture, task queues, team synchronization (via Radmin VPN), and playtesting triage.

---

## ⚡ Fundamental Architecture: Direct Native MCP (No Proxying)

**STRICT RULE: NEVER PROXY ENGINE OR 3D OPERATIONS THROUGH THE HUB.**
There is NO `AI -> Hub MCP -> Unity/Blender MCP` chain.

- **Unity**: You interact with Unity **DIRECTLY** via native `unityMCP` tools (`execute_code`, `manage_gameobject`, `manage_components`, `manage_scene`, `run_tests`, `read_console`, `apply_text_edits`, `create_script`, `manage_prefabs`, etc.) and direct C# file edits.
- **Blender**: You interact with Blender **DIRECTLY** via native `blenderMCP` tools (`execute_blender_code`, `get_scene_info`, `get_viewport_screenshot`, `download_sketchfab_model`, `generate_hyper3d_model`, etc.).
- **GameDev Hub**: You interact with the Hub **DIRECTLY** via `gamedevHub` MCP tools (`get_project_context`, `get_task_envelope`, `claim_task`, `log_progress`, `propose_task_ready`, `propose_bug`, `propose_design_dilemma`) or CLI (`hub.bat`).
  - The Hub is solely the **Coordinator & SSoT** (Single Source of Truth), tracking who works on what, what decisions are active, and what needs testing.

---

## 🔄 Three-Phase Agent Execution Protocol

Whenever you are assigned or asked to implement a feature, fix a bug, or execute a task:

### Phase 1: Context Ingestion & Task Lock (GameDev Hub)
Before writing or modifying game code:
1. **Acquire the task envelope**:
   - Call `get_task_envelope(taskId)` or run `d:/projects/pirateslop hub/hub.bat task <taskId>`.
   - The envelope provides:
     - Task specifications & acceptance criteria.
     - Affected systems and scripts (`SYS-SHIP`, `SYS-WORLD`, etc.).
     - Active architectural decisions (SSoT).
     - Discussion thread history from You and Friend.
2. **Claim the task (Concurrency lock)**:
   - Call `claim_task(taskId, "Antigravity")` or `hub claim <taskId>`.
   - This immediately notifies the human developer and their friend via Radmin VPN that you are actively working on this task.

### Phase 2: Direct Execution (Unity & Blender Native Tools)
Perform the actual implementation directly using the dedicated tools:
1. **In Unity**:
   - Use `unityMCP` for inspecting GameObjects, managing components, reading console logs, and compiling scripts.
   - Use file tools (`view_file`, `replace_file_content`, `write_to_file`) for modifying Unity C# scripts.
   - Strictly adhere to active SSoT decisions from the task envelope.
2. **In Blender**:
   - Use `blenderMCP` for creating 3D meshes, running procedural Python scripts, inspecting materials, and capturing viewport screenshots.
3. *Zero proxy overhead* — all engine/DCC operations happen directly between you and Unity/Blender.

### Phase 3: Real-Time Sync & Handoff (Back to Hub)
Keep the team and mission control updated during and after your work:
1. **During execution (Progress Reporting)**:
   - For significant milestones or modified files, call:
     `log_progress(taskId, "Implemented component XYZ", ["file1.cs", "file2.cs"])` or `hub progress <taskId> <message>`.
   - This keeps the Hub's AI Radar active and updates the live team feed.
2. **On completion (Playtest Handoff)**:
   - When coding and checks are done:
     `propose_task_ready(taskId, "Implemented and validated in Unity. Ready for playtest.")` or `hub ready <taskId> [testResults]`.
   - This transitions the task to `NEEDS_HUMAN_TEST` and moves it into the **Needs Attention** queue for human playtesting.
3. **On runtime issues / bugs found**:
   - Call `propose_bug(title, severity, repro)` or `hub bug <title> <severity> <repro>` to register unexpected bugs in the Hub.

---

## 💡 Mechanic Design Mode (Brainstorming & Design Lab)

When the user asks to:
- *"Продумай механику..."* / *"Design mechanic..."*
- *"Спроектируй фичу..."* / *"Architect feature..."*
- *"Разработай концепт..."* / *"Brainstorm concept..."*

**DO NOT WRITE CODE OR MODIFY SCRIPTS YET!**
Follow this architectural design procedure:
1. Analyze the project structure (`SYS-SHIP`, `SYS-WORLD`, `SYS-PLAYER`, etc.) and active SSoT decisions.
2. Check for conflicts with existing gameplay decisions.
3. Formulate the technical specification (gameplay pros, technical risks, suggested subtasks, suggested SSoT rule).
4. Send the proposal to **Design Lab** using the MCP tool `propose_design_dilemma` (or CLI `hub propose <title> <desc>`):
   ```json
   {
     "title": "Название концепта",
     "description": "Архитектурное описание механики",
     "type": "AI_PROPOSAL",
     "affectedSystems": ["SYS-SHIP", "SYS-COMBAT"],
     "gameplayPros": ["Плюс 1", "Плюс 2"],
     "technicalRisks": ["Риск 1", "Риск 2"],
     "suggestedDecision": "Архитектурное правило для SSoT",
     "suggestedTasks": ["Подзадача 1", "Подзадача 2"]
   }
   ```
5. Confirm to the user:
   *"Я детально спроектировал механику и отправил спецификацию в Дизайн-Лаб (Design Lab). Вы с другом можете обсудить её в ветке и в 1 клик перевести в задачу на доске."*
