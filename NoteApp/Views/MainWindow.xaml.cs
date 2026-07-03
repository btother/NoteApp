using NoteApp.Models;
using NoteApp.Services;
using NoteApp.Views;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace NoteApp.Views
{
    public partial class MainWindow : Window
    {
        // ── State ──────────────────────────────────────────────────────────
        private List<Notebook> _notebooks = new();
        private Notebook? _activeNotebook;
        private Category? _activeCategory;
        private NotePage? _activePage;

              
        private bool _isErasing = false;
        private double _penThickness = 3.0;
        private Color _penColor = Colors.Black;

        private readonly string _dataDir;
        private readonly string _dataFile;

        private static readonly Color[] PaletteColors =
        {
            Colors.Black, Colors.DarkBlue, Colors.DarkRed,
            Colors.DarkGreen, Colors.Purple, Colors.DarkOrange,
            Colors.White, Colors.Gray
        };
        
        private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;

        // ── Constructor ────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            
            PenTool.IsChecked = true; // ← jetzt sind alle Elemente bereits geladen

            _dataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NoteApp");
            _dataFile = System.IO.Path.Combine(_dataDir, "notebooks.json");
            Directory.CreateDirectory(_dataDir);

            BuildColorPalette();
            LoadData();
            StartAutoSaveTimer();
            RefreshNotebookList();
            SetupInkCanvas();
            Loaded += (s, e) => UpdateCanvasWidth();
        }

        // ── Setup ──────────────────────────────────────────────────────────
        private void SetupInkCanvas()
        {
            var da = new DrawingAttributes
            {
                Color = _penColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };
            MainInkCanvas.DefaultDrawingAttributes = da;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void BuildColorPalette()
        {
            ColorPicker.Items.Clear();
            foreach (var c in PaletteColors)
            {
                var btn = new Border
                {
                    Width = 22, Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(c),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(2),
                    BorderBrush = c == _penColor
                        ? new SolidColorBrush(Colors.White)
                        : new SolidColorBrush(Colors.Transparent),
                    Tag = c
                };
                btn.MouseLeftButtonDown += ColorSwatch_Click;
                ColorPicker.Items.Add(btn);
            }
        }

        // ── Data Persistence ───────────────────────────────────────────────
        private void LoadData()
        {
            if (!File.Exists(_dataFile)) return;
            try
            {
                var json = File.ReadAllText(_dataFile);
                _notebooks = JsonConvert.DeserializeObject<List<Notebook>>(json)
                             ?? new List<Notebook>();
            }
            catch { _notebooks = new List<Notebook>(); }
        }

        private void SaveData()
        {
            var json = JsonConvert.SerializeObject(_notebooks, Formatting.Indented);
            File.WriteAllText(_dataFile, json);
        }

        // ── Notebook Sidebar ───────────────────────────────────────────────
        private void RefreshNotebookList()
        {
            NotebookList.ItemsSource = null;
            NotebookList.ItemsSource = _notebooks;
        }

        private void AddNotebook_Click(object sender, RoutedEventArgs e)
        {
            // 1. Nach Name fragen
            var dlg = new RenameDialog("Neues Notizbuch") { Owner = this };
            dlg.Title = "Notizbuch erstellen";
            if (dlg.ShowDialog() != true) return;

            // 2. Pfad wählen
            var folderDialog = new OpenFolderDialog
            {
                Title = $"Speicherpfad für '{dlg.NewName}' wählen"
            };
            if (folderDialog.ShowDialog() != true) return;

            // 3. Notizbuch erstellen
            var nb = new Notebook
            {
                Name = dlg.NewName,
                SavePath = folderDialog.FolderName
            };
            _notebooks.Add(nb);
            SaveData();
            RefreshNotebookList();
            SelectNotebook(nb);

            MessageBox.Show(
                $"Notizbuch \"{nb.Name}\" wurde erstellt.\nSpeicherpfad: {nb.SavePath}",
                "Notizbuch erstellt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Notebook_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is Notebook nb)
                SelectNotebook(nb);
        }

        private void RenameNotebook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Notebook nb)
            {
                var dlg = new RenameDialog(nb.Name) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    nb.Name = dlg.NewName;
                    SaveData();
                    RefreshNotebookList();
                }
            }
        }

        private void SelectNotebook(Notebook nb)
        {
            _activeNotebook = nb;
            CategoryHeader.Text = $"📂  {nb.Name}";
            RefreshCategoryTree();
            ClearCanvas();
        }
        
        // ── Notizbuch: Speicherort ändern ─────────────────────────────
        private void ChangeNotebookPath_Click(object sender, RoutedEventArgs e)
        {
            var nb = GetNotebookFromSender(sender);
            if (nb == null) return;

            var dialog = new OpenFolderDialog
            {
                Title = $"Neuer Speicherpfad für '{nb.Name}'"
            };
            if (dialog.ShowDialog() != true) return;

            nb.SavePath = dialog.FolderName;
            SaveData();

            MessageBox.Show(
                $"Speicherort geändert:\n{nb.SavePath}",
                "Gespeichert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ── Notizbuch: Löschen ────────────────────────────────────────
        private void DeleteNotebook_Click(object sender, RoutedEventArgs e)
        {
            var nb = GetNotebookFromSender(sender);
            if (nb == null) return;

            var result = MessageBox.Show(
                $"Notizbuch \"{nb.Name}\" wirklich löschen?\nDieser Vorgang kann nicht rückgängig gemacht werden.",
                "Löschen bestätigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _notebooks.Remove(nb);
            if (_activeNotebook == nb)
            {
                _activeNotebook = null;
                _activeCategory = null;
                _activePage = null;
            }
            SaveData();
            RefreshNotebookList();
            RefreshCategoryTree();
        }

        // ── Kategorie: Umbenennen ─────────────────────────────────────
        private void RenameCategory_Click(object sender, RoutedEventArgs e)
        {
            var cat = GetCategoryFromSender(sender);
            if (cat == null) return;

            var dlg = new RenameDialog(cat.Name) { Owner = this };
            dlg.Title = "Kategorie umbenennen";
            if (dlg.ShowDialog() != true) return;

            cat.Name = dlg.NewName;
            SaveData();
            RefreshCategoryTree();
        }

        // ── Kategorie: Löschen ────────────────────────────────────────
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var cat = GetCategoryFromSender(sender);
            if (cat == null) return;

            var result = MessageBox.Show(
                $"Kategorie \"{cat.Name}\" und alle Unterkategorien wirklich löschen?",
                "Löschen bestätigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // In Hauptkategorien suchen
            if (_activeNotebook!.Categories.Remove(cat))
            {
                if (_activeCategory == cat) _activeCategory = null;
                SaveData();
                RefreshCategoryTree();
                return;
            }

            // In Unterkategorien suchen
            foreach (var parent in _activeNotebook.Categories)
            {
                if (parent.SubCategories.Remove(cat))
                {
                    if (_activeCategory == cat) _activeCategory = null;
                    SaveData();
                    RefreshCategoryTree();
                    return;
                }
            }
        }

        // ── Hilfsmethoden ─────────────────────────────────────────────
        private Notebook? GetNotebookFromSender(object sender)
        {
            if (sender is MenuItem mi && mi.Tag is Notebook nb) return nb;
            return null;
        }

        private Category? GetCategoryFromSender(object sender)
        {
            if (sender is MenuItem mi && mi.Tag is Category cat) return cat;
            return null;
        }

        // ── Category Tree ──────────────────────────────────────────────────
        private void RefreshCategoryTree()
        {
            CategoryTree.Items.Clear();
            if (_activeNotebook == null) return;

            foreach (var cat in _activeNotebook.Categories)
                CategoryTree.Items.Add(BuildTreeItem(cat));
        }

        private TreeViewItem BuildTreeItem(Category cat)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = "📁 ", FontSize = 12 });
            var nameBlock = new TextBlock
            {
                Text = cat.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(nameBlock);

            var item = new TreeViewItem
            {
                Header = header,
                Tag = cat,
                IsExpanded = true
            };

            // Context menu
            var cm = new ContextMenu();
            var renameItem = new MenuItem { Header = "Umbenennen" };
            renameItem.Click += (s, e) => RenameCategory(cat);
            var deleteItem = new MenuItem { Header = "Löschen" };
            deleteItem.Click += (s, e) => DeleteCategory(cat);
            cm.Items.Add(renameItem);
            cm.Items.Add(deleteItem);
            item.ContextMenu = cm;

            foreach (var sub in cat.SubCategories)
                item.Items.Add(BuildTreeItem(sub));

            return item;
        }

        private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (CategoryTree.SelectedItem is TreeViewItem tvi && tvi.Tag is Category cat)
            {
                _activeCategory = cat;
                RefreshPageTabs();
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_activeNotebook == null)
            {
                MessageBox.Show("Bitte zuerst ein Notizbuch auswählen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new RenameDialog("Neue Kategorie") { Owner = this };
            dlg.Title = "Kategorie erstellen";
            if (dlg.ShowDialog() != true) return;

            var cat = new Category { Name = dlg.NewName };
            _activeNotebook.Categories.Add(cat);
            SaveData();
            RefreshCategoryTree();

            MessageBox.Show(
                $"Kategorie \"{cat.Name}\" wurde erstellt.",
                "Kategorie erstellt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        private void AddSubCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCategory == null)
            {
                MessageBox.Show("Bitte zuerst eine Kategorie auswählen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new RenameDialog("Neue Unterkategorie") { Owner = this };
            dlg.Title = "Unterkategorie erstellen";
            if (dlg.ShowDialog() != true) return;

            var sub = new Category { Name = dlg.NewName };
            _activeCategory.SubCategories.Add(sub);
            SaveData();
            RefreshCategoryTree();

            MessageBox.Show(
                $"Unterkategorie \"{sub.Name}\" wurde erstellt.",
                "Unterkategorie erstellt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RenameCategory(Category cat)
        {
            var dlg = new RenameDialog(cat.Name) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                cat.Name = dlg.NewName;
                SaveData();
                RefreshCategoryTree();
            }
        }

        private void DeleteCategory(Category cat)
        {
            if (_activeNotebook == null) return;
            var result = MessageBox.Show($"Kategorie \"{cat.Name}\" wirklich löschen?",
                "Löschen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                RemoveCategoryRecursive(_activeNotebook.Categories, cat);
                SaveData();
                RefreshCategoryTree();
                ClearCanvas();
            }
        }

        private bool RemoveCategoryRecursive(
            System.Collections.ObjectModel.ObservableCollection<Category> list, Category target)
        {
            if (list.Remove(target)) return true;
            foreach (var c in list)
                if (RemoveCategoryRecursive(c.SubCategories, target)) return true;
            return false;
        }

        // ── Page Tabs ──────────────────────────────────────────────────────
        private void RefreshPageTabs()
        {
            PageTabPanel.Children.Clear();
            if (_activeCategory == null) return;

            foreach (var page in _activeCategory.Pages)
            {
                var tab = CreatePageTab(page);
                PageTabPanel.Children.Add(tab);
            }

            // ✅ FIX: Direkt LoadPage aufrufen statt SelectPage,
            //         um die Rekursion zu vermeiden
            if (_activeCategory.Pages.Count > 0)
            {
                // Wenn noch keine Seite aktiv ist, erste Seite wählen
                if (_activePage == null || !_activeCategory.Pages.Contains(_activePage))
                {
                    _activePage = _activeCategory.Pages[0];
                }
                LoadPage(_activePage);
            }
            else
            {
                _activePage = null;
                ClearCanvas();
            }
        }

        private Border CreatePageTab(NotePage page)
        {
            var isActive = page == _activePage;
            var tab = new Border
            {
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(14, 8, 0 ,0),
                Margin = new Thickness(2, 6, 2, 0),
                Cursor = Cursors.Hand,
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x7C, 0x6A, 0xF7))
                    : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x45)),
                Tag = page
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = page.Type == PageType.ExternalPdf ? "📄 " : "📝 ",
                FontSize = 12
            });
            sp.Children.Add(new TextBlock
            {
                Text = page.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center
            });

            tab.Child = sp;
            tab.MouseLeftButtonDown += (s, e) =>
            {
                if (tab.Tag is NotePage p) SelectPage(p);
            };

            // Context menu
            var cm = new ContextMenu();
            var renameItem = new MenuItem { Header = "Umbenennen" };
            renameItem.Click += (s, e) => RenamePage(page);
            var deleteItem = new MenuItem { Header = "Löschen" };
            deleteItem.Click += (s, e) => DeletePage(page);
            cm.Items.Add(renameItem);
            cm.Items.Add(deleteItem);
            tab.ContextMenu = cm;

            return tab;
        }

        private void SelectPage(NotePage page)
        {
            SaveCurrentPageStrokes();
            _activePage = page;
            RefreshPageTabs();  // Tabs neu zeichnen (aktiver Tab wird hervorgehoben)
            LoadPage(page);
        }

        private void AddPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCategory == null)
            {
                MessageBox.Show("Bitte zuerst eine Kategorie auswählen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var page = new NotePage
            {
                Name = $"Seite {_activeCategory.Pages.Count + 1}",
                Type = PageType.Lined
            };

            // PDF-Datei für diese Seite anlegen
            var pdfDir = System.IO.Path.Combine(_dataDir, "pages");
            Directory.CreateDirectory(pdfDir);
            page.PdfPath = System.IO.Path.Combine(pdfDir, $"{Guid.NewGuid()}.pdf");
            PdfService.CreateLinedPage(page.PdfPath);

            _activeCategory.Pages.Add(page);
            SaveData();
            SelectPage(page);
        }

        private void ImportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCategory == null)
            {
                MessageBox.Show("Bitte zuerst eine Kategorie auswählen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ofd = new OpenFileDialog
            {
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                Title = "PDF importieren"
            };
            if (ofd.ShowDialog() != true) return;

            // Einfügeposition bestimmen
            int insertIndex = _activeCategory.Pages.Count;
            if (_activePage != null)
                insertIndex = _activeCategory.Pages.IndexOf(_activePage) + 1;

            var page = new NotePage
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName),
                Type = PageType.ExternalPdf,
                PdfPath = ofd.FileName
            };

            _activeCategory.Pages.Insert(insertIndex, page);
            SaveData();
            RefreshPageTabs();
            SelectPage(page);
        }

        private void RenamePage(NotePage page)
        {
            var dlg = new RenameDialog(page.Name) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                page.Name = dlg.NewName;
                SaveData();
                RefreshPageTabs();
            }
        }

        private void DeletePage(NotePage page)
        {
            if (_activeCategory == null) return;
            var result = MessageBox.Show($"Seite \"{page.Name}\" wirklich löschen?",
                "Löschen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _activeCategory.Pages.Remove(page);
                if (_activePage == page) _activePage = null;
                SaveData();
                RefreshPageTabs();
                ClearCanvas();
            }
        }

        // ── Canvas / Drawing ───────────────────────────────────────────────
        private void LoadPage(NotePage page)
        {
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            CanvasScroller.Visibility = Visibility.Visible;

            DrawLinedBackground();
            MainInkCanvas.Strokes.Clear();

            // Gespeicherte Strokes laden
            foreach (var sd in page.Strokes)
            {
                if (sd.Points.Count < 2) continue;
                var pts = new StylusPointCollection(
                    sd.Points.Select(p => new StylusPoint(p.X, p.Y)));
                var da = new DrawingAttributes
                {
                    Color = (Color)ColorConverter.ConvertFromString(sd.Color),
                    Width = sd.Thickness,
                    Height = sd.Thickness,
                    FitToCurve = true
                };
                var stroke = new Stroke(pts, da);
                MainInkCanvas.Strokes.Add(stroke);
            }
        }

        private void DrawLinedBackground()
        {
            LineCanvas.Children.Clear();

            const double pageW = 794;
            const double pageH = 1123;
            const double marginLeft = 80;
            const double marginTop = 53;
            const double marginRight = 53;
            const double lineSpacing = 32;

            // Roter Rand
            var redLine = new Line
            {
                X1 = marginLeft, Y1 = marginTop,
                X2 = marginLeft, Y2 = pageH - marginTop,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 220, 80, 80)),
                StrokeThickness = 1.5
            };
            LineCanvas.Children.Add(redLine);

            // Blaue Linien
            double y = marginTop + lineSpacing;
            while (y < pageH - marginTop)
            {
                var line = new Line
                {
                    X1 = marginLeft, Y1 = y,
                    X2 = pageW - marginRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(120, 180, 210, 240)),
                    StrokeThickness = 0.8
                };
                LineCanvas.Children.Add(line);
                y += lineSpacing;
            }
        }

        private void ClearCanvas()
        {
            PlaceholderPanel.Visibility = Visibility.Visible;
            CanvasScroller.Visibility = Visibility.Collapsed;
            MainInkCanvas.Strokes.Clear();
            LineCanvas.Children.Clear();
            _activePage = null;
        }

        private void SaveCurrentPageStrokes()
        {
            if (_activePage == null) return;
            _activePage.Strokes.Clear();
            foreach (var stroke in MainInkCanvas.Strokes)
            {
                var sd = new StrokeData
                {
                    Thickness = stroke.DrawingAttributes.Width,
                    Color = stroke.DrawingAttributes.Color.ToString()
                };
                foreach (var pt in stroke.StylusPoints)
                    sd.Points.Add(new PointData { X = pt.X, Y = pt.Y });
                _activePage.Strokes.Add(sd);
            }
            SaveData();
        }

        private void InkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
            => SaveCurrentPageStrokes();

        private void InkCanvas_StrokeErased(object sender, RoutedEventArgs e)
            => SaveCurrentPageStrokes();

        // ── Tools ──────────────────────────────────────────────────────────
        private void PenTool_Checked(object sender, RoutedEventArgs e)
        {
            EraserTool.IsChecked = false;
            _isErasing = false;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            UpdateDrawingAttributes();
        }

        private void PenTool_Unchecked(object sender, RoutedEventArgs e) { }

        private void EraserTool_Checked(object sender, RoutedEventArgs e)
        {
            PenTool.IsChecked = false;
            _isErasing = true;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        }

        private void EraserTool_Unchecked(object sender, RoutedEventArgs e) { }

        private void PenThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _penThickness = e.NewValue;
            UpdateDrawingAttributes();
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is Color c)
            {
                _penColor = c;
                BuildColorPalette();
                UpdateDrawingAttributes();
            }
        }

        private void UpdateDrawingAttributes()
        {
            if (MainInkCanvas == null) return;
            MainInkCanvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _penColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };
        }
        
        // ── Auto-Save Timer ──────────────────────────────────────────
        private void StartAutoSaveTimer()
        {
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(10);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var notebook in _notebooks)
            {
                PdfService.AutoSaveNotebook(notebook);
            }
        }

        // ── Pfad für Notizbuch wählen ─────────────────────────────────
        private void SetNotebookPath_Click(object sender, RoutedEventArgs e)
        {
            if (_activeNotebook  == null) return;

            // OpenFolderDialog ist ab .NET 8 in WPF nativ verfügbar
            var dialog = new OpenFolderDialog
            {
                Title = $"Speicherpfad für '{_activeNotebook .Name}' wählen"
            };

            if (dialog.ShowDialog() == true)
            {
                _activeNotebook .SavePath = dialog.FolderName;
                SaveData();
                MessageBox.Show(
                    $"Pfad gesetzt:\n{dialog.FolderName}",
                    "Gespeichert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ── Manueller Export ──────────────────────────────────────────
        private void ManualSaveAll_Click(object sender, RoutedEventArgs e)
        {
            if (_activeNotebook  == null) return;

            if (string.IsNullOrWhiteSpace(_activeNotebook .SavePath))
            {
                MessageBox.Show("Bitte zuerst einen Speicherpfad für dieses Notizbuch festlegen.",
                    "Kein Pfad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PdfService.AutoSaveNotebook(_activeNotebook );
            MessageBox.Show("Alle Seiten wurden gespeichert!", "Gespeichert",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Export ─────────────────────────────────────────────────────────
        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_activePage == null)
            {
                MessageBox.Show("Keine Seite ausgewählt.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveCurrentPageStrokes();

            var sfd = new SaveFileDialog
            {
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                FileName = _activePage.Name + ".pdf",
                Title = "Seite exportieren"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                PdfService.ExportPageWithStrokes(_activePage, sfd.FileName);
                MessageBox.Show("PDF erfolgreich exportiert!", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Export: {ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Window Close ───────────────────────────────────────────────────
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveCurrentPageStrokes();
            base.OnClosing(e);
        }
        
        // ═══════════════════════════════════════════════════════
        // VOLLBILD / SIDEBAR-TOGGLE
        // ═══════════════════════════════════════════════════════

        private bool _sidebarVisible = true;

        /// <summary>
        /// Blendet die beiden Seitenleisten-Spalten ein oder aus.
        /// Im ausgeblendeten Zustand erscheint der Hamburger-Button.
        /// </summary>
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarVisible = !_sidebarVisible;
            SetSidebarVisibility(_sidebarVisible);
        }

        private void SetSidebarVisibility(bool visible)
        {
            if (visible)
            {
            // Spalten wieder einblenden
            ColNotebooks.Width = new GridLength(200);
            ColSplitter1.Width = new GridLength(4);
            ColCategories.Width = new GridLength(220);
            ColSplitter2.Width = new GridLength(4);

                      
                NotebookPanel.Visibility = Visibility.Visible;
                CategoryPanel.Visibility = Visibility.Visible;
                Splitter1.Visibility     = Visibility.Visible;
                Splitter2.Visibility     = Visibility.Visible;

                HamburgerButton.Visibility    = Visibility.Collapsed;
                ToggleSidebarButton.Content   = "◀◀ Ausblenden";
            }
            else
            {
                // Spalten auf 0 setzen → Seitenleisten verschwinden
                ColNotebooks.Width  = new GridLength(0);
                ColSplitter1.Width  = new GridLength(0);
                ColCategories.Width = new GridLength(0);
                ColSplitter2.Width  = new GridLength(0);

                NotebookPanel.Visibility = Visibility.Collapsed;
                CategoryPanel.Visibility = Visibility.Collapsed;
                Splitter1.Visibility     = Visibility.Collapsed;
                Splitter2.Visibility     = Visibility.Collapsed;

                HamburgerButton.Visibility  = Visibility.Visible;
                ToggleSidebarButton.Content = "▶▶ Einblenden";

                // Popup schließen, falls offen
                SidebarPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// Hamburger-Button: öffnet/schließt das Popup mit Notizbüchern & Kategorien.
        /// </summary>
        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            // Popup-Listen synchronisieren
            PopupNotebookList.ItemsSource = NotebookList.ItemsSource;
            SyncPopupCategoryTree();

                      
            SidebarPopup.IsOpen = !SidebarPopup.IsOpen;
        }

        /// <summary>
        /// Synchronisiert den Kategorie-TreeView im Popup mit dem Haupt-TreeView.
        /// </summary>
        private void SyncPopupCategoryTree()
        {
            PopupCategoryTree.ItemsSource = CategoryTree.ItemsSource;
        }

        /// <summary>
        /// Klick auf ein Notizbuch im Popup → gleiche Logik wie Hauptliste.
        /// </summary>
        private void PopupNotebook_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Notebook nb)
            {
            // Gleiche Logik wie Notebook_Click aufrufen
            Notebook_Click(sender, e);
            SyncPopupCategoryTree();
            }
        }

        /// <summary>
        /// Kategorie im Popup ausgewählt → Popup schließen und Seite laden.
        /// </summary>
        private void PopupCategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Selektion an den Haupt-TreeView weitergeben
            CategoryTree_SelectedItemChanged(sender, e);
            // Popup nach Auswahl schließen
            SidebarPopup.IsOpen = false;
        }

        // ═══════════════════════════════════════════════════════
        // VOLLE BREITE: Canvas passt sich dem Fenster an
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Wird aufgerufen wenn der ScrollViewer seine Größe ändert.
        /// Passt InkCanvas, LineCanvas und PageBorder auf die verfügbare Breite an.
        /// </summary>
        private void CanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCanvasWidth();
        }

        private void UpdateCanvasWidth()
        {
            if (CanvasScroller.ActualWidth <= 0) return;

                      
            // Verfügbare Breite minus Margin (2 × 40px)
            double availableWidth = Math.Max(200, CanvasScroller.ActualWidth - 80);

            PageBorder.Width    = availableWidth;
            LineCanvas.Width    = availableWidth;
            MainInkCanvas.Width = availableWidth;

            // Höhe: DIN-A4-Seitenverhältnis (1:√2) beibehalten
            double pageHeight = availableWidth * (1123.0 / 794.0);
            PageBorder.Height    = pageHeight;
            LineCanvas.Height    = pageHeight;
            MainInkCanvas.Height = pageHeight;

            // Linien neu zeichnen (falls vorhanden)
            DrawPageLines();
        }
    }
}