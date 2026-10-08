# Winspector first milestone

Build a Windows desktop inspector for FlaUI tests against .NET Framework 4.8 WinForms applications.

- WPF on .NET 10, with FlaUI UIA2 initially; UIA3 is a later backend option.
- Choose a running target process and include all its top-level windows.
- Display its UI Automation raw tree and basic selector properties plus ancestry.
- Use bundled Google Material Icons PNGs for windows, containers, control types, and provider-reported Custom controls, with type/class tooltips.
- Track additions, removals, shown/hidden controls, and property changes.
- Combine structure-change events with periodic reconciliation and manual refresh.
- Preserve surviving nodes, expanded branches, and selection during updates.
- Briefly mark added/changed nodes and report removals.
- Synchronize hover with tree selection and reveal the hovered node.
- Highlight the selected element without taking focus or blocking mouse input.
- Freeze hover selection with a global hotkey; pause live updates independently.
- Keep provider queries on a background thread; retain the last complete tree on failure.
- Handle target changes and process exit without showing stale target data.
- Include xUnit tests, a solution under src, build/review the implementation, and commit it.

Selector generation/validation, UIA3, and branch-only refresh are subsequent milestones.
This milestone reconciles full snapshots; Windows providers may omit hidden controls.
