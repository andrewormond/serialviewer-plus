using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using ReactiveUI;
using SerialViewer_Plus.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
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
using static SerialViewer_Plus.Views.ViewportCartesianChart;

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



            LiveCharts.Configure(config =>
                config
                    .HasMap<Point>((p, point) =>
                    {
                        // use the city Population property as the primary value
                        point.PrimaryValue = p.Y;
                        // and the index of the city in our cities array as the secondary value
                        point.SecondaryValue = p.X;
                    }));

            ViewModel = new();
            DataContext = ViewModel;

            this.WhenActivated((CompositeDisposable registration) =>
            {
                chart.UpdaterThrottler = TimeSpan.FromMilliseconds(1000/60);
                ViewModel.WhenAnyValue(vm => vm.Series)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(series =>
                         {
                             chart.Series = series;
                             seriesDetailsControl.ItemsSource = series;
                         })
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.FftSeries)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(series => fftView.Series = series)
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.Sections)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(sects => chart.Sections = sects)
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.SpectrogramSeries)
                         .Where(spect => spect != null)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(spect => spectrogramView.Series = new ISeries[] { spect })
                         .DisposeWith(registration);
                ViewModel.WhenAnyValue(vm => vm.FftStats)
                         .ObserveOn(RxApp.MainThreadScheduler)
                         .Subscribe(stats => fftStatGrid.ItemsSource = stats)
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
                this.OneWayBind(ViewModel, vm => vm.IsPaused, v => v.chart.TooltipPosition, ip => ip ? TooltipPosition.Top : TooltipPosition.Hidden).DisposeWith(registration);

                this.Bind(ViewModel, vm => vm.EnableFft, v => v.enableFFTCheckbox.IsChecked).DisposeWith(registration);
                this.OneWayBind(ViewModel, vm => vm.EnableFft, v => v.fftView.Visibility, b => b ? Visibility.Visible : Visibility.Collapsed).DisposeWith(registration);
                this.OneWayBind(ViewModel, vm => vm.EnableFft, v => v.fftRow.Height, b => b ? new GridLength(1, GridUnitType.Star) : GridLength.Auto).DisposeWith(registration);

                this.WhenAnyValue(v => v.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => fftSizeCombo.ItemsSource = vm.FftSizeOptions)
                    .DisposeWith(registration);

                this.Bind(ViewModel, vm => vm.FftSize, v => v.fftSizeCombo.SelectedItem).DisposeWith(registration);
                this.Bind(ViewModel, vm => vm.LineThickness, v => v.lineWidthBox.Text).DisposeWith(registration);
                this.Bind(ViewModel, vm => vm.MarkerDiameter, v => v.markerSizeBox.Text).DisposeWith(registration);

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

                Observable.FromEvent<SelectionHandler, Rect>(handler => chart.OnSelection += handler, handler => chart.OnSelection -= handler)
                          .ObserveOn(RxApp.MainThreadScheduler)
                          .Subscribe(sect => ViewModel.OnSectionSelected(sect))
                          .DisposeWith(registration);

                Observable.FromEvent(handler => chart.OnSelectionReset += handler, handler => chart.OnSelectionReset -= handler)
                          .ObserveOn(RxApp.MainThreadScheduler)
                          .Subscribe(_ => ViewModel.OnSelectionReset())
                          .DisposeWith(registration);

                ViewModel.RequestAxisReset.RegisterHandler(ctx =>
                {
                    chart.ResetAxis();
                    ctx.SetOutput(Unit.Default);
                }).DisposeWith(registration);

            });
        }
    }
}
