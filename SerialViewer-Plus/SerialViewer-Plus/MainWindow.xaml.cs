using ReactiveUI;
using SerialViewer_Plus.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SerialViewer_Plus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ReactiveWindow<TerminalViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new();
            DataContext = ViewModel;

            this.WhenActivated((CompositeDisposable registration) =>
            {
                ViewModel.WhenAnyValue(vm => vm.Series)
                         .Subscribe(series => chart.Series = series)
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.FFTs)
                         .Subscribe(series => fftView.Series = series)
                         .DisposeWith(registration);
            });
        }
    }
}
