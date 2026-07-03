using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoteApp.Models
{
    public class Notebook : INotifyPropertyChanged
    {
        private string _name = "Neues Notizbuch";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // Benutzerdefinierter Speicherpfad
        public string? SavePath { get; set; }
              
        public ObservableCollection<Category> Categories { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Category : INotifyPropertyChanged
    {
        private string _name = "Neue Kategorie";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Category> SubCategories { get; set; } = new();
        public ObservableCollection<NotePage> Pages { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NotePage : INotifyPropertyChanged
    {
        private string _name = "Seite";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public PageType Type { get; set; } = PageType.Lined;

        // Pfad zur gespeicherten PDF-Datei dieser Seite
        public string? PdfPath { get; set; }

        // Handschriftliche Strokes als serialisierbare Liste
        public List<StrokeData> Strokes { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public enum PageType
    {
        Lined,
        ExternalPdf
    }

    public class StrokeData
    {
        public List<PointData> Points { get; set; } = new();
        public double Thickness { get; set; } = 2.0;
        public string Color { get; set; } = "#000000";
    }

    public class PointData
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

        

}