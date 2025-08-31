using System;
using System.ComponentModel;

namespace NeoClientVis
{
    public class BulkFileItem : INotifyPropertyChanged
    {
        private bool _add;
        private string _name;
        private string _pathToFile;
        private DateTime _date;
        private bool _actual;

        public bool Add
        {
            get => _add;
            set { _add = value; OnPropertyChanged(nameof(Add)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string PathToFile
        {
            get => _pathToFile;
            set { _pathToFile = value; OnPropertyChanged(nameof(PathToFile)); }
        }

        public DateTime Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(nameof(Date)); }
        }

        public bool Actual
        {
            get => _actual;
            set { _actual = value; OnPropertyChanged(nameof(Actual)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}