# Dedup Mode Design

**Date:** 2026-04-29
**Status:** Approved

## Summary

A standalone "Find Duplicates" mode accessible from a new mode-selector screen shown at app start. The user picks a folder, scans for exact duplicates, reviews each group (auto-selected keeper, manual override available), confirms deletions, and the copies are sent to the Windows Recycle Bin. No file organization is involved. The existing wizard shell (nav buttons, progress bar, step title) is reused with new dedup-specific step views.

---

## 1. Entry Point — Mode Selector

`WizardStep.ModeSelector` is the new initial step (replaces landing on `Folder` directly).

**Layout:** two side-by-side cards inside the existing shell.

| Card | Title (DE / EN) | Action |
|---|---|---|
| Organize Files | Dateien organisieren | Navigate to `WizardStep.Folder` |
| Find Duplicates | Duplikate entfernen | Navigate to `WizardStep.DedupScan` |

**Shell behavior at this step:**
- Back button hidden
- Next button hidden
- Step progress bar hidden or at 0
- Step title: `WizardStepModeSelectorTitle` resource key

---

## 2. Dedup Flow Steps

Three new `WizardStep` values are added after the existing five:

```
DedupScan    — folder picker + scan trigger
DedupReview  — group-by-group review: auto keeper, manual override, confirm/skip per group
DedupExecute — Recycle Bin execution + results summary
```

Navigation: `ModeSelector` → `DedupScan` → `DedupReview` → `DedupExecute` → `ModeSelector`.  
Back works at every step within the dedup sub-flow.

---

## 3. DedupScan Step

**Purpose:** choose a root folder and trigger the duplicate scan.

**UI (two-column card):**
- Left: folder path display (read-only) + "Browse…" button (`FolderBrowserDialog`)
- Right:
  - Checkbox: "Include subfolders" (default: on)
  - Numeric field: "Maximum files to scan" (default: 10 000)
  - "Scan for Duplicates" primary button → `DedupScanCommand`

**Scan execution:**
- Walks the directory tree to produce `IReadOnlyList<ScannedFile>` — reuses whatever file-walking infrastructure is extractable from `OrganizationWorkflowService` (a thin wrapper or direct `Directory.EnumerateFiles` if no suitable seam exists)
- Calls `DuplicateDetectionService.DetectAsync` with a minimal `DuplicatePolicy { EnableExactDuplicateDetection = true }`
- Progress reported via existing `WorkflowProgress` / shell `StatusMessage` + `ProgressBar`
- Cancel available via `CancelCurrentWorkCommand`

**On completion:**
- Status: "Found N duplicate groups (M files)"
- Shell Next button enables
- Results stored as `ObservableCollection<DedupGroupViewModel>` on the VM

**Empty result:** Next still enabled; `DedupReview` shows an empty-state message.

---

## 4. DedupReview Step

**Purpose:** the user resolves each duplicate group — picking a keeper and confirming deletion of copies.

**Layout (two-column, full height):**

**Left — group list:**
- `ListBox` of `DedupGroupViewModel` items
- Each row: keeper filename + "N copies · X MB wasted"
- Badge tally at top: "12 groups · 3 resolved · 2 skipped"
- Selecting a group populates the right panel

**Right — group detail:**
- Header: "Keep one, remove the rest"
- One card per file in the group:
  - Full relative path, size, last-modified date
  - Auto-selected keeper: green **Keep** pill badge (scoring: path depth, creation date, filename richness — existing `DuplicateDetectionService` ordering)
  - Other copies: muted **Remove** pill badge
  - "Set as keeper" button on each non-keeper card
- Two action buttons per group:
  - **"Confirm"** (primary) — marks group as resolved (no files touched yet), auto-advances to next unresolved group
  - **"Skip this group"** (secondary) — marks skipped, advances

**Bottom bar:**
- **"Resolve all automatically"** — applies auto-selection to every unresolved group in one shot
- Shell Next button enables when every group is resolved or skipped

---

## 5. DedupExecute Step

**Purpose:** execute all groups confirmed in DedupReview and show a summary. No files are touched in DedupReview — it is purely a decision screen. All Recycle Bin operations happen here, consistent with the app's preview-first principle.

**On entry:** immediately begins processing remaining confirmed groups.

**During execution:**
- Shell progress bar + "Sending file N of M to Recycle Bin…" status
- Cancel stops mid-run; already-recycled files remain in Recycle Bin (safe)

**Results summary card:**
- "N files sent to Recycle Bin"
- "M groups skipped"
- "X MB freed"
- Per-file errors listed as warnings (non-blocking)

**Actions:**
- **"Scan another folder"** → `ModeSelector`
- **"Open Recycle Bin"** → `Process.Start("shell:RecycleBinFolder")`

---

## 6. New ViewModels

### DedupGroupViewModel
```
CanonicalRelativePath  string
Copies                 ObservableCollection<DedupFileItemViewModel>
IsResolved             bool
IsSkipped              bool
WastedBytes            long
DisplayLabel           string  ← computed: filename + "N copies · X MB"
```

### DedupFileItemViewModel
```
FullPath        string
RelativePath    string
SizeBytes       long
ModifiedUtc     DateTimeOffset
IsKeeper        bool   ← toggled by "Set as keeper"
```

Both are lightweight — no MVVM toolkit attributes needed unless property-change notification is required for `IsKeeper` / `IsResolved`.

---

## 7. New Infrastructure — IRecycleBinService

**Interface (Application layer):**
```csharp
public interface IRecycleBinService
{
    Task SendToRecycleBinAsync(string fullPath, CancellationToken ct);
}
```

**Implementation (Infrastructure layer):** uses `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` with `RecycleOption.SendToRecycleBin`. No new NuGet dependency — available in the Windows/.NET target already used by this project.

**Error handling:** throws on failure; callers wrap in try/catch and collect errors for the summary.

**DI registration:** added in `ServiceCollectionExtensions.cs`.

---

## 8. WizardStep Enum Changes

```csharp
public enum WizardStep
{
    ModeSelector   = -1,   // new — initial step
    Folder         =  0,
    Strategy       =  1,
    Rules          =  2,
    Preview        =  3,
    ExecuteRollback =  4,
    DedupScan      =  5,   // new
    DedupReview    =  6,   // new
    DedupExecute   =  7,   // new
}
```

`MainWindowViewModel` initial step changes from `Folder` to `ModeSelector`. Shell XAML `DataTrigger` blocks gain entries for the three new steps and for `ModeSelector` (title, body, template). Back/Next visibility logic gains guards for the dedup sub-flow.

---

## 9. Localization

All new strings follow the existing `DynamicResource` pattern in `Strings.de-DE.xaml` (primary) and `Strings.en-US.xaml`.

New resource keys (representative — full list determined during implementation):

```
WizardStepModeSelectorTitle
ModeOrganizeTitle / ModeOrganizeBody / ModeOrganizeButton
ModeDedupTitle / ModeDedupBody / ModeDedupButton
WizardStepDedupScanTitle / WizardStepDedupScanBody
WizardStepDedupReviewTitle / WizardStepDedupReviewBody
WizardStepDedupExecuteTitle / WizardStepDedupExecuteBody
DedupScanButton / DedupBrowseButton
DedupIncludeSubfolders / DedupMaxFiles
DedupStatusFound / DedupStatusEmpty
DedupKeepPill / DedupRemovePill / DedupSetAsKeeper
DedupConfirmGroup / DedupSkipGroup / DedupResolveAll
DedupSummaryFiles / DedupSummaryGroups / DedupSummarySkipped / DedupSummaryFreed
DedupOpenRecycleBin / DedupScanAnother
```

---

## 10. What Does NOT Change

- `WizardFolderStepView`, `WizardStrategyStepView`, `WizardRulesStepView`, `WizardPreviewStepView`, `WizardExecuteStepView`
- `OrganizationWorkflowService`, `PlanExecutionService`, `RollbackService`, `ExecutionJournal`
- `DuplicateDetectionService`, `IFileHashProvider` — reused as-is
- All existing tests — no behavior changes to existing paths

---

## 11. Security & Safety Constraints

- Recycle Bin is the undo mechanism — no journal required for dedup deletions
- Cancel mid-execute is always safe (already-recycled files stay in Recycle Bin)
- No file is touched until the user explicitly confirms a group or clicks "Resolve all automatically"
- Paths are validated to remain inside the selected dedup root (same `PathSafetyService` contract)
