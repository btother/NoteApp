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

namespace NoteApp.Views {
    public partial class MainWindow : Window {
        // State
        private List<Notebook> _notebooks = new();
        private Notebook? _activeNotebook;
        private Category? _activeCategory;
        private Category? _activeDocument;
        private NotePage? _activePage;
        private bool _isErasing = false;
        private double _penThickness = 3.0;
        private Color _penColor = Colors.Black;
        private readonly string _dataDir;
        private readonly string _dataFile;

        private static readonly Color[] PaletteColors = {
            Colors.Black, Colors.DarkBlue, Colors.DarkRed,
            Colors.DarkGreen, Colors.Purple, Colors.DarkOrange,
            Colors.White, Colors.Gray
        };

        private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;

        // Constructor
        public MainWindow() {
            InitializeComponent();
            PenTool.IsChecked = true;
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
            Loaded += (s, e) => {
                UpdateCanvasSize();
                // FIX 3: Auto-open first notebook/category/document on startup
                AutoOpenFirst();
            };
        }

        // FIX 3: Auto-open first notebook, category, document
        private void AutoOpenFirst() {
            if (_notebooks.Count == 0) return;
            var nb = _notebooks[0];
            SelectNotebook(nb);
            if (nb.Categories.Count == 0) return;
            var cat = nb.Categories[0];
            _activeCategory = cat;
            // Select the TreeViewItem for this category
            foreach (TreeViewItem tvi in CategoryTree.Items) {
                if (tvi.Tag == cat) {
                    tvi.IsSelected = true;
                    break;
                }
            }

            RefreshDocumentTabs();
            if (cat.SubCategories.Count == 0) return;
            var doc = cat.SubCategories[0];
            SelectDocument(doc);
        }

        // Setup
        private void SetupInkCanvas() {
            var da = new DrawingAttributes {
                Color = _penColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };
            MainInkCanvas.DefaultDrawingAttributes = da;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void BuildColorPalette() {
            ColorPicker.Items.Clear();
            foreach (var c in PaletteColors) {
                var btn = new Border {
                    Width = 22,
                    Height = 22,
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

        // Data Persistence
        private void LoadData() {
            if (!File.Exists(_dataFile)) return;
            try {
                var json = File.ReadAllText(_dataFile);
                _notebooks = JsonConvert.DeserializeObject<List<Notebook>>(json)
                             ?? new List<Notebook>();
            }
            catch {
                _notebooks = new List<Notebook>();
            }
        }

        private void SaveData() {
            var json = JsonConvert.SerializeObject(_notebooks, Formatting.Indented);
            File.WriteAllText(_dataFile, json);
        }

        // Notebook Sidebar
        private void RefreshNotebookList() {
            NotebookList.ItemsSource = null;
            NotebookList.ItemsSource = _notebooks;
        }

        private void AddNotebook_Click(object sender, RoutedEventArgs e) {
            var dlg = new RenameDialog("Neues Notizbuch") { Owner = this };
            dlg.Title = "Notizbuch erstellen";
            if (dlg.ShowDialog() != true) return;
            var folderDialog = new OpenFolderDialog {
                Title = $"Speicherpfad f\u00fcr \u2018{dlg.NewName}\u2019 w\u00e4hlen"
            };
            if (folderDialog.ShowDialog() != true) return;
            var nb = new Notebook { Name = dlg.NewName, SavePath = folderDialog.FolderName };
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

        private void Notebook_Click(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Tag is Notebook nb)
                SelectNotebook(nb);
        }

        private void RenameNotebook_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi && mi.Tag is Notebook nb) {
                var dlg = new RenameDialog(nb.Name) { Owner = this };
                if (dlg.ShowDialog() == true) {
                    nb.Name = dlg.NewName;
                    SaveData();
                    RefreshNotebookList();
                }
            }
        }

        private void SelectNotebook(Notebook nb) {
            SaveCurrentPageStrokes();
            _activeNotebook = nb;
            CategoryHeader.Text = $"\U0001f4c2 {nb.Name}";
            RefreshCategoryTree();
            ClearDocumentTabs();
            ClearCanvas();
            AutoOpenFirstInNotebook(nb);
        }
        
        private void AutoOpenFirstInNotebook(Notebook nb)
        {
            var firstCat = nb.Categories.FirstOrDefault();
            if (firstCat == null) return;

            _activeCategory = firstCat;

            var firstDoc = firstCat.SubCategories.FirstOrDefault(s => s.IsDocument);
            if (firstDoc == null) return;

            _activeDocument = firstDoc;
            RefreshDocumentTabs();
            SelectPage(firstDoc.Pages.FirstOrDefault());
        }

        private void ChangeNotebookPath_Click(object sender, RoutedEventArgs e) {
            var nb = GetNotebookFromSender(sender);
            if (nb == null) return;
            var dialog = new OpenFolderDialog {
                Title = $"Neuer Speicherpfad f\u00fcr \u2018{nb.Name}\u2019"
            };
            if (dialog.ShowDialog() != true) return;
            nb.SavePath = dialog.FolderName;
            SaveData();
            MessageBox.Show(
                $"Speicherort ge\u00e4ndert:\n{nb.SavePath}",
                "Gespeichert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DeleteNotebook_Click(object sender, RoutedEventArgs e) {
            var nb = GetNotebookFromSender(sender);
            if (nb == null) return;
            var result = MessageBox.Show(
                $"Notizbuch \"{nb.Name}\" wirklich l\u00f6schen?\nDieser Vorgang kann nicht r\u00fcckg\u00e4ngig gemacht werden.",
                "L\u00f6schen best\u00e4tigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            _notebooks.Remove(nb);
            if (_activeNotebook == nb) {
                _activeNotebook = null;
                _activeCategory = null;
                _activeDocument = null;
                _activePage = null;
            }

            SaveData();
            RefreshNotebookList();
            RefreshCategoryTree();
        }

        private Notebook? GetNotebookFromSender(object sender) {
            if (sender is MenuItem mi && mi.Tag is Notebook nb) return nb;
            return null;
        }

        private Category? GetCategoryFromSender(object sender) {
            if (sender is MenuItem mi && mi.Tag is Category cat) return cat;
            return null;
        }

        // Category Tree
        private void RefreshCategoryTree() {
            CategoryTree.Items.Clear();
            if (_activeNotebook == null) return;
            foreach (var cat in _activeNotebook.Categories)
                CategoryTree.Items.Add(BuildTreeItem(cat));
        }

        private TreeViewItem BuildTreeItem(Category cat) {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            // FIX 1: single backslash unicode escape
            header.Children.Add(new TextBlock { Text = "\U0001f4c1 ", FontSize = 12 });
            var nameBlock = new TextBlock {
                Text = cat.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(nameBlock);
            var item = new TreeViewItem { Header = header, Tag = cat, IsExpanded = true };
            var cm = new ContextMenu();
            var renameItem = new MenuItem { Header = "Umbenennen" };
            renameItem.Click += (s, e) => RenameCategory(cat);
            var deleteItem = new MenuItem { Header = "L\u00f6schen" };
            deleteItem.Click += (s, e) => DeleteCategory(cat);
            cm.Items.Add(renameItem);
            cm.Items.Add(deleteItem);
            item.ContextMenu = cm;
            return item;
        }

        private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (CategoryTree.SelectedItem is TreeViewItem tvi && tvi.Tag is Category cat) {
                SaveCurrentPageStrokes();
                _activeCategory = cat;
                RefreshDocumentTabs();
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e) {
            if (_activeNotebook == null) {
                MessageBox.Show("Bitte zuerst ein Notizbuch ausw\u00e4hlen.", "Hinweis",
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

        private void AddSubCategory_Click(object sender, RoutedEventArgs e) {
            if (_activeCategory == null) {
                MessageBox.Show("Bitte zuerst eine Kategorie ausw\u00e4hlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new RenameDialog("Neues Dokument") { Owner = this };
            dlg.Title = "Dokument erstellen";
            if (dlg.ShowDialog() != true) return;
            var doc = new Category { Name = dlg.NewName, IsDocument = true };
            _activeCategory.SubCategories.Add(doc);

            // FIX 2: Automatically create first page for new document
            var pdfDir = System.IO.Path.Combine(_dataDir, "pages");
            Directory.CreateDirectory(pdfDir);
            var guid = Guid.NewGuid().ToString();
            var firstPage = new NotePage {
                Name = "Seite 1",
                Type = PageType.Lined,
                PdfPath = System.IO.Path.Combine(pdfDir, $"{guid}.pdf"),
                IsfPath = System.IO.Path.Combine(pdfDir, $"{guid}.isf")
            };
            PdfService.CreateLinedPage(firstPage.PdfPath);
            doc.Pages.Add(firstPage);

            SaveData();
            RefreshDocumentTabs();
            SelectDocument(doc);
            MessageBox.Show(
                $"Dokument \"{doc.Name}\" wurde erstellt.",
                "Dokument erstellt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RenameCategory_Click(object sender, RoutedEventArgs e) {
            var cat = GetCategoryFromSender(sender);
            if (cat == null) return;
            var dlg = new RenameDialog(cat.Name) { Owner = this };
            dlg.Title = "Kategorie umbenennen";
            if (dlg.ShowDialog() != true) return;
            cat.Name = dlg.NewName;
            SaveData();
            RefreshCategoryTree();
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e) {
            var cat = GetCategoryFromSender(sender);
            if (cat == null) return;
            var result = MessageBox.Show(
                $"Kategorie \"{cat.Name}\" und alle Dokumente wirklich l\u00f6schen?",
                "L\u00f6schen best\u00e4tigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            if (_activeNotebook!.Categories.Remove(cat)) {
                if (_activeCategory == cat) {
                    _activeCategory = null;
                    _activeDocument = null;
                    _activePage = null;
                }

                SaveData();
                RefreshCategoryTree();
                ClearDocumentTabs();
                ClearCanvas();
            }
        }

        private void RenameCategory(Category cat) {
            var dlg = new RenameDialog(cat.Name) { Owner = this };
            if (dlg.ShowDialog() == true) {
                cat.Name = dlg.NewName;
                SaveData();
                RefreshCategoryTree();
                RefreshDocumentTabs();
            }
        }

        private void DeleteCategory(Category cat) {
            if (_activeNotebook == null) return;
            var result = MessageBox.Show(
                $"Kategorie \"{cat.Name}\" wirklich l\u00f6schen?",
                "L\u00f6schen best\u00e4tigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes) {
                RemoveCategoryRecursive(_activeNotebook.Categories, cat);
                SaveData();
                RefreshCategoryTree();
                ClearDocumentTabs();
                ClearCanvas();
            }
        }

        private bool RemoveCategoryRecursive(
            System.Collections.ObjectModel.ObservableCollection<Category> list, Category target) {
            if (list.Remove(target)) return true;
            foreach (var c in list)
                if (RemoveCategoryRecursive(c.SubCategories, target))
                    return true;
            return false;
        }

        // Document Tabs (Unterkategorien = Dokumente)
        private void RefreshDocumentTabs() {
            PageTabPanel.Children.Clear();
            if (_activeCategory == null) return;
            foreach (var doc in _activeCategory.SubCategories)
                PageTabPanel.Children.Add(CreateDocumentTab(doc));
            if (_activeDocument == null || !_activeCategory.SubCategories.Contains(_activeDocument)) {
                if (_activeCategory.SubCategories.Count > 0)
                    SelectDocument(_activeCategory.SubCategories[0]);
                else
                    ClearCanvas();
            }
            else {
                HighlightActiveDocumentTab();
            }
        }

        private Border CreateDocumentTab(Category doc) {
            var isActive = doc == _activeDocument;
            var tab = new Border {
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(2, 6, 2, 0),
                Cursor = Cursors.Hand,
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x7C, 0x6A, 0xF7))
                    : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x45)),
                Tag = doc
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            // FIX 1: single backslash unicode escape
            sp.Children.Add(new TextBlock { Text = "\U0001f4c4 ", FontSize = 12 });
            sp.Children.Add(new TextBlock {
                Text = doc.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center
            });
            tab.Child = sp;
            tab.MouseLeftButtonDown += (s, e) => {
                if (tab.Tag is Category d) SelectDocument(d);
            };
            var cm = new ContextMenu();
            var renameItem = new MenuItem { Header = "Umbenennen" };
            renameItem.Click += (s, e) => RenameDocument(doc);
            var deleteItem = new MenuItem { Header = "L\u00f6schen" };
            deleteItem.Click += (s, e) => DeleteDocument(doc);
            cm.Items.Add(renameItem);
            cm.Items.Add(deleteItem);
            tab.ContextMenu = cm;
            return tab;
        }

        private void SelectDocument(Category doc) {
            SaveCurrentPageStrokes();
            _activeDocument = doc;
            HighlightActiveDocumentTab();
            RefreshPageIndicator();
            if (doc.Pages.Count > 0) {
                if (_activePage == null || !doc.Pages.Contains(_activePage))
                    _activePage = doc.Pages[0];
                LoadPage(_activePage);
            }
            else {
                _activePage = null;
                ClearCanvas();
            }
        }

        private void HighlightActiveDocumentTab() {
            foreach (var child in PageTabPanel.Children) {
                if (child is Border tab) {
                    bool isActive = tab.Tag == _activeDocument;
                    tab.Background = isActive
                        ? new SolidColorBrush(Color.FromRgb(0x7C, 0x6A, 0xF7))
                        : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x45));
                }
            }
        }

        private void ClearDocumentTabs() {
            PageTabPanel.Children.Clear();
            _activeDocument = null;
        }

        private void RenameDocument(Category doc) {
            var dlg = new RenameDialog(doc.Name) { Owner = this };
            dlg.Title = "Dokument umbenennen";
            if (dlg.ShowDialog() == true) {
                doc.Name = dlg.NewName;
                SaveData();
                RefreshDocumentTabs();
            }
        }

        private void DeleteDocument(Category doc) {
            if (_activeCategory == null) return;
            var result = MessageBox.Show(
                $"Dokument \"{doc.Name}\" und alle Seiten wirklich l\u00f6schen?",
                "L\u00f6schen best\u00e4tigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            foreach (var page in doc.Pages)
                PdfService.DeleteStrokes(page);
            _activeCategory.SubCategories.Remove(doc);
            if (_activeDocument == doc) {
                _activeDocument = null;
                _activePage = null;
            }

            SaveData();
            RefreshDocumentTabs();
            ClearCanvas();
        }

        // Page Indicator
        private void RefreshPageIndicator() {
            if (_activeDocument == null) return;
            int total = _activeDocument.Pages.Count;
            int current = _activePage != null
                ? _activeDocument.Pages.IndexOf(_activePage) + 1
                : 0;
            PageIndicatorText.Text = total > 0
                ? $"Seite {current} / {total}"
                : "Keine Seiten";
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e) {
            if (_activeDocument == null || _activePage == null) return;
            int idx = _activeDocument.Pages.IndexOf(_activePage);
            if (idx > 0) SelectPage(_activeDocument.Pages[idx - 1]);
        }

        private void NextPage_Click(object sender, RoutedEventArgs e) {
            if (_activeDocument == null || _activePage == null) return;
            int idx = _activeDocument.Pages.IndexOf(_activePage);
            if (idx < _activeDocument.Pages.Count - 1)
                SelectPage(_activeDocument.Pages[idx + 1]);
        }

        private void SelectPage(NotePage page) {
            SaveCurrentPageStrokes();
            _activePage = page;
            RefreshPageIndicator();
            LoadPage(page);
        }

        private void AddPage_Click(object sender, RoutedEventArgs e) {
            if (_activeDocument == null) {
                MessageBox.Show("Bitte zuerst ein Dokument (Tab) ausw\u00e4hlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var page = new NotePage {
                Name = $"Seite {_activeDocument.Pages.Count + 1}",
                Type = PageType.Lined
            };
            var pdfDir = System.IO.Path.Combine(_dataDir, "pages");
            Directory.CreateDirectory(pdfDir);
            var guid = Guid.NewGuid().ToString();
            page.PdfPath = System.IO.Path.Combine(pdfDir, $"{guid}.pdf");
            page.IsfPath = System.IO.Path.Combine(pdfDir, $"{guid}.isf");
            PdfService.CreateLinedPage(page.PdfPath);
            _activeDocument.Pages.Add(page);
            SaveData();
            SelectPage(page);
            RefreshPageIndicator();
        }

        private void ImportPdf_Click(object sender, RoutedEventArgs e) {
            if (_activeDocument == null) {
                MessageBox.Show("Bitte zuerst ein Dokument (Tab) ausw\u00e4hlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ofd = new OpenFileDialog {
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                Title = "PDF importieren"
            };
            if (ofd.ShowDialog() != true) return;
            var pdfDir = System.IO.Path.Combine(_dataDir, "pages");
            Directory.CreateDirectory(pdfDir);
            var page = new NotePage {
                Name = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName),
                Type = PageType.ExternalPdf,
                PdfPath = ofd.FileName,
                IsfPath = System.IO.Path.Combine(pdfDir, $"{Guid.NewGuid()}.isf")
            };
            int insertIndex = _activeDocument.Pages.Count;
            if (_activePage != null)
                insertIndex = _activeDocument.Pages.IndexOf(_activePage) + 1;
            _activeDocument.Pages.Insert(insertIndex, page);
            SaveData();
            RefreshPageIndicator();
            SelectPage(page);
        }

        private void RenamePage(NotePage page) {
            var dlg = new RenameDialog(page.Name) { Owner = this };
            if (dlg.ShowDialog() == true) {
                page.Name = dlg.NewName;
                SaveData();
                RefreshPageIndicator();
            }
        }

        private void DeletePage(NotePage page) {
            if (_activeDocument == null) return;
            var result = MessageBox.Show(
                $"Seite \"{page.Name}\" wirklich l\u00f6schen?",
                "L\u00f6schen best\u00e4tigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes) {
                PdfService.DeleteStrokes(page);
                _activeDocument.Pages.Remove(page);
                if (_activePage == page) _activePage = null;
                SaveData();
                RefreshPageIndicator();
                if (_activeDocument.Pages.Count > 0)
                    SelectPage(_activeDocument.Pages[0]);
                else
                    ClearCanvas();
            }
        }

        // Canvas / Drawing
        private void LoadPage(NotePage page) {
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            CanvasScroller.Visibility = Visibility.Visible;
            UpdateCanvasSize();
            DrawLinedBackground();
            MainInkCanvas.Strokes.Clear();
            var strokes = PdfService.LoadStrokes(page);
            foreach (var stroke in strokes)
                MainInkCanvas.Strokes.Add(stroke);
        }

        private void DrawLinedBackground() {
            LineCanvas.Children.Clear();
            double pageW = MainInkCanvas.Width > 0 ? MainInkCanvas.Width : 794;
            double pageH = MainInkCanvas.Height > 0 ? MainInkCanvas.Height : 1123;
            const double marginLeft = 80;
            const double marginTop = 53;
            const double marginRight = 53;
            const double lineSpacing = 32;
            var redLine = new Line {
                X1 = marginLeft, Y1 = marginTop,
                X2 = marginLeft, Y2 = pageH - marginTop,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 220, 80, 80)),
                StrokeThickness = 1.5
            };
            LineCanvas.Children.Add(redLine);
            double y = marginTop + lineSpacing;
            while (y < pageH - marginTop) {
                var line = new Line {
                    X1 = marginLeft, Y1 = y,
                    X2 = pageW - marginRight, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(120, 180, 210, 240)),
                    StrokeThickness = 0.8
                };
                LineCanvas.Children.Add(line);
                y += lineSpacing;
            }
        }

        private void ClearCanvas() {
            PlaceholderPanel.Visibility = Visibility.Visible;
            CanvasScroller.Visibility = Visibility.Collapsed;
            MainInkCanvas.Strokes.Clear();
            LineCanvas.Children.Clear();
            _activePage = null;
        }

        private void SaveCurrentPageStrokes() {
            if (_activePage == null) return;
            PdfService.SaveStrokes(_activePage, MainInkCanvas.Strokes);
            SaveData();
        }

        private void InkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
            => SaveCurrentPageStrokes();

        private void InkCanvas_StrokeErased(object sender, RoutedEventArgs e)
            => SaveCurrentPageStrokes();

        // Tools
        private void PenTool_Checked(object sender, RoutedEventArgs e) {
            EraserTool.IsChecked = false;
            _isErasing = false;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            UpdateDrawingAttributes();
        }

        private void PenTool_Unchecked(object sender, RoutedEventArgs e) {
        }

        private void EraserTool_Checked(object sender, RoutedEventArgs e) {
            PenTool.IsChecked = false;
            _isErasing = true;
            MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        }

        private void EraserTool_Unchecked(object sender, RoutedEventArgs e) {
        }

        private void PenThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _penThickness = e.NewValue;
            UpdateDrawingAttributes();
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Tag is Color c) {
                _penColor = c;
                BuildColorPalette();
                UpdateDrawingAttributes();
            }
        }

        private void UpdateDrawingAttributes() {
            if (MainInkCanvas == null) return;
            MainInkCanvas.DefaultDrawingAttributes = new DrawingAttributes {
                Color = _penColor,
                Width = _penThickness,
                Height = _penThickness,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };
        }

        // Auto-Save Timer
        private void StartAutoSaveTimer() {
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(1);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e) {
            SaveCurrentPageStrokes();
            foreach (var notebook in _notebooks)
                PdfService.AutoSaveNotebook(notebook);
        }

        private void SetNotebookPath_Click(object sender, RoutedEventArgs e) {
            if (_activeNotebook == null) return;
            var dialog = new OpenFolderDialog {
                Title = $"Speicherpfad f\u00fcr '{_activeNotebook.Name}' w\u00e4hlen"
            };
            if (dialog.ShowDialog() == true) {
                _activeNotebook.SavePath = dialog.FolderName;
                SaveData();
                MessageBox.Show(
                    $"Pfad gesetzt:\n{dialog.FolderName}",
                    "Gespeichert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ManualSaveAll_Click(object sender, RoutedEventArgs e) {
            if (_activeNotebook == null) return;
            if (string.IsNullOrWhiteSpace(_activeNotebook.SavePath)) {
                MessageBox.Show(
                    "Bitte zuerst einen Speicherpfad f\u00fcr dieses Notizbuch festlegen.",
                    "Kein Pfad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SaveCurrentPageStrokes();
            PdfService.AutoSaveNotebook(_activeNotebook);
            MessageBox.Show(
                "Alle Seiten wurden gespeichert!",
                "Gespeichert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Export
        private void ExportPdf_Click(object sender, RoutedEventArgs e) {
            if (_activeDocument == null) {
                MessageBox.Show("Kein Dokument ausgew\u00e4hlt.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveCurrentPageStrokes();
            var sfd = new SaveFileDialog {
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                FileName = _activeDocument.Name + ".pdf",
                Title = "Dokument exportieren"
            };
            if (sfd.ShowDialog() != true) return;
            try {
                PdfService.ExportDocumentWithStrokes(_activeDocument, sfd.FileName);
                MessageBox.Show(
                    "PDF erfolgreich exportiert!",
                    "Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex) {
                MessageBox.Show(
                    $"Fehler beim Export: {ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Window Close
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
            SaveCurrentPageStrokes();
            base.OnClosing(e);
        }

        // Sidebar-Toggle & Hamburger-Popup
        private bool _sidebarVisible = true;

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e) {
            _sidebarVisible = !_sidebarVisible;
            SetSidebarVisibility(_sidebarVisible);
        }

        private void SetSidebarVisibility(bool visible) {
            if (visible) {
                ColNotebooks.MinWidth = 150;
                ColNotebooks.Width = new GridLength(200);
                ColSplitter1.Width = new GridLength(4);
                ColCategories.MinWidth = 150;
                ColCategories.Width = new GridLength(220);
                ColSplitter2.Width = new GridLength(4);
                NotebookPanel.Visibility = Visibility.Visible;
                CategoryPanel.Visibility = Visibility.Visible;
                Splitter1.Visibility = Visibility.Visible;
                Splitter2.Visibility = Visibility.Visible;
                HamburgerButton.Visibility = Visibility.Collapsed;
                ToggleSidebarButton.Content = "\u25c4\u25c4 Seitenleisten ausblenden";
            }
            else {
                ColNotebooks.Width = new GridLength(0);
                ColNotebooks.MinWidth = 0.0;
                ColSplitter1.Width = new GridLength(0);
                ColCategories.Width = new GridLength(0);
                ColCategories.MinWidth = 0.0;
                ColSplitter2.Width = new GridLength(0);
                NotebookPanel.Visibility = Visibility.Collapsed;
                CategoryPanel.Visibility = Visibility.Collapsed;
                Splitter1.Visibility = Visibility.Collapsed;
                Splitter2.Visibility = Visibility.Collapsed;
                HamburgerButton.Visibility = Visibility.Visible;
                ToggleSidebarButton.Content = "\u25ba\u25ba Seitenleisten einblenden";
                SidebarPopup.IsOpen = false;
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e) {
            PopupNotebookList.ItemsSource = _notebooks;
            PopupCategoryTree.Items.Clear();
            foreach (TreeViewItem item in CategoryTree.Items)
                PopupCategoryTree.Items.Add(BuildPopupTreeItem(item));
            SidebarPopup.IsOpen = !SidebarPopup.IsOpen;
        }

        private TreeViewItem BuildPopupTreeItem(TreeViewItem original) {
            var cat = original.Tag as Category;
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            // FIX 1: single backslash unicode escape
            header.Children.Add(new TextBlock { Text = "\U0001f4c1 ", FontSize = 12 });
            header.Children.Add(new TextBlock {
                Text = cat?.Name ?? "?",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xF0)),
                VerticalAlignment = VerticalAlignment.Center
            });
            var copy = new TreeViewItem {
                Header = header,
                Tag = original.Tag,
                IsExpanded = true
            };
            copy.MouseLeftButtonUp += (s, e) => {
                if (copy.Tag is Category c) {
                    SaveCurrentPageStrokes();
                    _activeCategory = c;
                    RefreshDocumentTabs();
                    SidebarPopup.IsOpen = false;
                }
            };
            foreach (TreeViewItem child in original.Items)
                copy.Items.Add(BuildPopupTreeItem(child));
            return copy;
        }

        private void PopupNotebook_Click(object sender, MouseButtonEventArgs e) {
            if (sender is Border b && b.Tag is Notebook nb) {
                SelectNotebook(nb);
                PopupCategoryTree.Items.Clear();
                foreach (TreeViewItem item in CategoryTree.Items)
                    PopupCategoryTree.Items.Add(BuildPopupTreeItem(item));
            }
        }

        private void PopupCategoryTree_SelectedItemChanged(
            object sender, RoutedPropertyChangedEventArgs<object> e) {
            // Wird durch BuildPopupTreeItem's MouseLeftButtonUp behandelt
        }

        // Volle Breite
        private void CanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e)
            => UpdateCanvasSize();

        private void UpdateCanvasSize() {
            if (CanvasScroller.ActualWidth <= 0) return;
            double availableWidth = Math.Max(200, CanvasScroller.ActualWidth - 80);
            double pageHeight = availableWidth * (1123.0 / 794.0);
            PageBorder.Width = availableWidth;
            PageBorder.Height = pageHeight;
            LineCanvas.Width = availableWidth;
            LineCanvas.Height = pageHeight;
            MainInkCanvas.Width = availableWidth;
            MainInkCanvas.Height = pageHeight;
            if (CanvasScroller.Visibility == Visibility.Visible)
                DrawLinedBackground();
        }
    }
}