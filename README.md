# Precast Studio : Revit add-in for precast wall panelization (with AI commands)

[![build](https://github.com/Huynguyenhoang1/PrecastStudio/actions/workflows/build.yml/badge.svg)](https://github.com/Huynguyenhoang1/PrecastStudio/actions/workflows/build.yml)

A small but complete Revit add-in that turns a cast-in-place wall into **precast panels**:

1. Pick a straight wall in Revit.
2. Set panel rules (max/min width, joint, clearance to openings, mark prefix), or **type them in plain Vietnamese/English** and let Claude fill them in:
   > *"tấm tối đa 2.4m, khe 15mm, cách cửa 200mm, ký hiệu PW"*
3. See a live elevation preview and panel schedule: marks, widths, weight, lifting anchors.
4. Click **Create Parts**. The wall is converted to Revit **Parts**, divided at each joint with the joint width as the gap, and every part gets its panel mark.

```
Wall 12400 x 3200 x 200 mm, openings: [1000, 1900], [5800, 7300], [10200, 11100]
Rules: max 3000, min 600, joint 20, clearance 150

Mark     Start   Width  Openings  Weight kg  Anchors
W-01         0    2470         1       3007        2
W-02      2490    2460         0       3936        4
W-03      4970    2470         1       2377        2
W-04      7460    2455         0       3928        4
W-05      9935    2465         1       2999        2

Joints at: 2480, 4960, 7450, 9925 mm
```
*(output of `dotnet run --project src/PrecastStudio.Demo`, which runs without Revit)*

## Architecture

```
PrecastStudio.Core     net8.0          Pure domain logic, no Revit reference, fully unit-tested
 ├─ WallPanelizer          joint placement: equal panels, min/max width, never inside an opening + clearance
 ├─ PanelMarkGenerator     identical panels share a mark (mould reuse)
 └─ LiftingAnchorCalculator  weight, anchor count, 0.207·L two-point lift positions

PrecastStudio.AI       net8.0          Natural language -> PanelizationOptions
 └─ ClaudePanelCommandInterpreter  official Anthropic C# SDK + structured outputs (JSON schema);
                                   the model only *proposes* values, Core validation stays the source of truth

PrecastStudio.Revit    net8.0-windows  Revit 2025+ add-in (thin layer over Core)
 ├─ App / PanelizeWallCommand      ribbon button, selection filter, transaction
 ├─ WallReader                     Revit wall -> WallInfo (mm, openings projected on the wall axis)
 ├─ WallPartDivider                PartUtils.CreateParts / DivideParts + division gap + marks
 └─ PanelizeViewModel / Window     WPF + MVVM (CommunityToolkit.Mvvm), no Revit API in the ViewModel

PrecastStudio.Demo     net8.0          Console demo of Core + AI
tests/                 xUnit           17 tests: panelizer edge cases, marks, lifting, AI answer parsing
```

Design choices worth noting:

- **Revit is kept at the edge.** All geometry rules live in `Core`, which has no Revit dependency, so they are tested in milliseconds and could be reused for slabs or a web service.
- **AI never writes to the model directly.** Claude returns JSON constrained by a schema; it is parsed and validated by the same `PanelizationOptions.Validate()` the UI uses, and the user still reviews the preview before clicking *Create Parts*.
- **The AI is optional.** Without `ANTHROPIC_API_KEY` the AI box is simply hidden.
- **Clear failure messages.** When openings make a valid joint impossible, the user is told which range is blocked and what to change.

## Build & run

Requirements: .NET 8 SDK; Revit 2025 or newer to run the add-in. Revit API references come from the `Nice3point.Revit.Api` NuGet packages, so the solution builds without Revit installed.

```bash
dotnet build
dotnet test
dotnet run --project src/PrecastStudio.Demo
dotnet run --project src/PrecastStudio.Demo -- "tấm tối đa 2.4m, khe 15mm"   # needs ANTHROPIC_API_KEY
```

Install the add-in: copy `src/PrecastStudio.Revit/bin/Debug/net8.0-windows/` to
`%AppData%\Autodesk\Revit\Addins\2025\PrecastStudio\` and `PrecastStudio.addin` to `%AppData%\Autodesk\Revit\Addins\2025\`.

## Roadmap

- Slab / hollow-core panelization with the same `Core` engine
- Panel shop drawings (assembly views + dimensions) and an Excel panel schedule
- Connection and lifting-anchor families placed on each part
- Revit 2022–2024 (.NET Framework 4.8) build target

## Author

**Nguyen Hoang Huy** — C# Revit API developer · huynguyenhoang.nuce@gmail.com

Built with AI coding tools; every change was reviewed, refactored and covered by tests by hand.
