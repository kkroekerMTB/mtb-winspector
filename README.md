# Winspector

Internal Windows desktop inspector for debugging FlaUI selectors in .NET Framework 4.8 WinForms applications. Built with WPF, .NET 10, and FlaUI UIA2.

## Run

Install the .NET 10 SDK on Windows, then:

```powershell
dotnet build src/Winspector.slnx
dotnet run --project src/Winspector.App
```

Select an application from the list. All UI Automation top-level windows belonging to that process appear in the **Raw view** tree. Expand branches or hover over the target to reveal a control. Bundled Google Material Icons identify windows, containers, common control types, and provider-reported Custom controls; hover over an icon to see its type and class. Selecting a node highlights its screen bounds and displays selector properties and ancestry.

- **Ctrl+Shift+F** or **Freeze selection** stops hover selection so you can inspect properties. If the hotkey is occupied, the checkbox remains available.
- **Pause live updates** holds the tree; **Refresh tree** requests one snapshot even while paused.
- **Reload applications** updates the process list.
- Added/changed nodes are marked until the next complete snapshot; the status line reports added/removed counts.
- Structure events trigger throttled refreshes; a 1.5-second reconciliation also catches property changes and missed events.
- Provider errors retain the last complete tree and appear in the status line.

Hidden controls may disappear from the automation tree or remain marked **offscreen**. Offscreen is not an exact equivalent of WinForms Visible. Runtime IDs preserve selection within an inspection session and are not reusable test selectors. The tool reads UI Automation data; it cannot expose information a custom control's provider does not publish.

The inspector runs at ordinary user privilege. To inspect an elevated application, run it at the same privilege level.

## Tests

```powershell
dotnet test src/Winspector.slnx
```

**Winspector.Core.Tests** verifies tree reconciliation, node/expansion/selection preservation, visibility updates, reparenting, and invalid snapshot handling.

**Winspector.Windows.Tests** launches the included .NET Framework 4.8 WinForms fixture to verify real UIA2 inspection, dynamic controls, additional windows, paused/manual refresh, and process exit. It requires an unlocked interactive Windows desktop and .NET Framework 4.8. Tests also exercise the actual WPF window, hover/freeze synchronization, nested scrolling, and selection preservation during reparenting/removal. Tests close the processes they launch and restore the cursor position.

Run just the platform-independent core tests:

```powershell
dotnet test src/Winspector.Core.Tests
```

For manual testing:

```powershell
dotnet run --project src/Winspector.WinFormsFixture
```

Select the fixture in Winspector, then use Add control, Show / hide, Remove control, and Open dialog. Verify the tree changes, surviving expansion/selection stays intact, and hover/tree highlighting aligns on monitors with different DPI settings.

## Structure

- src/Winspector.App: WPF presentation, native overlay/hotkey support, dedicated MTA UIA2 worker.
- src/Winspector.Core: detached snapshots and UI-thread tree reconciliation.
- src/Winspector.Core.Tests: xUnit behavioral tests.
- src/Winspector.Windows.Tests: xUnit Windows desktop integration tests.
- src/Winspector.WinFormsFixture: .NET Framework 4.8 test application.
- docs/milestone-one.md: the agreed milestone scope.

This milestone reconciles complete snapshots rather than refreshing individual branches. Traversal is bounded at 10,000 nodes / 100 levels and reports a limit error rather than presenting a partial tree. UIA provider calls can stall; they stay off the GUI thread and do not block closing the inspector indefinitely.

UIA3 and selector generation/validation are later milestones.

Icon sources and licensing are recorded in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).