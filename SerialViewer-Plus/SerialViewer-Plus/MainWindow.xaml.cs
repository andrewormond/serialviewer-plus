﻿using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.WPF;
using ReactiveUI;
using SerialViewer_Plus.ViewModels;
using Serilog;
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
                chart.UpdaterThrottler = TimeSpan.FromMilliseconds(1000/30);
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

        private void chart_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(sender is CartesianChart chart)
            {
                Log.Information("Right click");
                foreach (var xaxis in chart.XAxes)
                {
                    xaxis.MinLimit = null;
                    xaxis.MaxLimit = null;
                }
                foreach (var yaxis in chart.YAxes)
                {
                    yaxis.MinLimit = null;
                    yaxis.MaxLimit = null;
                }
            }
        }
    }
}
