using ReactiveUI;
using SerialViewer_Plus.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(series => chart.Series = series)
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.FFTs)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(series => fftView.Series = series)
                         .DisposeWith(registration);

                ViewModel.WhenAnyValue(vm => vm.FftSize)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(ws => fftWindowSizeLabel.Content = $"{ws}")
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.GraphUPS)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(ups => upsGraphLabel.Content = $"Graph UPS: {ups:0.0} ({1000/ups:0.0} ms)")
                         .DisposeWith(registration);

                this.Bind(ViewModel, vm => vm.IsPaused, v => v.pauseButton.IsChecked).DisposeWith(registration);



                this.Bind(ViewModel, 
                          vm => vm.BufferSize, 
                          v => v.bufferSizeBox.Text, 
                          bs => bs.ToString(), s =>
                {
                    if (int.TryParse(s, out int bs))
                    {
                        if (bs < 64)
                        {
                            bs = 64;
                            //bufferSizeBox.Text = bs.ToString();
                        }
                        else if (bs > 5000)
                        {
                            bs = 5000;
                            bufferSizeBox.Text = bs.ToString();
                        }
                        return bs;
                    }
                    else
                    {
                        return ViewModel.BufferSize;
                    }
                }).DisposeWith(registration);

                this.BindCommand(ViewModel, vm => vm.ClearPointsCommand, v => v.clearButton).DisposeWith(registration);
            });
        }
    }
}
