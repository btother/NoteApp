using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoteApp.Models {
    public class Notebook : INotifyPropertyChanged {
        private string _name = "Neues Notizbuch";

        public string Name {
            get => _name;
            set {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string? SavePath { get; set; }
        public ObservableCollection<Category> Categories { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Category : INotifyPropertyChanged {
        private string _name = "Neue Kategorie";

        public string Name {
            get => _name;
            set {
                _name = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Category> SubCategories { get; set; } = new();
        public ObservableCollection<NotePage> Pages { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NotePage : INotifyPropertyChanged {
        private string _name = "Seite";

        public string Name {
            get => _name;
            set {
                _name = value;
                OnPropertyChanged();
            }
        }

        public PageType Type { get; set; } = PageType.Lined;

        /// <summary>
        /// Pfad zur PDF-Datei (Hintergrund, wird beim Erstellen generiert).
        /// </summary>
        public string? PdfPath { get; set; }

        /// <summary>
        /// Pfad zur ISF-Datei (Ink Serialized Format) mit den Handschrift-Strokes.
        /// Wird automatisch neben der PdfPath-Datei abgelegt.
        /// </summary>
        public string? IsfPath { get; set; }

        // ENTFERNT: Strokes werden nicht mehr in der JSON gespeichert.
        // Sie liegen ausschließlich in der ISF-Datei (IsfPath).

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public enum PageType {
        Lined,
        ExternalPdf
    }

    // StrokeData und PointData werden nicht mehr benötigt,
    // bleiben aber für eventuelle Migration alter Daten erhalten.
    public class StrokeData {
        public List<PointData> Points { get; set; } = new();
        public double Thickness { get; set; } = 2.0;
        public string Color { get; set; } = "#000000";
    }

    public class PointData {
        public double X { get; set; }
        public double Y { get; set; }
    }
}