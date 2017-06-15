using System;
using System.Collections.Generic;
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

using scada_analyst.Shared;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables

        private bool geoLoaded = false;
        private bool meteoLoaded = false;
        private bool posnsCombnd = false;
        private bool scadaLoaded = false;

        private bool exportPowMaxm = false, exportAmbMaxm = false, exportWSpMaxm = false;
        private bool exportPowMinm = false, exportAmbMinm = false, exportWSpMinm = false;
        private bool exportPowMean = false, exportAmbMean = false, exportWSpMean = false;
        private bool exportPowStdv = false, exportAmbStdv = false, exportWSpStdv = false;

        private bool exportGBxMaxm = false, exportGenMaxm = false, exportMBrMaxm = false;
        private bool exportGBxMinm = false, exportGenMinm = false, exportMBrMinm = false;
        private bool exportGBxMean = false, exportGenMean = false, exportMBrMean = false;
        private bool exportGBxStdv = false, exportGenStdv = false, exportMBrStdv = false;

        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource cts;

        private List<Asset> assetList = new List<Asset>();
        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;

            LView_Overview.IsEnabled = false;
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        private void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();

                ProgressBarInvisible();
            }
        }

        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);
        }

        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            geoFile = null; geoLoaded = false;

            StructureLocations();
        }

        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            meteoFile = null; meteoLoaded = false;

            StructureLocations();
            UnPopulateOverview();
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            scadaFile = null; scadaLoaded = false;

            StructureLocations();
            UnPopulateOverview();
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ExportDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                // set a default file name
                saveFileDialog.FileName = ".csv";
                // set filters
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                if (saveFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                        exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                        exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                        exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                        exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                        exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                        exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv));

                    ProgressBarInvisible();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async void LoadGeoAsync(object sender, RoutedEventArgs e)
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
                    
                    await Task.Run(() => GeographyLoading(openFileDialog.FileNames, progress));

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

        private async void LoadMetAsync(object sender, RoutedEventArgs e)
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

                    await Task.Run(() => MeteorologyLoading(meteoFile, openFileDialog.FileNames, meteoLoaded,
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

        private async void LoadScadaAsync(object sender, RoutedEventArgs e)
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
                    
                    await Task.Run(() => ScadaLoading(scadaFile, openFileDialog.FileNames, scadaLoaded, progress));

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

        private void PopulateOverview()
        {
            LView_Overview.IsEnabled = true;

            if (meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    assetList.Add((Asset)meteoFile.MetMasts[i]);
                }
            }

            if (scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    assetList.Add((Asset)scadaFile.WindFarm[i]);
                }
            }

            LView_Overview.ItemsSource = assetList;
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

        private bool StructureLocations()
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
                                break;
                            }
                        }
                    }
                }

                return posnsCombnd = true;
            }
            else { return posnsCombnd = false; }
        }

        private void StructureLocations(object sender, RoutedEventArgs e)
        {
            StructureLocations();
        }

        private void UnPopulateOverview()
        {
            LView_Overview.Items.Clear();
            LView_Overview.IsEnabled = false;
        }

        #region Background Processes
        
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

        private void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgressBar.Value = value;
        }

        private void GeographyLoading(string[] filenames, IProgress<int> progress)
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

        private void MeteorologyLoading(MeteoData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                MeteoData analysis = existingData;

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

        private void ScadaLoading(ScadaData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = existingData;

                if (!isLoaded)
                {
                    analysis = new ScadaData(filenames, progress);
                }
                else
                {
                    analysis.AppendFiles(filenames, progress);
                }

                scadaFile = analysis;
                scadaLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Support Classes
        
        public class Asset : BaseStructure
        {
            #region Variables

            private DateTime startTime;
            private DateTime endTime;

            #endregion

            public Asset() { }

            private Asset(MeteoData.MetMastData metMast)
            {
                UnitID = metMast.UnitID;
                Type = metMast.Type;

                startTime = GetFirstOrLast(metMast.InclDtTm, true);
                endTime = GetFirstOrLast(metMast.InclDtTm, false);
            }

            private Asset(ScadaData.TurbineData turbine)
            {
                UnitID = turbine.UnitID;
                Type = turbine.Type;

                startTime = GetFirstOrLast(turbine.InclDtTm, true);
                endTime = GetFirstOrLast(turbine.InclDtTm, false);
            }

            private DateTime GetFirstOrLast(List<DateTime> times, bool getFirst)
            {
                DateTime result;

                List<DateTime> sortedTimes = times.OrderBy( s => s).ToList();

                if (getFirst)
                {
                    result = sortedTimes[0];
                }
                else
                {
                    result = sortedTimes[sortedTimes.Count - 1];
                }

                return result;
            }

            public static explicit operator Asset(MeteoData.MetMastData metMast)
            {
                return new Asset(metMast);
            }
            
            public static explicit operator Asset(ScadaData.TurbineData turbine)
            {
                return new Asset(turbine);
            }

            #region Properties

            public DateTime StartTime {  get { return startTime; } set { startTime = value; } }
            public DateTime EndTime { get { return endTime; } set { endTime = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool PosnsCombnd { get { return posnsCombnd; } set { posnsCombnd = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }

        #endregion

    }

    public class FutureDevelopmentException : Exception { }

    public class LoadingCancelledException : Exception { }

    public class WritingCancelledException : Exception { }

    public class WrongFileTypeException : Exception { }

    public class UnsuitableTargetFileException : Exception { }

    public class NoFilesInFolderException : Exception { }

    public class CancelLoadingException : Exception { }
}
