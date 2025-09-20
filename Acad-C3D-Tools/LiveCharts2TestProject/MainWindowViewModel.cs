
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LiveCharts2TestProject
{
    class MainWindowViewModel : ObservableObject
    {
        private readonly Random _random;

        public MainWindowViewModel()
        {
            _random = new Random();
            Points = new ObservableCollection<Point>();
            AddDataCommand = new RelayCommand(AddData);
        }

        public ObservableCollection<Point> Points { get; }

        public ICommand AddDataCommand { get; }

        private void AddData()
        {
            // Generate random data and add to Points collection
            var x = _random.Next(10, 700); // Random X position
            var y = _random.Next(10, 400); // Random Y position
            Points.Add(new Point(x, y));
        }
    }
}
