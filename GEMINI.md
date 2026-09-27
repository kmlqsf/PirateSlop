# Antigravity & Agent Guidelines: GameDev Hub Integration

You are connected to **GameDev Hub (Pirateslop Mission Control)** running locally at `http://localhost:3888`.
GameDev Hub acts as the single source of truth (SSoT) for game architecture, task queues, team synchronization (via Radmin VPN), and human playtesting triage.

## How to Interact with GameDev Hub

You have three seamless ways to interact with the Hub:
1. **MCP Server**: Use `gamedevHub` MCP tools (`get_project_context`, `get_task_envelope`, `claim_task`, `log_progress`, `propose_task_ready`, `propose_bug`, `query_decisions`, `convert_idea_to_task`).
2. **CLI Utility**: Run `d:/projects/pirateslop hub/hub.bat <command>` (e.g. `hub task TASK-001`, `hub claim TASK-001`, `hub ready TASK-001`).
3. **HTTP REST**: `POST http://localhost:3888/mcp/call` with payload `{"tool": "<tool_name>", "arguments": { ... }, "agent": "Antigravity"}`.

---

## Agent Task Lifecycle Protocol

Whenever the user instructs you to implement a feature, fix a bug, or work on a task:

### 1. Context Acquisition (Context Envelope)
Before writing or modifying game code:
- Obtain the task envelope: `get_task_envelope(taskId)` or `hub task <taskId>`.
- The envelope provides:
  - Task specifications & acceptance criteria.
  - Related systems and scripts.
  - Active architecture & design decisions (SSoT).
  - Discussion thread comments from You and Friend.

### 2. Task Claiming (Concurrency Lock)
- Lock the task: `claim_task(taskId, "Antigravity")` or `hub claim <taskId>`.
- This immediately updates the Hub and notifies the human developer and their friend via Radmin VPN that Antigravity is working on this task.

### 3. Implementation & Architecture Compliance
- Modify Unity C# scripts adhering strictly to the architectural decisions listed in the envelope.
- Never violate active SSoT decisions. If an architectural contradiction arises, use `propose_bug` or leave a note in the task thread.

### 4. Progress Reporting
- For significant milestones or modified files, call:
  `log_progress(taskId, "Implemented component XYZ", ["file1.cs", "file2.cs"])`.

### 5. Completion & Human Approval Handoff
- When coding is finished:
  Call `propose_task_ready(taskId, "Implemented and validated in Unity. Ready for playtest.")`.
- This transitions the task to `NEEDS_HUMAN_TEST` and puts it into the **Needs Attention** queue for the user to playtest.

---

### 6. Mechanic Design Mode (Brainstorming & Design Lab)
When the user asks to:
- *"Продумай механику..."* / *"Design mechanic..."*
- *"Спроектируй фичу..."* / *"Architect feature..."*
- *"Разработай концепт..."* / *"Brainstorm concept..."*

**DO NOT WRITE CODE OR MODIFY SCRIPTS YET!**
Follow this architectural design procedure:
1. Analyze the project structure (`SYS-SHIP`, `SYS-WORLD`, `SYS-PLAYER`, etc.) and active SSoT decisions.
2. Check for conflicts with existing gameplay decisions.
3. Formulate the technical specification (gameplay pros, technical risks, suggested subtasks, suggested SSoT rule).
4. Send the proposal to **Design Lab** using the MCP tool `propose_design_dilemma`:
   ```json
   {
     "title": "Название концепта",
     "description": "Архитектурное описание механики",
     "type": "AI_PROPOSAL",
     "affectedSystems": ["SYS-SHIP", "SYS-STATIONS"],
     "gameplayPros": ["Плюс 1", "Плюс 2"],
     "technicalRisks": ["Риск 1", "Риск 2"],
     "suggestedDecision": "Архитектурное правило для SSoT",
     "suggestedTasks": ["Подзадача 1", "Подзадача 2"]
   }
   ```
5. Confirm to the user:
   *"Я детально спроектировал механику и отправил спецификацию в Дизайн-Лаб (Design Lab). Вы с другом можете обсудить её в ветке и в 1 клик перевести в задачу на доске."*

