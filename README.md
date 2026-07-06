**PROJECT:** NoteApp

**TECH STACK:** C# / WPF / .NET 8 / PdfSharp / Newtonsoft.Json

**REPO:** <https://github.com/btother/NoteApp>

NoteApp

WPF Handwriting Note Application - Developer Documentation & README

# 1\. Project Overview

NoteApp is a WPF desktop application for handwritten digital notes using a stylus or pen. It organizes notes in a four-level hierarchy and renders each page as a lined DIN-A4 canvas via WPF InkCanvas. Annotations are stored as ISF files, the notebook structure as JSON, and exports are produced as PDF via PdfSharp.

Repository: <https://github.com/btother/NoteApp>

## Hierarchy

- Notebook → top-level container with a save path on disk
- Category → folder shown in the left TreeView sidebar
- Document (Subcategory with IsDocument=true) → appears as a tab in the tab bar
- Page → individual lined canvas within a document, navigated with ◀ / ▶ buttons

# 2\. Tech Stack & Dependencies

| **Dependency**                     | **Purpose**                                    |
| ---------------------------------- | ---------------------------------------------- |
| .NET 8 / C# / WPF                  | Application framework                          |
| PdfSharp                           | PDF creation and export; lined page background |
| Newtonsoft.Json                    | JSON serialization of notebooks.json           |
| Microsoft.Win32 (OpenFolderDialog) | Folder picker for notebook save path           |
| System.Windows.Ink                 | InkCanvas, StrokeCollection, ISF file format   |

# 3\. Project Structure (file tree)

NoteApp/

├── App.xaml / App.xaml.cs

├── AssemblyInfo.cs

├── NoteApp.csproj

├── Converters/

│ └── Converters.cs (NullToVisibilityConverter, BoolToVisibilityConverter)

├── Models/

│ └── Notebook.cs (Notebook, Category, NotePage, PageType, StrokeData, PointData)

├── Services/

│ └── PdfService.cs (PDF creation, ISF save/load, export, autosave)

├── Themes/

│ └── DarkTheme.xaml (ModernButton, GhostButton styles, dark color palette)

└── Views/

├── MainWindow.xaml (main UI layout)

├── MainWindow.xaml.cs (all UI logic, ~941 lines)

└── RenameDialog.xaml/.cs (simple input dialog)

# 4\. Architecture & Data Model

## Models (Notebook.cs)

| **Class**              | **Key Properties**                                                                                                    | **Notes**                                                 |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| Notebook               | Name, SavePath, ObservableCollection&lt;Category&gt; Categories                                                       | Top-level container                                       |
| Category               | Name, bool IsDocument, ObservableCollection&lt;Category&gt; SubCategories, ObservableCollection&lt;NotePage&gt; Pages | IsDocument=false → folder; IsDocument=true → document/tab |
| NotePage               | Name, PageType (Lined/ExternalPdf), PdfPath, IsfPath                                                                  | PageType: Lined or ExternalPdf                            |
| StrokeData / PointData | Legacy                                                                                                                | Kept for JSON compatibility; not used for stroke storage  |

## Storage Layout

%APPDATA%/NoteApp/notebooks.json → structure only (no strokes)

%APPDATA%/NoteApp/pages/{guid}.isf → handwriting strokes per page (ISF format)

%APPDATA%/NoteApp/pages/{guid}.pdf → lined background per page

Notebook.SavePath/{NotebookName}/{CategoryName}/{DocumentName}.pdf → exported multi-page PDF

# 5\. Feature Documentation (all features)

## Notebook Management

- Create notebook: RenameDialog + OpenFolderDialog → creates Notebook and persists to JSON
- Rename / Delete notebook with MessageBox confirmation
- Switch notebook: auto-opens first Category → first Document → first Page

## Category & Document Management

- \+ Kategorie: creates Category (IsDocument=false) as folder in TreeView
- \+ Unterkategorie: creates Category (IsDocument=true) as a document tab and auto-creates first page
- Rename / Delete with confirmation; deletion is recursive
- ContextMenu on TreeView items and document tabs

## Page Navigation

- Documents contain multiple pages
- ◀ / ▶ buttons navigate between pages within the active document
- "Seite X / Y" indicator shows current position
- \+ Seite adds a new lined page at the end of the active document

## Drawing Tools

- Pen: InkCanvasEditingMode.Ink with configurable thickness and color
- Eraser: InkCanvasEditingMode.EraseByPoint
- Thickness slider: 1-20 px
- Color palette: Black, DarkBlue, DarkRed, DarkGreen, Purple, DarkOrange, White, Gray
- Strokes saved immediately on StrokeCollected and StrokeErased events

## Canvas & Layout

- Always maximized (WindowState=Maximized)
- Page fills available width dynamically (UpdateCanvasSize)
- DIN-A4 aspect ratio maintained (794:1123)
- Lined background: red margin line + blue ruled lines
- Sidebar toggle: ◀◀ hides both sidebars; ☰ hamburger opens popup

## Sidebar Popup (Hamburger Mode)

- When sidebars are hidden, ☰ appears in toolbar
- Popup (≈440 px wide) shows Notebooks and Categories side by side
- Popup closes on outside click (StaysOpen=False)
- Popup lists sync from main lists each time it opens

## Save & Export

- AutoSave every 1 minute via DispatcherTimer
- Save on navigation: strokes saved when switching notebook/category/document/page
- Save on close: strokes saved when window closes
- Export: active document exported as multi-page PDF via SaveFileDialog
- PDF import: stub implemented (ExternalPdf page type); background rendering not yet done

# 6\. Changelog (all changes made during development)

## 1\. ALWAYS FULLSCREEN

- Added WindowState="Maximized" to Window element in MainWindow.xaml

## 2\. FULL WIDTH PAGE

- Added SizeChanged="CanvasScroller_SizeChanged" to ScrollViewer
- New method UpdateCanvasSize(): calculates width from CanvasScroller.ActualWidth - 80 (margins), sets PageBorder/LineCanvas/MainInkCanvas width+height dynamically
- DrawLinedBackground() changed to use dynamic MainInkCanvas.Width/Height instead of hardcoded 794/1123
- LoadPage() now calls UpdateCanvasSize() before DrawLinedBackground()

## 3\. FULLSCREEN MODE (HAMBURGER)

- Added HamburgerButton (☰) to toolbar, Visibility=Collapsed by default
- Added ToggleSidebarButton ("◀◀ Seitenleisten ausblenden") to toolbar
- Added SidebarPopup (Popup control) to root Grid, anchored to HamburgerButton
- New ColumnDefinitions with x:Name: ColNotebooks, ColSplitter1, ColCategories, ColSplitter2
- SetSidebarVisibility(bool): sets ColNotebooks.MinWidth=0 BEFORE Width=0 (fix for dead space bug), collapses/shows panels and splitters
- HamburgerButton_Click: opens popup, syncs lists
- BuildPopupTreeItem(): creates fresh StackPanel headers (fix for WPF Visual Tree crash)
- PopupNotebook_Click, PopupCategoryTree_SelectedItemChanged

## 4\. AUTOSAVE EVERY MINUTE

- AutoSaveTimer interval changed from 10 minutes to 1 minute
- AutoSaveTimer_Tick: calls SaveCurrentPageStrokes() first, then AutoSaveNotebook for all notebooks

## 5\. SAVE ON NAVIGATION

- SelectNotebook(): calls SaveCurrentPageStrokes() at start
- CategoryTree_SelectedItemChanged(): calls SaveCurrentPageStrokes() at start
- SelectPage(): calls SaveCurrentPageStrokes() at start

## 6\. ISF-BASED STORAGE (strokes in ISF, not JSON)

- Removed StrokeData list from NotePage model
- Added IsfPath property to NotePage
- PdfService.SaveStrokes(): saves StrokeCollection as .isf via FileStream
- PdfService.LoadStrokes(): loads StrokeCollection from .isf via FileStream
- PdfService.DeleteStrokes(): deletes .isf file
- SaveCurrentPageStrokes() now calls PdfService.SaveStrokes()
- LoadPage() now calls PdfService.LoadStrokes()
- notebooks.json now contains structure only (no stroke data)

## 7\. DOCUMENT/TAB CONCEPT (Subcategory = Document = Tab)

- Category model: added IsDocument bool, Pages collection
- AddSubCategory_Click: creates Category (IsDocument=true) + first NotePage automatically
- RefreshDocumentTabs(): builds tab bar from SubCategories
- CreateDocumentTab(): creates styled Border tab
- SelectDocument(): loads first/current page of document
- HighlightActiveDocumentTab(): visual feedback for active tab
- \_activeDocument state variable added
- \+ Seite button: adds page to active document (not as new tab)
- PrevPage_Click / NextPage_Click: navigate within document
- PageIndicatorText: "Seite X / Y" display
- TabBar XAML: left ScrollViewer for tabs + right StackPanel for navigation buttons

## 8\. AUTO-OPEN ON STARTUP

- AutoOpenFirst(): called in Loaded event, opens \_notebooks\[0\] → Categories\[0\] → SubCategories\[0\]
- AutoOpenFirstInNotebook(nb): called in SelectNotebook(), opens first category+document of new notebook

## 9\. EMOJI FIX IN TREEVIEW

- BuildTreeItem(): uses "\\U0001f4c1" (single backslash) instead of "\\\\U0001f4c1"
- CreateDocumentTab(): uses "\\U0001f4c4" (single backslash)
- BuildPopupTreeItem(): uses "\\U0001f4c1" (single backslash)

## 10\. MESSAGEBOX STRING ESCAPING FIX

- All interpolated strings with variable names now use \\" instead of " inside \$"..." strings
- Example: \$"Notizbuch \\"{nb.Name}\\" wurde erstellt."

## 11\. SIDEBAR DEAD SPACE FIX

- SetSidebarVisibility(false): sets ColNotebooks.MinWidth=0 and ColCategories.MinWidth=0 BEFORE setting Width=0
- SetSidebarVisibility(true): restores MinWidth to 150/160 when re-showing

## 12\. WPF VISUAL TREE CRASH FIX (Hamburger Popup)

- BuildPopupTreeItem() creates a completely new StackPanel + TextBlock for each item
- Does NOT reuse original.Header (which is a UIElement that can only exist once in the Visual Tree)
- MouseLeftButtonUp handler sets \_activeCategory, calls RefreshPageTabs(), closes popup

# 7\. Current State / Known Limitations

- PDF import (ImportPdf_Click) is implemented as stub - actual PDF rendering on canvas not yet done
- No undo/redo functionality
- No text input (only handwriting via InkCanvas)
- No zoom functionality
- RenameDialog is a simple custom dialog (Views/RenameDialog.xaml)
- StrokeData/PointData classes kept in Notebook.cs for JSON backwards compatibility but no longer used
- ExternalPdf page type exists but rendering of external PDF background not implemented

# 8\. For AI Assistants: How to continue working on this project

## Before You Start

- Always fetch latest code from <https://github.com/btother/NoteApp> before making changes
- Main files to edit: Views/MainWindow.xaml, Views/MainWindow.xaml.cs, Services/PdfService.cs, Models/Notebook.cs

## Active State & Rules

- Active state is tracked via: \_activeNotebook, \_activeCategory, \_activeDocument, \_activePage
- Always call SaveCurrentPageStrokes() before switching any active state
- Always call UpdateCanvasSize() before DrawLinedBackground() when showing a page

## WPF Do's and Don'ts

- When adding new TreeView items, always create NEW UIElements - never reuse existing ones (Visual Tree rule)
- ColNotebooks.MinWidth must be set to 0 before Width=0 to avoid dead space
- The Popup (SidebarPopup) uses StaysOpen=False - it closes on outside click automatically

## C# Syntax Reminders

| **Wrong**            | **Correct**              |
| -------------------- | ------------------------ |
| f"text {var}"        | \$"text {var}"           |
| Math.max(a, b)       | Math.Max(a, b)           |
| \$"Name "{var}" end" | \$"Name \\"{var}\\" end" |
| "\\\\U0001f4c1"      | "\\U0001f4c1"            |

## Key Method Reference

| **Method**                   | **When to Call**                                        |
| ---------------------------- | ------------------------------------------------------- |
| UpdateCanvasSize()           | Call when canvas becomes visible or window resizes      |
| DrawLinedBackground()        | Call after UpdateCanvasSize() - uses dynamic dimensions |
| SaveCurrentPageStrokes()     | Call before any navigation change                       |
| AutoOpenFirstInNotebook(nb)  | Call after SelectNotebook() to auto-open first content  |
| RefreshCategoryTree()        | Call after adding/removing/renaming categories          |
| RefreshDocumentTabs()        | Call after adding/removing/renaming documents           |
| HighlightActiveDocumentTab() | Call after SelectDocument() to update tab colors        |
| RefreshPageIndicator()       | Call after page navigation to update "Seite X / Y"      |PROJECT: NoteApp
TECH STACK: C# / WPF / .NET 8 / PdfSharp / Newtonsoft.Json
REPO: https://github.com/btother/NoteApp
NoteApp
WPF Handwriting Note Application — Developer Documentation & README

1. Project Overview
NoteApp is a WPF desktop application for handwritten digital notes using a stylus or pen. It organizes notes in a four-level hierarchy and renders each page as a lined DIN-A4 canvas via WPF InkCanvas. Annotations are stored as ISF files, the notebook structure as JSON, and exports are produced as PDF via PdfSharp.

Repository: https://github.com/btother/NoteApp

Hierarchy
•	Notebook → top-level container with a save path on disk
•	Category → folder shown in the left TreeView sidebar
•	Document (Subcategory with IsDocument=true) → appears as a tab in the tab bar
•	Page → individual lined canvas within a document, navigated with ◀ / ▶ buttons
2. Tech Stack & Dependencies
Dependency	Purpose
.NET 8 / C# / WPF	Application framework
PdfSharp	PDF creation and export; lined page background
Newtonsoft.Json	JSON serialization of notebooks.json
Microsoft.Win32 (OpenFolderDialog)	Folder picker for notebook save path
System.Windows.Ink	InkCanvas, StrokeCollection, ISF file format
3. Project Structure (file tree)
NoteApp/
├── App.xaml / App.xaml.cs
├── AssemblyInfo.cs
├── NoteApp.csproj
├── Converters/
│   └── Converters.cs  (NullToVisibilityConverter, BoolToVisibilityConverter)
├── Models/
│   └── Notebook.cs            (Notebook, Category, NotePage, PageType, StrokeData, PointData)
├── Services/
│   └── PdfService.cs    (PDF creation, ISF save/load, export, autosave)
├── Themes/
│   └── DarkTheme.xaml         (ModernButton, GhostButton styles, dark color palette)
└── Views/
    ├── MainWindow.xaml    (main UI layout)
    ├── MainWindow.xaml.cs      (all UI logic, ~941 lines)
    └── RenameDialog.xaml/.cs   (simple input dialog)
4. Architecture & Data Model
Models (Notebook.cs)
Class	Key Properties	Notes
Notebook	Name, SavePath, ObservableCollection<Category> Categories	Top-level container
Category	Name, bool IsDocument, ObservableCollection<Category> SubCategories, ObservableCollection<NotePage> Pages	IsDocument=false → folder; IsDocument=true → document/tab
NotePage	Name, PageType (Lined/ExternalPdf), PdfPath, IsfPath	PageType: Lined or ExternalPdf
StrokeData / PointData	Legacy	Kept for JSON compatibility; not used for stroke storage

Storage Layout
%APPDATA%/NoteApp/notebooks.json → structure only (no strokes)
%APPDATA%/NoteApp/pages/{guid}.isf → handwriting strokes per page (ISF format)
%APPDATA%/NoteApp/pages/{guid}.pdf → lined background per page
Notebook.SavePath/{NotebookName}/{CategoryName}/{DocumentName}.pdf → exported multi-page PDF
5. Feature Documentation (all features)
Notebook Management
•	Create notebook: RenameDialog + OpenFolderDialog → creates Notebook and persists to JSON
•	Rename / Delete notebook with MessageBox confirmation
•	Switch notebook: auto-opens first Category → first Document → first Page
Category & Document Management
•	+ Kategorie: creates Category (IsDocument=false) as folder in TreeView
•	+ Unterkategorie: creates Category (IsDocument=true) as a document tab and auto-creates first page
•	Rename / Delete with confirmation; deletion is recursive
•	ContextMenu on TreeView items and document tabs
Page Navigation
•	Documents contain multiple pages
•	◀ / ▶ buttons navigate between pages within the active document
•	"Seite X / Y" indicator shows current position
•	+ Seite adds a new lined page at the end of the active document
Drawing Tools
•	Pen: InkCanvasEditingMode.Ink with configurable thickness and color
•	Eraser: InkCanvasEditingMode.EraseByPoint
•	Thickness slider: 1–20 px
•	Color palette: Black, DarkBlue, DarkRed, DarkGreen, Purple, DarkOrange, White, Gray
•	Strokes saved immediately on StrokeCollected and StrokeErased events
Canvas & Layout
•	Always maximized (WindowState=Maximized)
•	Page fills available width dynamically (UpdateCanvasSize)
•	DIN-A4 aspect ratio maintained (794:1123)
•	Lined background: red margin line + blue ruled lines
•	Sidebar toggle: ◀◀ hides both sidebars; ☰ hamburger opens popup
Sidebar Popup (Hamburger Mode)
•	When sidebars are hidden, ☰ appears in toolbar
•	Popup (≈440 px wide) shows Notebooks and Categories side by side
•	Popup closes on outside click (StaysOpen=False)
•	Popup lists sync from main lists each time it opens
Save & Export
•	AutoSave every 1 minute via DispatcherTimer
•	Save on navigation: strokes saved when switching notebook/category/document/page
•	Save on close: strokes saved when window closes
•	Export: active document exported as multi-page PDF via SaveFileDialog
•	PDF import: stub implemented (ExternalPdf page type); background rendering not yet done
6. Changelog (all changes made during development)
1. ALWAYS FULLSCREEN
•	Added WindowState="Maximized" to Window element in MainWindow.xaml
2. FULL WIDTH PAGE
•	Added SizeChanged="CanvasScroller_SizeChanged" to ScrollViewer
•	New method UpdateCanvasSize(): calculates width from CanvasScroller.ActualWidth - 80 (margins), sets PageBorder/LineCanvas/MainInkCanvas width+height dynamically
•	DrawLinedBackground() changed to use dynamic MainInkCanvas.Width/Height instead of hardcoded 794/1123
•	LoadPage() now calls UpdateCanvasSize() before DrawLinedBackground()
3. FULLSCREEN MODE (HAMBURGER)
•	Added HamburgerButton (☰) to toolbar, Visibility=Collapsed by default
•	Added ToggleSidebarButton ("◀◀ Seitenleisten ausblenden") to toolbar
•	Added SidebarPopup (Popup control) to root Grid, anchored to HamburgerButton
•	New ColumnDefinitions with x:Name: ColNotebooks, ColSplitter1, ColCategories, ColSplitter2
•	SetSidebarVisibility(bool): sets ColNotebooks.MinWidth=0 BEFORE Width=0 (fix for dead space bug), collapses/shows panels and splitters
•	HamburgerButton_Click: opens popup, syncs lists
•	BuildPopupTreeItem(): creates fresh StackPanel headers (fix for WPF Visual Tree crash)
•	PopupNotebook_Click, PopupCategoryTree_SelectedItemChanged
4. AUTOSAVE EVERY MINUTE
•	AutoSaveTimer interval changed from 10 minutes to 1 minute
•	AutoSaveTimer_Tick: calls SaveCurrentPageStrokes() first, then AutoSaveNotebook for all notebooks
5. SAVE ON NAVIGATION
•	SelectNotebook(): calls SaveCurrentPageStrokes() at start
•	CategoryTree_SelectedItemChanged(): calls SaveCurrentPageStrokes() at start
•	SelectPage(): calls SaveCurrentPageStrokes() at start
6. ISF-BASED STORAGE (strokes in ISF, not JSON)
•	Removed StrokeData list from NotePage model
•	Added IsfPath property to NotePage
•	PdfService.SaveStrokes(): saves StrokeCollection as .isf via FileStream
•	PdfService.LoadStrokes(): loads StrokeCollection from .isf via FileStream
•	PdfService.DeleteStrokes(): deletes .isf file
•	SaveCurrentPageStrokes() now calls PdfService.SaveStrokes()
•	LoadPage() now calls PdfService.LoadStrokes()
•	notebooks.json now contains structure only (no stroke data)
7. DOCUMENT/TAB CONCEPT (Subcategory = Document = Tab)
•	Category model: added IsDocument bool, Pages collection
•	AddSubCategory_Click: creates Category (IsDocument=true) + first NotePage automatically
•	RefreshDocumentTabs(): builds tab bar from SubCategories
•	CreateDocumentTab(): creates styled Border tab
•	SelectDocument(): loads first/current page of document
•	HighlightActiveDocumentTab(): visual feedback for active tab
•	_activeDocument state variable added
•	+ Seite button: adds page to active document (not as new tab)
•	PrevPage_Click / NextPage_Click: navigate within document
•	PageIndicatorText: "Seite X / Y" display
•	TabBar XAML: left ScrollViewer for tabs + right StackPanel for navigation buttons
8. AUTO-OPEN ON STARTUP
•	AutoOpenFirst(): called in Loaded event, opens _notebooks[0] → Categories[0] → SubCategories[0]
•	AutoOpenFirstInNotebook(nb): called in SelectNotebook(), opens first category+document of new notebook
9. EMOJI FIX IN TREEVIEW
•	BuildTreeItem(): uses "\U0001f4c1" (single backslash) instead of "\\U0001f4c1"
•	CreateDocumentTab(): uses "\U0001f4c4" (single backslash)
•	BuildPopupTreeItem(): uses "\U0001f4c1" (single backslash)
10. MESSAGEBOX STRING ESCAPING FIX
•	All interpolated strings with variable names now use \" instead of " inside $"..." strings
•	Example: $"Notizbuch \"{nb.Name}\" wurde erstellt."
11. SIDEBAR DEAD SPACE FIX
•	SetSidebarVisibility(false): sets ColNotebooks.MinWidth=0 and ColCategories.MinWidth=0 BEFORE setting Width=0
•	SetSidebarVisibility(true): restores MinWidth to 150/160 when re-showing
12. WPF VISUAL TREE CRASH FIX (Hamburger Popup)
•	BuildPopupTreeItem() creates a completely new StackPanel + TextBlock for each item
•	Does NOT reuse original.Header (which is a UIElement that can only exist once in the Visual Tree)
•	MouseLeftButtonUp handler sets _activeCategory, calls RefreshPageTabs(), closes popup
7. Current State / Known Limitations
•	PDF import (ImportPdf_Click) is implemented as stub — actual PDF rendering on canvas not yet done
•	No undo/redo functionality
•	No text input (only handwriting via InkCanvas)
•	No zoom functionality
•	RenameDialog is a simple custom dialog (Views/RenameDialog.xaml)
•	StrokeData/PointData classes kept in Notebook.cs for JSON backwards compatibility but no longer used
•	ExternalPdf page type exists but rendering of external PDF background not implemented
8. For AI Assistants: How to continue working on this project
Before You Start
•	Always fetch latest code from https://github.com/btother/NoteApp before making changes
•	Main files to edit: Views/MainWindow.xaml, Views/MainWindow.xaml.cs, Services/PdfService.cs, Models/Notebook.cs
Active State & Rules
•	Active state is tracked via: _activeNotebook, _activeCategory, _activeDocument, _activePage
•	Always call SaveCurrentPageStrokes() before switching any active state
•	Always call UpdateCanvasSize() before DrawLinedBackground() when showing a page
WPF Do’s and Don’ts
•	When adding new TreeView items, always create NEW UIElements — never reuse existing ones (Visual Tree rule)
•	ColNotebooks.MinWidth must be set to 0 before Width=0 to avoid dead space
•	The Popup (SidebarPopup) uses StaysOpen=False — it closes on outside click automatically
C# Syntax Reminders
Wrong	Correct
f"text {var}"	$"text {var}"
Math.max(a, b)	Math.Max(a, b)
$"Name "{var}" end"	$"Name \"{var}\" end"
"\\U0001f4c1"	"\U0001f4c1"
Key Method Reference
Method	When to Call
UpdateCanvasSize()	Call when canvas becomes visible or window resizes
DrawLinedBackground()	Call after UpdateCanvasSize() — uses dynamic dimensions
SaveCurrentPageStrokes()	Call before any navigation change
AutoOpenFirstInNotebook(nb)	Call after SelectNotebook() to auto-open first content
RefreshCategoryTree()	Call after adding/removing/renaming categories
RefreshDocumentTabs()	Call after adding/removing/renaming documents
HighlightActiveDocumentTab()	Call after SelectDocument() to update tab colors
RefreshPageIndicator()	Call after page navigation to update "Seite X / Y"

