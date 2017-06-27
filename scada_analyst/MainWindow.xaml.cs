using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
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

using Microsoft.Win32;

using LiveCharts;
using LiveCharts.Charts;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using scada_analyst.Controls;
using scada_analyst.Shared;
using LiveCharts.Wpf;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region Variables

        private bool geoLoaded = false, meteoLoaded = false, scadaLoaded = false;

        private bool positionsAddedToData = false, eventsAreProcessed = false, eventsMatchedAcrossTypes = false;

        private static bool mnt_Night = false;
        private static bool mnt_AstDw = false;
        private static bool mnt_NauDw = false;
        private static bool mnt_CivDw = true;
        private static bool mnt_Daytm = true;
        private static bool mnt_CivDs = true;
        private static bool mnt_NauDs = false;
        private static bool mnt_AstDs = false;

        private bool exportPowMaxm = false, exportAmbMaxm = false, exportWSpMaxm = false;
        private bool exportPowMinm = false, exportAmbMinm = false, exportWSpMinm = false;
        private bool exportPowMean = false, exportAmbMean = false, exportWSpMean = false;
        private bool exportPowStdv = false, exportAmbStdv = false, exportWSpStdv = false;

        private bool exportGBxMaxm = false, exportGenMaxm = false, exportMBrMaxm = false;
        private bool exportGBxMinm = false, exportGenMinm = false, exportMBrMinm = false;
        private bool exportGBxMean = false, exportGenMean = false, exportMBrMean = false;
        private bool exportGBxStdv = false, exportGenStdv = false, exportMBrStdv = false;

        private static double cutIn = 4, cutOut = 25, powerLim = 0, ratedPwr = 2300; // ratedPwr always in kW !!!

        private List<int> loadedAsset = new List<int>();
        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource _cts;

        private DateTime _dataExportStart = new DateTime();
        private DateTime _dataExportEndTm = new DateTime();

        public static TimeSpan _workHrsMorning = new TimeSpan(7, 0, 0);
        public static TimeSpan _workHrsEvening = new TimeSpan(20, 0, 0);

        private static TimeSpan _duratFilter = new TimeSpan(0, 10, 0);

        // this is to allow changing the property of the timestep in the loaded scada data at some point
        private static TimeSpan _scadaSeprtr = new TimeSpan(0, 10, 0);

        private Analysis analyser = new Analysis();
        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();
        
        private List<DataOverview> _overview = new List<DataOverview>();

        private ObservableCollection<Structure> _assetsVw = new ObservableCollection<Structure>();
        private ObservableCollection<EventData> _allWtrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _allPwrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _loSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _hiSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _noPwView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _rtPwView = new ObservableCollection<EventData>();
           
        private ObservableCollection<ScadaData.ScadaSample> _thisEventData;
        private ObservableCollection<ScadaData.ScadaSample> _weekEventData;
        private ObservableCollection<ScadaData.ScadaSample> _histEventData;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            this.WindowState = WindowState.Maximized;

            progress_ProgrBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;

            List<string> newNames = new List<string>();
            newNames.Add(" ");
            newNames.Add("Gearbox");
            newNames.Add("Generator");
            newNames.Add("Main bearing");
            Comb_EquipmentChoice.ItemsSource = newNames;

            LView_Overview.IsEnabled = false;

            LView_WSpdEvLo.IsEnabled = false;
            LView_WSpdEvHi.IsEnabled = false;
            LView_PowrNone.IsEnabled = false;
            LView_PowrRted.IsEnabled = false;

            CreateAndUpdateDataSummary(); // Call this before GetPowerProdLabel as that will change one of the strings here
            UpdateDurationLabel();
            LBL_PwrProdAmount.Content = GetPowerProdLabel();

            Comb_EquipmentChoice.IsEnabled = false;
            LBL_EquipmentChoice.IsEnabled = false;
            CBox_DataSetChoice.IsEnabled = false;

            LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
            LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
            LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region UI Updating

        /// <summary>
        /// Creates a label used in the power production tab which shows a power value
        /// </summary>
        /// <returns></returns>
        private string GetPowerProdLabel()
        {
            return "Power Production: " + ratedPwr.ToString() + " kW";
        }

        private void UpdateDurationLabel()
        {
            LBL_DurationFilter.Content = "Duration Filter: " + _duratFilter.ToString();
            LBL_DurationFilter2.Content = "Duration Filter: " + _duratFilter.ToString();
        }

        #endregion

        /// <summary>
        /// Brings up the About window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        /// <summary>
        /// The function that is called by the button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddDaytimesToEvents(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if (AssetsView == null || AssetsView.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No structures have been loaded. Load structures before using this function.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && NoPowViews.Count == 0) ||
                        (Tab_RtPower.IsSelected && RtdPowView.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if (geoFile == null || !positionsAddedToData)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }

                // if the above conditions are not fulfilled, the process can continue

                ProgressBarVisible();
                
                await Task.Run(() => analyser.AddDaytimesToEvents(progress));                 

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method called by the UI that references the AddStructureLocations bool
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddStructureLocationsAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                if (geoFile == null || geoFile.GeoInfo.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No geographic data is loaded.");

                    throw new CancelLoadingException();
                }
                else if (meteoFile.MetMasts.Count == 0 && scadaFile.WindFarm.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No meteorologic or SCADA data is loaded.");

                    throw new CancelLoadingException();
                }

                positionsAddedToData = analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            }
            catch (CancelLoadingException) { }
            catch { throw new Exception(); }
        }

        /// <summary>
        /// Method to deal with the equipment chosen; should change what the graph and the listview display
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Comb_EquipmentChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // only go into this event if the below conditions are true; probably will disable the combobox as well though
            DisplayCorrectEventDetails();
        }

        private void DisplayCorrectEventDetails()
        {
            if (Comb_EquipmentChoice.SelectedIndex != -1)
            {
                if (Comb_EquipmentChoice.SelectedIndex == 0)
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    LChart_Basic.Visibility = Visibility.Collapsed;
                }
                else if ((string)Comb_EquipmentChoice.SelectedItem == "Gearbox")
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Visible;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;

                    //try it with a normal view as the List<DateTimePoint> did not work particularly well
                    ChartShowSeries("Gearbox");
                }
                else if ((string)Comb_EquipmentChoice.SelectedItem == "Generator")
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Visible;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    ChartShowSeries("Generator");
                }
                else if ((string)Comb_EquipmentChoice.SelectedItem == "Main bearing")
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Visible;
                    ChartShowSeries("Main bearing");
                }
            }
        }

        private void ChartShowSeries(string input)
        {
            try
            {
                if (LChart_Basic.Series.Count == 1) { LChart_Basic.Series.RemoveAt(0); }

                List<double> list = new List<double>();
                List<string> times = new List<string>();

                LineSeries newLine = new LineSeries();

                for (int i = 0; i < _weekEventData.Count; i++)
                {
                    double variable = 0;
                    
                    if (input == "Gearbox")
                    {
                        newLine.Title = "HS Gens.";
                        variable = _weekEventData[i].Gearbox.Hs.Gens.Mean;
                    }
                    else if (input == "Generator")
                    {
                        newLine.Title = "Bearing G";
                        variable = _weekEventData[i].Genny.bearingG.Mean;
                    }
                    else if (input == "Main bearing")
                    {
                        newLine.Title = "Main Bearing.";
                        variable = _weekEventData[i].MainBear.Standards.Mean;
                    }

                    list.Add(variable != -9999 ? variable : double.NaN);
                    times.Add(_weekEventData[i].TimeStamp.ToString("HH:mm DD/MM"));
                }

                newLine.Values = new ChartValues<double>(list);
                newLine.Fill = Brushes.Transparent;
                LChart_Basic.Series.Add(newLine);
            }
            catch { }
        }

        /// <summary>
        /// Calls other clear events, leaves nothing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);

            ClearEvents(sender, e);

            _dataExportStart = new DateTime();
            _dataExportEndTm = new DateTime();
        }
        
        /// <summary>
        /// Clears all processed events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearEvents(object sender, RoutedEventArgs e)
        {
            AllWtrView = new ObservableCollection<EventData>();
            LoSpdViews = new ObservableCollection<EventData>();
            HiSpdViews = new ObservableCollection<EventData>();

            AllPowView = new ObservableCollection<EventData>();
            NoPowViews = new ObservableCollection<EventData>();
            RtdPowView = new ObservableCollection<EventData>();

            LView_PowrNone.ItemsSource = null;
            LView_PowrNone.IsEnabled = false;

            LView_PowrRted.ItemsSource = null;
            LView_PowrRted.IsEnabled = false;

            LView_WSpdEvLo.ItemsSource = null;
            LView_WSpdEvLo.IsEnabled = false;

            LView_WSpdEvHi.ItemsSource = null;
            LView_WSpdEvHi.IsEnabled = false;

            CreateAndUpdateDataSummary();

            eventsMatchedAcrossTypes = false;
            eventsAreProcessed = false;
        }

        /// <summary>
        /// Clears all loaded geographic data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            loadedFiles.Clear();
            geoFile = null; geoLoaded = false; positionsAddedToData = false;

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            CreateAndUpdateDataSummary();
        }

        /// <summary>
        /// Clears all loaded meteorologic data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            for (int i = analyser.AssetsList.Count - 1; i >= 0; i--)
            {
                if (analyser.AssetsList[i].Type == BaseStructure.Types.METMAST)
                {
                    loadedAsset.Remove(analyser.AssetsList[i].UnitID);
                    analyser.AssetsList.RemoveAt(i);
                }
            }

            loadedFiles.Clear();
            meteoFile = new MeteoData(); meteoLoaded = false;            

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            CreateAndUpdateDataSummary();
            PopulateOverview();
        }

        /// <summary>
        /// Clears all loaded SCADA data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            for (int i = analyser.AssetsList.Count - 1; i >= 0; i--)
            {
                if (analyser.AssetsList[i].Type == BaseStructure.Types.TURBINE)
                {
                    loadedAsset.Remove(analyser.AssetsList[i].UnitID);
                    analyser.AssetsList.RemoveAt(i);
                }
            }

            loadedFiles.Clear();
            scadaFile = new ScadaData(); scadaLoaded = false;

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            CreateAndUpdateDataSummary();
            PopulateOverview();
        }

        /// <summary>
        /// Changes the duration of the filter than can be used to remove events from active consideration
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditDurationFilter(object sender, RoutedEventArgs e)
        {
            Window_NumberTwo getTimeDur = new Window_NumberTwo(this, "Duration Filter Settings",
                "Hours", "Minutes", false, false, _duratFilter.TotalHours, _duratFilter.Minutes);

            if (getTimeDur.ShowDialog().Value)
            {
                _duratFilter = new TimeSpan((int)getTimeDur.NumericValue1, (int)getTimeDur.NumericValue2, 0);
                UpdateDurationLabel();
            }
        }

        /// <summary>
        /// Quit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();            
        }

        /// <summary>
        /// Exports meteorology data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (meteoFile.MetMasts.Count != 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    // set a default file name
                    saveFileDialog.FileName = ".csv";
                    // set filters
                    saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog().Value)
                    {
                        ProgressBarVisible();

                        if (CBox_DateRangeExport.IsChecked)
                        {
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", _dataExportStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", _dataExportEndTm);

                            if (startCal.ShowDialog().Value)
                            {
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => meteoFile.ExportFiles(progress, saveFileDialog.FileName,_dataExportStart,_dataExportEndTm));

                        ProgressBarInvisible();
                    }
                }
                else
                {
                    MessageBox.Show(this, "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }
        
        /// <summary>
        /// Exports SCADA data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportScadaDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (scadaFile.WindFarm.Count != 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    // set a default file name
                    saveFileDialog.FileName = ".csv";
                    // set filters
                    saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog().Value)
                    {
                        ProgressBarVisible();

                        if (CBox_DateRangeExport.IsChecked)
                        {
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", _dataExportStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", _dataExportEndTm);

                            if (startCal.ShowDialog().Value)
                            {
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                            exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                            exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                            exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                            exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                            exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                            exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv,
                            _dataExportStart, _dataExportEndTm));

                        ProgressBarInvisible();
                    }
                }
                else
                {
                    MessageBox.Show(this, "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method called by the UI, references the real method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FindEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to process it.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();
                ClearEvents(sender, e);

                await Task.Run(() => analyser.FindEvents(scadaFile, meteoFile, progress));

                ProgressBarInvisible();
                RefreshEvents();

                eventsAreProcessed = true;
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async void GetDistancesBetweenStructuresAsync(object sender, RoutedEventArgs e)
        {
            // this method to invoke the analysis class and do some work there

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (geoFile == null || !positionsAddedToData)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }
                else if (AssetsView.Count < 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }

                await Task.Run(() => analyser.GetDistances(analyser.AssetsList));
            }
            catch (CancelLoadingException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method that loads the geographic data
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="progress"></param>
        private void LoadGeoData(string[] filenames, IProgress<int> progress)
        {
            try
            {
                for (int i = 0; i < filenames.Length; i++)
                {
                    if (!loadedFiles.Contains(filenames[i]))
                    {
                        geoFile = new GeoData(filenames[i], progress);

                        loadedFiles.Add(filenames[i]);
                    }
                }
            }
            catch { throw; }
        }

        /// <summary>
        /// UI calls this, references the non async method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadGeoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Location files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();
                    
                    await Task.Run(() => LoadGeoData(openFileDialog.FileNames, progress));

                    ProgressBarInvisible();

                    geoLoaded = true;
                }
            }
            catch (LoadingCancelledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {

            }
            catch (WrongFileTypeException)
            {
                MessageBox.Show("This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method loads the meteorology data, checks for existing files and appends datapoints
        /// </summary>
        /// <param name="existingData"></param>
        /// <param name="filenames"></param>
        /// <param name="isLoaded"></param>
        /// <param name="progress"></param>
        private void LoadMeteoData(MeteoData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                MeteoData analysis = new MeteoData(existingData);

                if (!isLoaded)
                {
                    analysis = new MeteoData(filenames, progress);
                }
                else
                {
                    analysis.AppendFiles(filenames, progress);
                }

                meteoFile = analysis;
                meteoLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// UI references this, this calls the above sync method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Meteorology files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => LoadMeteoData(meteoFile, openFileDialog.FileNames, meteoLoaded,
                        progress));

                    ProgressBarInvisible();

                    PopulateOverview();
                }
            }
            catch (LoadingCancelledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {

            }
            catch (WrongFileTypeException)
            {
                MessageBox.Show("This file cannot be loaded since it is of an incompatible file type for this function.");                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method loads the SCADA data, checks for existing files and appends datapoints
        /// </summary>
        /// <param name="existingData"></param>
        /// <param name="filenames"></param>
        /// <param name="isLoaded"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task LoadScadaData(ScadaData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = new ScadaData(existingData);

                await Task.Run(() =>
                {
                    if (!isLoaded)
                    {
                        analysis = new ScadaData(filenames, progress);
                    }
                    else
                    {
                        analysis.AppendFiles(filenames, progress);
                    }
                });

                scadaFile = analysis;
                scadaLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// UI calls this method, references the async Task
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadScadaDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;    

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "SCADA files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() =>
                        LoadScadaData(scadaFile, openFileDialog.FileNames, scadaLoaded, progress), token);

                    ProgressBarInvisible();
                    PopulateOverview();
                }
            }
            catch (LoadingCancelledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (WrongFileTypeException)
            {
                MessageBox.Show("This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, goes to above
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MatchEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }
                else if (!eventsAreProcessed)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been processed.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;
                LView_PowrRted.ItemsSource = null;

                await Task.Run(() => analyser.AssociateEvents(progress));

                ProgressBarInvisible();
                RefreshEvents();

                eventsMatchedAcrossTypes = true;
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, references above
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ProcessDurationFilterAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (_duratFilter.TotalSeconds == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "The duration filter is set to 0 seconds. Please change the length of this filter.");

                    throw new CancelLoadingException();
                }
                else if (NoPowViews.Count == 0 && RtdPowView.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are power production related events to filter.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                await Task.Run(() => analyser.RemoveByDuration(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RemoveMatchedEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }
                else if (!eventsAreProcessed)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been processed.");

                    throw new CancelLoadingException();
                }
                else if (!eventsMatchedAcrossTypes)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been matched across types.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;
                LView_PowrRted.ItemsSource = null;

                await Task.Run(() => analyser.RemoveMatchedEvents(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, references the OC return method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RemoveProcessedDaytimesAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if ((Tab_NoPower.IsSelected && NoPowViews.Count == 0) ||
                        (Tab_RtPower.IsSelected && RtdPowView.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && (NoPowViews.Count > 0 && NoPowViews[0].DayTime == EventData.TimeOfEvent.UNKNOWN)) ||
                    (Tab_RtPower.IsSelected && (RtdPowView.Count > 0 && RtdPowView[0].DayTime == EventData.TimeOfEvent.UNKNOWN)))
                {
                    await this.ShowMessageAsync("Warning!",
                        "The events cannot be removed before the day-time associations have been created.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                await Task.Run(() => analyser.RemoveProcessedDaytimes(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Resets power production null or negative to the original state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetNoPwrProdEvents(object sender, RoutedEventArgs e)
        {
            _duratFilter = new TimeSpan(0, 10, 0);
            UpdateDurationLabel();

            analyser.ResetEventList();
            PopulateOverview();
            RefreshEvents();
        }

        /// <summary>
        /// Resets power production high -- at "rated" -- to the original state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetRtdPwrProdEvents(object sender, RoutedEventArgs e)
        {
            _duratFilter = new TimeSpan(0, 10, 0);
            UpdateDurationLabel();

            analyser.ResetEventList();
            PopulateOverview();
            RefreshEvents();
        }

        /// <summary>
        /// Brings up a dialog window with various analysis settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetAnalysisSettings(object sender, RoutedEventArgs e)
        {
            // old method here before took the use of the proper analysis settings window
            // I'll leave the old code here in its commented form

            //Window_NumberTwo setSpdLims = new Window_NumberTwo(this, "Turbine Operating Limits",
            //    "Cut In", "Cut Out", true, false, cutIn, cutOut);

            //if (setSpdLims.ShowDialog().Value)
            //{
            //    cutIn = setSpdLims.NumericValue1;
            //    cutOut = setSpdLims.NumericValue2;
            //}

            // new code below with the proper analysis settings window, etc
            // more options for expanding the code if necessary (which it no doubt will be)

            Window_AnalysisSettings anaSets = new Window_AnalysisSettings(this, cutIn, cutOut, ratedPwr,
                mnt_Night, mnt_AstDw, mnt_NauDw, mnt_CivDw, mnt_Daytm, mnt_CivDs, mnt_NauDs, mnt_AstDs, _workHrsMorning, _workHrsEvening);

            if (anaSets.ShowDialog().Value)
            {
                cutIn = anaSets.SpdIns;
                cutOut = anaSets.SpdOut;
                ratedPwr = anaSets.RtdPwr;

                mnt_Night = anaSets.Mnt_Night;
                mnt_AstDw = anaSets.Mnt_AstDw;
                mnt_NauDw = anaSets.Mnt_NauDw;
                mnt_CivDw = anaSets.Mnt_CivDw;
                mnt_Daytm = anaSets.Mnt_Daytm;
                mnt_CivDs = anaSets.Mnt_CivDs;
                mnt_NauDs = anaSets.Mnt_NauDs;
                mnt_AstDs = anaSets.Mnt_AstDs;

                _workHrsMorning = anaSets.WorkHoursMorning;
                _workHrsEvening = anaSets.WorkHoursEvening;

                RtdPowView.Clear();
                LBL_PwrProdAmount.Content = GetPowerProdLabel();
                CreateAndUpdateDataSummary(); 
            }
        }
        
        /// <summary>
        /// Brings up a dialog window with export settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetExportSettings(object sender, RoutedEventArgs e)
        {
            Window_ExportControl exportOptions = new Window_ExportControl(this);

            exportOptions.ExportAmbMaxm = exportAmbMaxm;
            exportOptions.ExportAmbMinm = exportAmbMinm;
            exportOptions.ExportAmbMean = exportAmbMean;
            exportOptions.ExportAmbStdv = exportAmbStdv;

            exportOptions.ExportPowMaxm = exportPowMaxm;
            exportOptions.ExportPowMinm = exportPowMinm;
            exportOptions.ExportPowMean = exportPowMean;
            exportOptions.ExportPowStdv = exportPowStdv;

            exportOptions.ExportWSpMaxm = exportWSpMaxm;
            exportOptions.ExportWSpMinm = exportWSpMinm;
            exportOptions.ExportWSpMean = exportWSpMean;
            exportOptions.ExportWSpStdv = exportWSpStdv;

            exportOptions.ExportGBxMaxm = exportGBxMaxm;
            exportOptions.ExportGBxMinm = exportGBxMinm;
            exportOptions.ExportGBxMean = exportGBxMean;
            exportOptions.ExportGBxStdv = exportGBxStdv;

            exportOptions.ExportGenMaxm = exportGenMaxm;
            exportOptions.ExportGenMinm = exportGenMinm;
            exportOptions.ExportGenMean = exportGenMean;
            exportOptions.ExportGenStdv = exportGenStdv;

            exportOptions.ExportMBrMaxm = exportMBrMaxm;
            exportOptions.ExportMBrMinm = exportMBrMinm;
            exportOptions.ExportMBrMean = exportMBrMean;
            exportOptions.ExportMBrStdv = exportMBrStdv;

            if (exportOptions.ShowDialog().Value)
            {
                exportPowMaxm = exportOptions.ExportPowMaxm;
                exportPowMinm = exportOptions.ExportPowMinm;
                exportPowMean = exportOptions.ExportPowMean;
                exportPowStdv = exportOptions.ExportPowStdv;

                exportAmbMaxm = exportOptions.ExportAmbMaxm;
                exportAmbMinm = exportOptions.ExportAmbMinm;
                exportAmbMean = exportOptions.ExportAmbMean;
                exportAmbStdv = exportOptions.ExportAmbStdv;

                exportWSpMaxm = exportOptions.ExportWSpMaxm;
                exportWSpMinm = exportOptions.ExportWSpMinm;
                exportWSpMean = exportOptions.ExportWSpMean;
                exportWSpStdv = exportOptions.ExportWSpStdv;

                exportGBxMaxm = exportOptions.ExportGBxMaxm;
                exportGBxMinm = exportOptions.ExportGBxMinm;
                exportGBxMean = exportOptions.ExportGBxMean;
                exportGBxStdv = exportOptions.ExportGBxStdv;

                exportGenMaxm = exportOptions.ExportGenMaxm;
                exportGenMinm = exportOptions.ExportGenMinm;
                exportGenMean = exportOptions.ExportGenMean;
                exportGenStdv = exportOptions.ExportGenStdv;

                exportMBrMaxm = exportOptions.ExportMBrMaxm;
                exportMBrMinm = exportOptions.ExportMBrMinm;
                exportMBrMean = exportOptions.ExportMBrMean;
                exportMBrStdv = exportOptions.ExportMBrStdv;

                // these must be used by the async task that doesn't yet exist
                // the async task will lead into the writing method
            }
        }

        #region Background Methods

        void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();

                ProgressBarInvisible();
            }
        }

        void CreateAndUpdateDataSummary()
        {
            LView_LoadedOverview.ItemsSource = null;

            _overview.Clear();
            _overview.Add(new DataOverview("Structures", AssetsView.Count));
            _overview.Add(new DataOverview("Low Winds", LoSpdViews.Count));
            _overview.Add(new DataOverview("High Winds", HiSpdViews.Count));
            _overview.Add(new DataOverview("Power: None", NoPowViews.Count));
            _overview.Add(new DataOverview("Power: " + Common.GetStringDecimals(ratedPwr / 1000.0, 1) + "MW", RtdPowView.Count));

            LView_LoadedOverview.ItemsSource = _overview;
        }

        void PopulateOverview()
        {
            if (meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    if (!loadedAsset.Contains(meteoFile.MetMasts[i].UnitID))
                    {
                        analyser.AssetsList.Add((Structure)meteoFile.MetMasts[i]);

                        loadedAsset.Add(meteoFile.MetMasts[i].UnitID);
                    }
                    else
                    {
                        int index = analyser.AssetsList.IndexOf
                            (analyser.AssetsList.Where(x => x.UnitID == meteoFile.MetMasts[i].UnitID).FirstOrDefault());

                        analyser.AssetsList[index].CheckDataSeriesTimes(meteoFile.MetMasts[i]);
                    }
                }
            }

            if (scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    if (!loadedAsset.Contains(scadaFile.WindFarm[i].UnitID))
                    {
                        analyser.AssetsList.Add((Structure)scadaFile.WindFarm[i]);

                        loadedAsset.Add(scadaFile.WindFarm[i].UnitID);
                    }
                    else
                    {
                        int index = analyser.AssetsList.IndexOf
                            (analyser.AssetsList.Where(x => x.UnitID == scadaFile.WindFarm[i].UnitID).FirstOrDefault());

                        analyser.AssetsList[index].CheckDataSeriesTimes(scadaFile.WindFarm[i]);
                    }
                }
            }

            LView_Overview.ItemsSource = AssetsView;
            LView_Overview.Items.Refresh();

            LView_Overview.IsEnabled = AssetsView != null && AssetsView.Count > 0 ? true : false;

            if (AssetsView != null && AssetsView.Count > 0)
            {
                _dataExportStart = AssetsView[0].StartTime;
                _dataExportEndTm = AssetsView[0].EndTime;

                // i is 1 below because the first values have already been assigned by the above code
                for (int i = 1; i < AssetsView.Count; i++)
                {
                    if (AssetsView[i].StartTime < _dataExportStart) { _dataExportStart = AssetsView[i].StartTime; }
                    if (AssetsView[i].EndTime > _dataExportEndTm) { _dataExportEndTm = AssetsView[i].EndTime; }
                }

                CreateAndUpdateDataSummary();
            }
        }

        void ProgressBarVisible()
        {
            progress_ProgrBar.Visibility = Visibility.Visible;
            label_ProgressBar.Visibility = Visibility.Visible;
            cancel_ProgressBar.Visibility = Visibility.Visible;
            //counter_ProgressBar.Visibility = Visibility.Visible;
        }
        
        void ProgressBarInvisible()
        {
            progress_ProgrBar.Visibility = Visibility.Collapsed;
            progress_ProgrBar.Value = 0;

            label_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Content = "";
            cancel_ProgressBar.Visibility = Visibility.Collapsed;

            //counter_ProgressBar.Content = "";
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        void RefreshEvents()
        {
            if (NoPowViews.Count != 0)
            {
                LView_PowrNone.IsEnabled = true;
                LView_PowrNone.ItemsSource = NoPowViews;
                LView_PowrNone.Items.Refresh();
            }

            if (RtdPowView.Count != 0)
            {
                LView_PowrRted.IsEnabled = true;
                LView_PowrRted.ItemsSource = RtdPowView;
                LView_PowrRted.Items.Refresh();
            }

            if (LoSpdViews.Count != 0)
            {
                LView_WSpdEvLo.IsEnabled = true;
                LView_WSpdEvLo.ItemsSource = LoSpdViews;
                LView_WSpdEvLo.Items.Refresh();
            }

            if (HiSpdViews.Count != 0)
            {
                LView_WSpdEvHi.IsEnabled = true;
                LView_WSpdEvHi.ItemsSource = HiSpdViews;
                LView_WSpdEvHi.Items.Refresh();
            }

            CreateAndUpdateDataSummary();
        }
        
        void RemoveSingleAsset(int toRemove)
        {
            if (AssetsView.Count != 0)
            {
                for (int i = AssetsView.Count - 1; i >= 0; i--)
                {
                    if (AssetsView[i].UnitID == toRemove)
                    {
                        //App.Current.Dispatcher.Invoke((Action)delegate // 
                        //{
                            analyser.AssetsList.RemoveAt(i);
                        //});

                        break;
                    }
                }
            }

            LView_Overview.ItemsSource = AssetsView;
            LView_Overview.Items.Refresh();
            CreateAndUpdateDataSummary();
        }

        void OnPropertyChanged(string name)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(name));
            }
        }
        
        void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgrBar.Value = value;
        }

        #endregion

        #region ContextMenu

        private void LView_Overview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuAssets();
        }

        private void SetContextMenuAssets()
        {
            ContextMenu menu = null;

            if (LView_Overview.SelectedItems.Count == 1)
            {
                menu = new ContextMenu();

                MenuItem removeAsset_MenuItem = new MenuItem();
                removeAsset_MenuItem.Header = "Remove Asset";
                removeAsset_MenuItem.Click += RemoveAsset_MenuItem_Click;
                menu.Items.Add(removeAsset_MenuItem);
            }

            LView_Overview.ContextMenu = menu;
        }

        private void RemoveAsset_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_Overview.SelectedItems.Count == 1)
            {
                Structure struc = (Structure)LView_Overview.SelectedItem;
                
                if (struc.Type == BaseStructure.Types.METMAST)
                {
                    int index = meteoFile.MetMasts.FindIndex(x => x.UnitID == struc.UnitID);
                    meteoFile.MetMasts.RemoveAt(index);
                    meteoFile.InclMetm.Remove(struc.UnitID);
                }
                else if (struc.Type == BaseStructure.Types.TURBINE)
                {
                    int index = scadaFile.WindFarm.FindIndex(x => x.UnitID == struc.UnitID);
                    scadaFile.WindFarm.RemoveAt(index);
                    scadaFile.InclTrbn.Remove(struc.UnitID);
                }

                loadedAsset.Remove(struc.UnitID);
                RemoveSingleAsset(struc.UnitID);
            }
        }

        private void LView_Powr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuPowerEvents();
        }

        private void SetContextMenuPowerEvents()
        {
            ContextMenu menu = null;

            if (LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1)
            {
                menu = new ContextMenu();

                MenuItem explorEvent_MenuItem = new MenuItem();
                explorEvent_MenuItem.Header = "Explore Event";
                explorEvent_MenuItem.Click += ExploreEvent_MenuItem_Click;
                menu.Items.Add(explorEvent_MenuItem);
            }

            if (LView_PowrNone.SelectedItems.Count == 1)
            {
                LView_PowrNone.ContextMenu = menu;
            }
            else if (LView_PowrRted.SelectedItems.Count == 1)
            {
                LView_PowrRted.ContextMenu = menu;
            }
        }
        
        private void ExploreEvent_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1)
                {
                    EventData thisEv = LView_PowrNone.SelectedItems.Count == 1 ?
                        (EventData)LView_PowrNone.SelectedItem : (EventData)LView_PowrRted.SelectedItem;

                    // do something part of the method
                    // this one needs to take the event details and send it to another listbox plus graph

                    // thisEvent has the event data only -- for the actual data to display, we'll need to find 
                    // the datapoints from the source data

                    List<ScadaData.ScadaSample> thisEvScada = new List<ScadaData.ScadaSample>();
                    List<ScadaData.ScadaSample> weekHistory = new List<ScadaData.ScadaSample>();
                    List<ScadaData.ScadaSample> dataHistory = new List<ScadaData.ScadaSample>();

                    // get index of the asset and get index of the event time in the asset
                    // the index of the asset to be used below
                    int assetIndex = scadaFile.WindFarm.FindIndex(x => x.UnitID == thisEv.FromAsset);
                    // the index of the timestamp a week before the event began or otherwise the first timestamp in the series - long conditional but should work
                    TimeSpan stepBack = new TimeSpan(0, -60 * 24 * 5, 0);
                    int weekIndex = scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0].Add(stepBack)) != -1 ? scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0].Add(stepBack)) : 0;

                    int timeIndex = scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0]);

                    for (int i = 0; i < thisEv.EvTimes.Count; i++)
                    {
                        thisEvScada.Add(scadaFile.WindFarm[assetIndex].DataSorted[timeIndex + i]);
                    }

                    for (int i = weekIndex; i < (timeIndex + thisEv.EvTimes.Count); i++)
                    {
                        weekHistory.Add(scadaFile.WindFarm[assetIndex].DataSorted[i]);
                    }

                    for (int j = 0; j < (timeIndex + thisEv.EvTimes.Count); j++)
                    {
                        dataHistory.Add(scadaFile.WindFarm[assetIndex].DataSorted[j]);
                    }

                    // assign the created dataset lists to their global variables
                    _thisEventData = new ObservableCollection<ScadaData.ScadaSample>(thisEvScada);
                    _weekEventData = new ObservableCollection<ScadaData.ScadaSample>(weekHistory);
                    _histEventData = new ObservableCollection<ScadaData.ScadaSample>(dataHistory);

                    // now sent the thisEvScada to the new ListView to populate it
                    InitializeEventExploration(sender, e);
                }
            }
            catch
            {
                MessageBox.Show("A problem has come up with the code in loading this event. Have the programmer check the indices.","Warning!");
            }
        }
        
        private void ChangeListViewDataset(object sender, RoutedEventArgs e)
        {
            // checks whether the dataset choice button has been activated and displays the respective
            // -- either all historic data to the end of the event, or only partial data for the duration
            // of the event

            if (CBox_DataSetChoice.IsChecked.Value)
            {
                LView_EventExplorer_Gearbox.ItemsSource = _histEventData;
                LView_EventExplorer_Generator.ItemsSource = _histEventData;
                LView_EventExplorer_MainBear.ItemsSource = _histEventData;
            }
            else
            {
                LView_EventExplorer_Gearbox.ItemsSource = _thisEventData;
                LView_EventExplorer_Generator.ItemsSource = _thisEventData;
                LView_EventExplorer_MainBear.ItemsSource = _thisEventData;
            }
        }

        private void InitializeEventExploration(object sender, RoutedEventArgs e)
        {
            Comb_EquipmentChoice.IsEnabled = true;
            LBL_EquipmentChoice.IsEnabled = true;
            CBox_DataSetChoice.IsEnabled = true;

            // first add it to the gridview on the list
            LView_EventExplorer_Gearbox.ItemsSource = _thisEventData;
            LView_EventExplorer_Generator.ItemsSource = _thisEventData;
            LView_EventExplorer_MainBear.ItemsSource = _thisEventData;

            ChangeListViewDataset(sender, e);
            DisplayCorrectEventDetails();

            // lastly also add respective dataviews to the chart and also to the viewmodel

            //ScrollableViewModel sVM = new ScrollableViewModel(histEventData.ToList());

            //ScrollView.Visibility = Visibility.Visible;
            //ScrollView.DataContext = sVM;
        }

        #endregion

        #region Treeview Controls

        private void TWI_Ev_LoSp_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_LoWinds.IsSelected = true;
        }

        private void TWI_Ev_HiSp_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_HiWinds.IsSelected = true;
        }
        
        private void TWI_Ev_NoPw_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_NoPower.IsSelected = true;
        }

        private void TWI_Ev_RtPw_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_RtPower.IsSelected = true;
        }

        private void TWI_Ev_LoSp_Click(object sender, RoutedEventArgs e)
        {
            Tab_LoWinds.IsSelected = true;
        }

        private void TWI_Ev_HiSp_Click(object sender, RoutedEventArgs e)
        {
            Tab_HiWinds.IsSelected = true;
        }

        private void TWI_Ev_NoPw_Click(object sender, RoutedEventArgs e)
        {
            Tab_NoPower.IsSelected = true;
        }

        private void TWI_Ev_RtPw_Click(object sender, RoutedEventArgs e)
        {
            Tab_RtPower.IsSelected = true;
        }

        #endregion 

        #region Support Classes
        
        public class DataOverview
        {
            #region Variables

            private int intValue;
            private string strValue;

            #endregion 

            public DataOverview(string strValue, int intValue)
            {
                this.strValue = strValue;
                this.intValue = intValue;
            }

            #region Properties

            public int IntegerData { get { return intValue; } set { intValue = value; } }
            public string StringData { get { return strValue; } set { strValue = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public GeoData GeoFile { get { return geoFile; } set { geoFile = value; } }
        public MeteoData MeteoFile { get { return meteoFile; } set { meteoFile = value; } }
        public ScadaData ScadaFile { get { return scadaFile; } set { scadaFile = value; } }

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }
        
        public static bool Mnt_Night { get { return mnt_Night; } set { mnt_Night = value; } }
        public static bool Mnt_AstDw { get { return mnt_AstDw; } set { mnt_AstDw = value; } }
        public static bool Mnt_NauDw { get { return mnt_NauDw; } set { mnt_NauDw = value; } }
        public static bool Mnt_CivDw { get { return mnt_CivDw; } set { mnt_CivDw = value; } }
        public static bool Mnt_Daytm { get { return mnt_Daytm; } set { mnt_Daytm = value; } }
        public static bool Mnt_CivDs { get { return mnt_CivDs; } set { mnt_CivDs = value; } }
        public static bool Mnt_NauDs { get { return mnt_NauDs; } set { mnt_NauDs = value; } }
        public static bool Mnt_AstDs { get { return mnt_AstDs; } set { mnt_AstDs = value; } }

        public static double CutIn { get { return cutIn; } set { cutIn = value; } }
        public static double CutOut { get { return cutOut; } set { cutOut = value; } }
        public static double PowerLim { get { return powerLim; } set { powerLim = value; } }
        public static double RatedPwr { get { return ratedPwr; } set { ratedPwr = value; } }

        public static TimeSpan ScadaSeprtr { get { return _scadaSeprtr; } set { _scadaSeprtr = value; } }
        public static TimeSpan DuratFilter { get { return _duratFilter; } set { _duratFilter = value; } }
        public static TimeSpan WorkHoursMorning { get { return _workHrsMorning; } set { _workHrsMorning = value; } }
        public static TimeSpan WorkHoursEvening { get { return _workHrsEvening; } set { _workHrsEvening = value; } }

        public List<DataOverview> Overview
        {
            get { return _overview; }
            set
            {
                if (_overview != value)
                {
                    _overview = value;
                    OnPropertyChanged("Overview");
                }
            }
        }
            
        public ObservableCollection<EventData> AllWtrView
        {
            get { return new ObservableCollection<EventData>(analyser.AllWtrEvts); }
            set { analyser.AllWtrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> LoSpdViews
        {
            get { return new ObservableCollection<EventData>(analyser.LoSpEvents); }
            set { analyser.LoSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> HiSpdViews
        {
            get { return new ObservableCollection<EventData>(analyser.HiSpEvents); }
            set { analyser.HiSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> NoPowViews
        {
            get { return new ObservableCollection<EventData>(analyser.NoPwEvents); }
            set { analyser.NoPwEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> AllPowView
        {
            get { return new ObservableCollection<EventData>(analyser.AllPwrEvts); }
            set { analyser.AllPwrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> RtdPowView
        {
            get { return new ObservableCollection<EventData>(analyser.RtPwEvents); }
            set { analyser.RtPwEvents = value.ToList(); }
        }

        public ObservableCollection<Structure> AssetsView
        {
            get { return _assetsVw = new ObservableCollection<Structure>(analyser.AssetsList); }
            set
            {
                if (_assetsVw != value)
                {
                    _assetsVw = value;
                    OnPropertyChanged(nameof(analyser.AssetsList));
                }
            }
        }

        #endregion
    }

    #region Project Specific Exceptions

    public class CancelLoadingException : Exception { }

    public class DataNotProcessedException : Exception { }

    public class FutureDevelopmentException : Exception { }

    public class LoadingCancelledException : Exception { }

    public class NoFilesInFolderException : Exception { }

    public class UnsuitableTargetFileException : Exception { }

    public class WritingCancelledException : Exception { }

    public class WrongFileTypeException : Exception { }

    #endregion
}
