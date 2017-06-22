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

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using scada_analyst.Shared;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region Variables

        private bool geoLoaded = false, geoAssociated = false;
        private bool meteoLoaded = false;
        private bool positionsAddedToData = false;
        private bool scadaLoaded = false;

        private bool mnt_Night = false;
        private bool mnt_AstDw = false;
        private bool mnt_NauDw = false;
        private bool mnt_CivDw = true;
        private bool mnt_Daytm = true;
        private bool mnt_CivDs = true;
        private bool mnt_NauDs = false;
        private bool mnt_AstDs = false;

        private bool exportPowMaxm = false, exportAmbMaxm = false, exportWSpMaxm = false;
        private bool exportPowMinm = false, exportAmbMinm = false, exportWSpMinm = false;
        private bool exportPowMean = false, exportAmbMean = false, exportWSpMean = false;
        private bool exportPowStdv = false, exportAmbStdv = false, exportWSpStdv = false;

        private bool exportGBxMaxm = false, exportGenMaxm = false, exportMBrMaxm = false;
        private bool exportGBxMinm = false, exportGenMinm = false, exportMBrMinm = false;
        private bool exportGBxMean = false, exportGenMean = false, exportMBrMean = false;
        private bool exportGBxStdv = false, exportGenStdv = false, exportMBrStdv = false;

        private double cutIn = 4, cutOut = 25, powerLim = 0, ratedPwr = 2000; // ratedPwr always in kW !!!

        private List<int> loadedAsset = new List<int>();
        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource cts;

        private DateTime expStart = new DateTime();
        private DateTime expEnd = new DateTime();
        private TimeSpan duratFilter = new TimeSpan(0, 10, 0);

        // this is to allow changing the property of the timestep in the loaded scada data at some point
        private TimeSpan scadaSeprtr = new TimeSpan(0, 10, 0); 

        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();

        private List<DataOverview> overview = new List<DataOverview>();

        private ObservableCollection<EventData> allWtrEvts = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> loSpEvents = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> hiSpEvents = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> noPwEvents = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> rtPwEvents = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> allPwrEvts = new ObservableCollection<EventData>();

        private ObservableCollection<Structure> assetList = new ObservableCollection<Structure>();

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public MainWindow()
        {
            InitializeComponent();
            this.WindowState = WindowState.Maximized;

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;

            LView_Overview.IsEnabled = false;

            LView_WSpdEvLo.IsEnabled = false;
            LView_WSpdEvHi.IsEnabled = false;
            LView_PowrNone.IsEnabled = false;
            LView_PowrRted.IsEnabled = false;

            CreateAndUpdateDataSummary(); // Call this before GetPowerProdLabel as that will change one of the strings here

            LBL_DurationFilter.Content = duratFilter.ToString();
            LBL_PwrProdAmount.Content = GetPowerProdLabel();
        }

        private string GetPowerProdLabel()
        {
            return "Power Production: " + ratedPwr.ToString() + " kW";
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        private ObservableCollection<EventData> AddDaytimesToEvents(ObservableCollection<EventData> currentEvents, 
            IProgress<int> progress)
        {
            // this method will contain the search for whether a non power production
            // event took place during the day or during the night by calculating the 
            // relevant sunrise and sunset for that day

            int count = 0;

            for (int i = 0; i < currentEvents.Count; i++)
            {
                Structure asset = assetList.Where(x => x.UnitID == currentEvents[i].FromAsset).FirstOrDefault();

                currentEvents[i].DayTime = GetEventDayTime(currentEvents[i], asset);

                count++;

                if (count % 10 == 0)
                {
                    if (progress != null)
                    {
                        progress.Report((int)(i / currentEvents.Count * 100.0));
                    }
                }
            }

            return currentEvents;
        }

        private async void AddDaytimesToEvents(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if (assetList == null || assetList.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No structures have been loaded. Load structures before using this function.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && noPwEvents.Count == 0) ||
                        (Tab_RtPower.IsSelected && rtPwEvents.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if (geoFile == null && !geoAssociated)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                if (Tab_NoPower.IsSelected)
                {
                    var thisEventSet = noPwEvents;
                    noPwEvents = null;

                    await Task.Run(() => NoPwEvents = AddDaytimesToEvents(thisEventSet, progress));
                }
                else if (Tab_RtPower.IsSelected)
                {
                    var thisEventSet = rtPwEvents;
                    rtPwEvents = null;

                    await Task.Run(() => RtPwEvents = AddDaytimesToEvents(thisEventSet, progress));
                }

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private bool AddStructureLocations()
        {
            try
            {
                if (geoLoaded && (meteoLoaded || scadaLoaded))
                {
                    for (int i = 0; i < geoFile.GeoInfo.Count; i++)
                    {
                        if (meteoLoaded)
                        {
                            for (int ij = 0; ij < meteoFile.MetMasts.Count; ij++)
                            {
                                if (geoFile.GeoInfo[i].AssetID == meteoFile.MetMasts[ij].UnitID)
                                {
                                    meteoFile.MetMasts[ij].Position = geoFile.GeoInfo[i].Position;

                                    int assetIndex = assetList.IndexOf(
                                        assetList.Where(x => x.UnitID == meteoFile.MetMasts[ij].UnitID).FirstOrDefault());

                                    assetList[assetIndex].Position = geoFile.GeoInfo[i].Position;

                                    break;
                                }
                            }
                        }

                        if (scadaLoaded)
                        {
                            for (int ik = 0; ik < scadaFile.WindFarm.Count; ik++)
                            {
                                if (geoFile.GeoInfo[i].AssetID == scadaFile.WindFarm[ik].UnitID)
                                {
                                    scadaFile.WindFarm[ik].Position = geoFile.GeoInfo[i].Position;

                                    int assetIndex = assetList.IndexOf(
                                        assetList.Where(x => x.UnitID == scadaFile.WindFarm[ik].UnitID).FirstOrDefault());

                                    assetList[assetIndex].Position = geoFile.GeoInfo[i].Position;

                                    break;
                                }
                            }
                        }
                    }

                    return positionsAddedToData = true;
                }
                else { return positionsAddedToData = false; }

            }
            catch
            {
                throw;
            }
        }

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

                AddStructureLocations();

                geoAssociated = true;
            }
            catch (CancelLoadingException) { }
            catch
            {
                throw new Exception();
            }
        }

        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);

            ClearEvents(sender, e);

            expStart = new DateTime();
            expEnd = new DateTime();
        }
        
        private void ClearEvents(object sender, RoutedEventArgs e)
        {
            allWtrEvts = new ObservableCollection<EventData>();
            loSpEvents = new ObservableCollection<EventData>();
            hiSpEvents = new ObservableCollection<EventData>();

            allPwrEvts = new ObservableCollection<EventData>();
            noPwEvents = new ObservableCollection<EventData>();
            rtPwEvents = new ObservableCollection<EventData>();

            LView_PowrNone.ItemsSource = null;
            LView_PowrNone.IsEnabled = false;

            LView_PowrRted.ItemsSource = null;
            LView_PowrRted.IsEnabled = false;

            LView_WSpdEvLo.ItemsSource = null;
            LView_WSpdEvLo.IsEnabled = false;

            LView_WSpdEvHi.ItemsSource = null;
            LView_WSpdEvHi.IsEnabled = false;

            CreateAndUpdateDataSummary();
        }

        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            geoFile = null; geoLoaded = false; geoAssociated = false;

            AddStructureLocations();

            CreateAndUpdateDataSummary();
        }

        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            for (int i = assetList.Count - 1; i >= 0; i--)
            {
                if (assetList[i].Type == BaseStructure.Types.METMAST)
                {
                    loadedAsset.Remove(assetList[i].UnitID);
                    assetList.RemoveAt(i);
                }
            }

            meteoFile = null; meteoLoaded = false;

            meteoFile = new MeteoData();

            AddStructureLocations();

            CreateAndUpdateDataSummary();
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            for (int i = assetList.Count - 1; i >= 0; i--)
            {
                if (assetList[i].Type == BaseStructure.Types.TURBINE)
                {
                    loadedAsset.Remove(assetList[i].UnitID);
                    assetList.RemoveAt(i);
                }
            }

            scadaFile = null; scadaLoaded = false;

            scadaFile = new ScadaData();

            AddStructureLocations();

            CreateAndUpdateDataSummary();
        }

        private void CreateEventAssociations(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = 0; i < currentEvents.Count; i++)
                {
                    bool goIntoHiSpEvents = true;

                    for (int j = 0; j < loSpEvents.Count; j++)
                    {
                        if (currentEvents[i].EvTimes.Intersect(loSpEvents[j].EvTimes).Any())
                        {
                            currentEvents[i].AssocEv = EventData.EventAssoct.LO_SP;

                            goIntoHiSpEvents = false;
                            break;
                        }
                    }

                    if (goIntoHiSpEvents)
                    {
                        for (int k = 0; k < hiSpEvents.Count; k++)
                        {
                            if (currentEvents[i].EvTimes.Intersect(hiSpEvents[k].EvTimes).Any())
                            {
                                currentEvents[i].AssocEv = EventData.EventAssoct.HI_SP;

                                break;
                            }
                        }
                    }

                    if (currentEvents[i].AssocEv == EventData.EventAssoct.NONE)
                    { currentEvents[i].AssocEv = EventData.EventAssoct.OTHER; }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch
            {
                throw;
            }
        }

        private void EditDurationFilter(object sender, RoutedEventArgs e)
        {
            Window_NumberTwo getTimeDur = new Window_NumberTwo(this, "Duration Filter Settings",
                "Hours", "Minutes", false, false, duratFilter.TotalHours, duratFilter.Minutes);

            if (getTimeDur.ShowDialog().Value)
            {
                duratFilter = new TimeSpan((int)getTimeDur.NumericValue1, (int)getTimeDur.NumericValue2, 0);
                LBL_DurationFilter.Content = duratFilter.ToString();
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();            
        }

        private async void ExportMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", expStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", expEnd);

                            if (startCal.ShowDialog().Value)
                            {
                                expStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                expEnd = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => meteoFile.ExportFiles(progress, saveFileDialog.FileName,expStart,expEnd));

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

        private async void ExportScadaDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", expStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", expEnd);

                            if (startCal.ShowDialog().Value)
                            {
                                expStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                expEnd = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                            exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                            exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                            exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                            exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                            exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                            exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv,
                            expStart, expEnd));

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

        private void FindEvents(IProgress<int> progress)
        {
            // all of the find events methods follow a similar methodology
            //
            // firstly the full set of applicable data is taken to investigate
            // the criteria these are tested against are defined based on wind speed or power output
            // and a certain number of if-conditions need to be passed for the testing to take place
            //
            // namely whether the dataset is continuous in time, whether it doesn't end with the file,
            // and whether all of the tested samples meet the same conditions

            try
            {
                FindNoPowerEvents(progress);
                FindRatedPowerEvents(progress);
                FindWeatherFromMeteo(progress);
                FindWeatherFromScada(progress);
            }
            catch
            {
                throw;
            }
        }

        private async void FindEventsAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                ProgressBarVisible();

                ClearEvents(sender, e);

                await Task.Run(() => FindEvents(progress));

                ProgressBarInvisible();

                RefreshEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void FindNoPowerEvents(IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low power (defined as below 0)
            // times in order to determine when the turbine may have been inactive --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Powers.Mean < powerLim &&
                                scadaFile.WindFarm[i].DataSorted[j].Powers.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > scadaSeprtr)
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Powers.Mean > powerLim)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                noPwEvents.Add(new EventData(thisEvent, EventData.PwrProdType.NOPROD));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindRatedPowerEvents(IProgress<int> progress)
        {
            // this method sees when and how often the turbine was operating
            // at rated power in the time that we are inputting
            
            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Powers.Mean >= ratedPwr &&
                                scadaFile.WindFarm[i].DataSorted[j].Powers.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > scadaSeprtr)
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Powers.Mean < ratedPwr)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                rtPwEvents.Add(new EventData(thisEvent, EventData.PwrProdType.RATEDP));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromMeteo(IProgress<int> progress)
        {
            // this method investigates all loaded meteorologic files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (meteoFile.MetMasts != null)
                {
                    int count = 0;

                    for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                    {
                        for (int j = 0; j < meteoFile.MetMasts[i].MetDataSorted.Count; j++)
                        {
                            if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean < cutIn &&
                                meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean >= 0)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean > cutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                loSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                                allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                            }
                            else if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean > cutOut)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean < cutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                hiSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                                allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / meteoFile.MetMasts.Count +
                                        (double)j / meteoFile.MetMasts[i].MetDataSorted.Count / meteoFile.MetMasts.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromScada(IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean < cutIn &&
                                scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean >= 0)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean > cutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                loSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                                allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                            }
                            else if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean > cutOut)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean < cutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                hiSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                                allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public EventData.TimeOfEvent GetEventDayTime(EventData thisEvent, Structure thisStructure)
        {
            double tsunrise, tsunsets, civcrise, civcsets, astrrise, astrsets, nautrise, nautsets;

            Sunriset.AstronomicalTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out astrrise, out astrsets);

            TimeSpan astriseTime = TimeSpan.FromHours(astrrise);
            TimeSpan astsetsTime = TimeSpan.FromHours(astrsets);

            Sunriset.NauticalTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out nautrise, out nautsets);

            TimeSpan nauriseTime = TimeSpan.FromHours(nautrise);
            TimeSpan nausetsTime = TimeSpan.FromHours(nautsets);

            Sunriset.CivilTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out civcrise, out civcsets);

            TimeSpan civriseTime = TimeSpan.FromHours(civcrise);
            TimeSpan civsetsTime = TimeSpan.FromHours(civcsets);

            Sunriset.SunriseSunset(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out tsunrise, out tsunsets);

            TimeSpan sunriseTime = TimeSpan.FromHours(tsunrise);
            TimeSpan sunsetsTime = TimeSpan.FromHours(tsunsets);

            if (thisEvent.Start.TimeOfDay <= astriseTime)
            {
                return EventData.TimeOfEvent.NIGHTTM;
            }
            else if (thisEvent.Start.TimeOfDay <= nauriseTime)
            {
                return EventData.TimeOfEvent.AS_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= civriseTime)
            {
                return EventData.TimeOfEvent.NA_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= sunriseTime)
            {
                return EventData.TimeOfEvent.CI_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= sunsetsTime)
            {
                return EventData.TimeOfEvent.DAYTIME;
            }
            else if (thisEvent.Start.TimeOfDay <= civsetsTime)
            {
                return EventData.TimeOfEvent.CI_DUSK;
            }
            else if (thisEvent.Start.TimeOfDay <= nausetsTime)
            {
                return EventData.TimeOfEvent.NA_DUSK;
            }
            else if (thisEvent.Start.TimeOfDay <= astsetsTime)
            {
                return EventData.TimeOfEvent.AS_DUSK;
            }
            else
            {
                return EventData.TimeOfEvent.NIGHTTM;
            }
        }

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

                geoLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        private async void LoadGeoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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

        private async void LoadMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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

        private async void LoadScadaDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;    

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

        private void MatchEvents(IProgress<int> progress)
        {
            try
            {
                CreateEventAssociations(progress);

                foreach (EventData singleEvent in noPwEvents)
                {
                    allPwrEvts.Add(singleEvent);
                }

                foreach (EventData singleEvent in rtPwEvents)
                {
                    allPwrEvts.Add(singleEvent);
                }
                
                allPwrEvts.OrderBy(o => o.Start); // does this need a ToList()?

                RemovedMatchedEvents(progress);
            }
            catch
            {
                throw;
            }
        }

        private async void MatchEventsAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;

                await Task.Run(() => MatchEvents(progress));

                ProgressBarInvisible();

                RefreshEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void ProcessDurationFilter(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].Durat < duratFilter)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            currentEvents.RemoveAt(i);
                        });
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch
            {
                throw;
            }
        }

        private async void ProcessDurationFilterAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (duratFilter.TotalSeconds != 0)
                {
                    if (noPwEvents.Count != 0)
                    {
                        ProgressBarVisible();

                        await Task.Run(() => ProcessDurationFilter(progress));

                        ProgressBarInvisible();

                        RefreshEvents();
                    }
                    else
                    {
                        await this.ShowMessageAsync("Warning!", 
                            "There are no null power production events to filter.");
                    }
                }
                else
                {
                    await this.ShowMessageAsync("Warning!",
                        "The duration filter is set to 0 seconds. Please change the length of this filter.");
                }
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private ObservableCollection<EventData> RemoveProcessedDaytimes(ObservableCollection<EventData> currentEvents, 
            IProgress<int> progress)
        {
            int count = 0;

            for (int i = currentEvents.Count - 1; i >= 0; i--)
            {
                if (currentEvents[i].DayTime == EventData.TimeOfEvent.NIGHTTM && mnt_Night)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.AS_DAWN && mnt_AstDw)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.NA_DAWN && mnt_NauDw)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.CI_DAWN && mnt_CivDw)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.DAYTIME && mnt_Daytm)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.CI_DUSK && mnt_CivDs)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.NA_DUSK && mnt_NauDs)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }
                else if (currentEvents[i].DayTime == EventData.TimeOfEvent.AS_DUSK && mnt_AstDs)
                {
                    App.Current.Dispatcher.Invoke((Action)delegate // 
                    {
                        currentEvents.RemoveAt(i);
                    });
                }

                count++;

                if (count % 10 == 0)
                {
                    if (progress != null)
                    {
                        progress.Report((int)(i / currentEvents.Count * 100.0));
                    }
                }
            }

            return currentEvents;
        }

        private async void RemoveProcessedDaytimesAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if ((Tab_NoPower.IsSelected && noPwEvents.Count == 0) ||
                        (Tab_RtPower.IsSelected && rtPwEvents.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && (noPwEvents.Count > 0 && noPwEvents[0].DayTime == EventData.TimeOfEvent.UNKNOWN)) ||
                    (Tab_RtPower.IsSelected && (rtPwEvents.Count > 0 && rtPwEvents[0].DayTime == EventData.TimeOfEvent.UNKNOWN)))
                {
                    await this.ShowMessageAsync("Warning!",
                        "The events cannot be removed before the day-time associations have been created.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                if (Tab_NoPower.IsSelected)
                {
                    var thisEventSet = noPwEvents;
                    noPwEvents = null;

                    await Task.Run(() => NoPwEvents = RemoveProcessedDaytimes(thisEventSet, progress));                    
                }
                else if (Tab_RtPower.IsSelected)
                {
                    var thisEventSet = rtPwEvents;
                    rtPwEvents = null;

                    await Task.Run(() => RtPwEvents = RemoveProcessedDaytimes(thisEventSet,progress));
                }

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void RemovedMatchedEvents(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].AssocEv == EventData.EventAssoct.LO_SP ||
                        currentEvents[i].AssocEv == EventData.EventAssoct.HI_SP)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            currentEvents.RemoveAt(i);
                        });
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(50 + 0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch { throw; }
        }

        private void ResetNoPwrProdEvents(object sender, RoutedEventArgs e)
        {
            noPwEvents.Clear();

            for (int i = 0; i < allPwrEvts.Count; i++)
            {
                if (allPwrEvts[i].PwrProd == EventData.PwrProdType.NOPROD)
                {
                    noPwEvents.Add(allPwrEvts[i]);
                }
            }

            duratFilter = new TimeSpan(0, 10, 0);
            LBL_DurationFilter.Content = duratFilter.ToString();

            CreateAndUpdateDataSummary();
        }

        private void ResetRtdPwrProdEvents(object sender, RoutedEventArgs e)
        {
            rtPwEvents.Clear();

            for (int i = 0; i < rtPwEvents.Count; i++)
            {
                if (allPwrEvts[i].PwrProd == EventData.PwrProdType.RATEDP)
                {
                    noPwEvents.Add(allPwrEvts[i]);
                }
            }

            CreateAndUpdateDataSummary();
        }

        private void SetAnalysisSets(object sender, RoutedEventArgs e)
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
                mnt_Night, mnt_AstDw, mnt_NauDw, mnt_CivDw, mnt_Daytm, mnt_CivDs, mnt_NauDs, mnt_AstDs);

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

                rtPwEvents.Clear();
                LBL_PwrProdAmount.Content = GetPowerProdLabel();
                CreateAndUpdateDataSummary(); 
            }
        }
        
        private void SetExportVars(object sender, RoutedEventArgs e)
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

        #region Background Processes

        void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();

                ProgressBarInvisible();
            }
        }

        void CreateAndUpdateDataSummary()
        {
            LView_LoadedOverview.ItemsSource = null;

            overview.Add(new DataOverview("Structures", assetList.Count));
            overview.Add(new DataOverview("Low Winds", loSpEvents.Count));
            overview.Add(new DataOverview("High Winds", hiSpEvents.Count));
            overview.Add(new DataOverview("No Power", noPwEvents.Count));
            
            double ratedPwrMw = ratedPwr / 1000.0;

            overview.Add(new DataOverview("Power: " + Common.GetStringDecimals(ratedPwrMw,1) + "MW", rtPwEvents.Count));

            LView_LoadedOverview.ItemsSource = overview;
        }

        void PopulateOverview()
        {
            LView_Overview.IsEnabled = true;

            if (meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    if (!loadedAsset.Contains(meteoFile.MetMasts[i].UnitID))
                    {
                        assetList.Add((Structure)meteoFile.MetMasts[i]);

                        loadedAsset.Add(meteoFile.MetMasts[i].UnitID);
                    }
                }
            }

            if (scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    if (!loadedAsset.Contains(scadaFile.WindFarm[i].UnitID))
                    {
                        assetList.Add((Structure)scadaFile.WindFarm[i]);

                        loadedAsset.Add(scadaFile.WindFarm[i].UnitID);
                    }
                }
            }

            LView_Overview.ItemsSource = assetList;
            LView_Overview.Items.Refresh();

            if (assetList != null && assetList.Count > 0)
            {
                expStart = assetList[0].StartTime;
                expEnd = assetList[0].EndTime;

                for (int i = 1; i < assetList.Count; i++)
                {
                    if (assetList[i].StartTime < expStart) { expStart = assetList[i].StartTime; }
                    if (assetList[i].EndTime > expEnd) { expEnd = assetList[i].EndTime; }
                }

                CreateAndUpdateDataSummary();
            }
        }

        void ProgressBarVisible()
        {
            progress_ProgressBar.Visibility = Visibility.Visible;
            label_ProgressBar.Visibility = Visibility.Visible;
            cancel_ProgressBar.Visibility = Visibility.Visible;
            //counter_ProgressBar.Visibility = Visibility.Visible;
        }
        
        void ProgressBarInvisible()
        {
            progress_ProgressBar.Visibility = Visibility.Collapsed;
            progress_ProgressBar.Value = 0;

            label_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Content = "";
            cancel_ProgressBar.Visibility = Visibility.Collapsed;

            //counter_ProgressBar.Content = "";
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        void RefreshEvents()
        {
            if (noPwEvents.Count != 0)
            {
                LView_PowrNone.IsEnabled = true;
                LView_PowrNone.ItemsSource = NoPwEvents;
                LView_PowrNone.Items.Refresh();
            }

            if (noPwEvents.Count != 0)
            {
                LView_PowrRted.IsEnabled = true;
                LView_PowrRted.ItemsSource = RtPwEvents;
                LView_PowrRted.Items.Refresh();
            }

            if (loSpEvents.Count != 0)
            {
                LView_WSpdEvLo.IsEnabled = true;
                LView_WSpdEvLo.ItemsSource = LoSpEvents;
                LView_WSpdEvLo.Items.Refresh();
            }

            if (hiSpEvents.Count != 0)
            {
                LView_WSpdEvHi.IsEnabled = true;
                LView_WSpdEvHi.ItemsSource = HiSpEvents;
                LView_WSpdEvHi.Items.Refresh();
            }

            CreateAndUpdateDataSummary();
        }
        
        void RemoveAllAssets()
        {
            LView_Overview.ItemsSource = null;
            LView_Overview.IsEnabled = false;

            assetList.Clear();
        }

        void RemoveSingleAsset(int toRemove)
        {
            if (assetList.Count != 0)
            {
                for (int i = assetList.Count - 1; i >= 0; i--)
                {
                    if (assetList[i].UnitID == toRemove)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            assetList.RemoveAt(i);
                        });

                        break;
                    }
                }
            }

            LView_Overview.Items.Refresh();
        }

        void OnPropertyChanged(string name)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(name));
            }
        }

        //void UpdateDataOverview()
        //{
        //    LView_LoadedOverview.ItemsSource = null;

        //    overview[0].IntegerData = assetList.Count;
        //    overview[1].IntegerData = loSpEvents.Count;
        //    overview[2].IntegerData = hiSpEvents.Count;
        //    overview[3].IntegerData = noPwEvents.Count;
        //    overview[4].StringData = ratedPwr.ToString() + " kW Power";
        //    overview[4].IntegerData = rtPwEvents.Count;

        //    LView_LoadedOverview.ItemsSource = overview;
        //}

        void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgressBar.Value = value;
        }

        #endregion

        #region ContextMenu

        private void LView_Overview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuLocationBox();
        }

        private void SetContextMenuLocationBox()
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

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool PosnsCombnd { get { return positionsAddedToData; } set { positionsAddedToData = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }

        public double CutIn { get { return cutIn; } set { cutIn = value; } }
        public double CutOut { get { return cutOut; } set { cutOut = value; } }

        public TimeSpan DuratFilter
        {
            get { return duratFilter; }
            set
            {
                if (duratFilter != value)
                {
                    duratFilter = value;
                    OnPropertyChanged("DuratFilter");
                }
            }
        }

        public List<DataOverview> Overview
        {
            get { return overview; }
            set
            {
                if (overview != value)
                {
                    overview = value;
                    OnPropertyChanged("Overview");
                }
            }
        }

        public ObservableCollection<EventData> LoSpEvents
        {
            get { return loSpEvents; }
            set
            {
                if (loSpEvents!=value)
                {
                    loSpEvents = value;
                    OnPropertyChanged("LoSpEvents");
                }
            }
        }

        public ObservableCollection<EventData> HiSpEvents
        {
            get { return hiSpEvents; }
            set
            {
                if (hiSpEvents != value)
                {
                    hiSpEvents = value;
                    OnPropertyChanged("HiSpEvents");
                }
            }
        }

        public ObservableCollection<EventData> NoPwEvents
        {
            get { return noPwEvents; }
            set
            {
                if (noPwEvents != value)
                {
                    noPwEvents = value;
                    OnPropertyChanged("NoPwEvents");
                }
            }
        }

        public ObservableCollection<EventData> RtPwEvents
        {
            get { return rtPwEvents; }
            set
            {
                if (rtPwEvents != value)
                {
                    rtPwEvents = value;
                    OnPropertyChanged("RtPwEvents");
                }
            }
        }

        public ObservableCollection<Structure> AssetList
        {
            get { return assetList; }
            set
            {
                if (assetList != value)
                {
                    assetList = value;
                    OnPropertyChanged("AssetList");
                }
            }
        }

        #endregion
    }

    public class FutureDevelopmentException : Exception { }

    public class DataNotProcessedException : Exception { }

    public class LoadingCancelledException : Exception { }

    public class WritingCancelledException : Exception { }

    public class WrongFileTypeException : Exception { }

    public class UnsuitableTargetFileException : Exception { }

    public class NoFilesInFolderException : Exception { }

    public class CancelLoadingException : Exception { }
}
