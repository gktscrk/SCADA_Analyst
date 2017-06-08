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

        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource cts;

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
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            scadaFile = null; scadaLoaded = false;

            StructureLocations();
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void LoadGeo(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            var progressHandler = new Progress<string>(value =>
            {
                label_ProgressBar.Content = value;
            });
            var progress = progressHandler as IProgress<string>;

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Location files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();
                    
                    await Task.Run(() => GeographyLoading(openFileDialog.FileNames));

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

        private async void LoadMet(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            var progressHandler = new Progress<string>(value =>
            {
                label_ProgressBar.Content = value;
            });
            var progress = progressHandler as IProgress<string>;

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Meteorology files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => MeteorologyLoading(meteoFile, openFileDialog.FileNames, meteoLoaded));

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

        private async void LoadScada(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            var progressHandler = new Progress<string>(value =>
            {
                label_ProgressBar.Content = value;
            });
            var progress = progressHandler as IProgress<string>;

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "SCADA files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();
                    
                    await Task.Run(() => ScadaLoading(scadaFile, openFileDialog.FileNames, scadaLoaded));

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

        private void GeographyLoading(string[] filenames)
        {
            try
            {
                for (int i = 0; i < filenames.Length; i++)
                {
                    if (!loadedFiles.Contains(filenames[i]))
                    {
                        geoFile = new GeoData(filenames[i]);

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

        private void MeteorologyLoading(MeteoData existingData, string[] filenames, bool isLoaded)
        {
            try
            {
                MeteoData analysis = existingData;

                if (!isLoaded)
                {
                    analysis = new MeteoData(filenames);
                }
                else
                {
                    analysis.AppendFiles(filenames);
                }

                meteoFile = analysis;
                meteoLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        private void ScadaLoading(ScadaData existingData, string[] filenames, bool isLoaded)
        {
            try
            {
                ScadaData analysis = existingData;

                if (!isLoaded)
                {
                    analysis = new ScadaData(filenames);
                }
                else
                {
                    analysis.AppendFiles(filenames);
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

        public class ProcessList : INotifyPropertyChanged
        {
            private string fileName, displayName;
            private int mergeIndex, mergeGroup, fileIndex, fileID;

            public ProcessList(string filename, bool splitName = false)
            {
                fileName = filename;
                displayName = System.IO.Path.GetFileNameWithoutExtension(filename);

                if (splitName)
                {
                    string[] nameParts = Common.GetSplits(displayName, new char[] { ' ', '-', '_' });

                    fileID = Convert.ToInt16(nameParts[nameParts.Length - 2]);

                    if (Convert.ToInt16(nameParts[nameParts.Length - 1]) < 99)
                    {
                        fileIndex = Convert.ToInt16(nameParts[nameParts.Length - 1]);

                        displayName = displayName.Replace(nameParts[nameParts.Length - 1], "");
                        displayName = displayName.Replace(" - ", "");
                    }
                }

                mergeIndex = -1;
                mergeGroup = -1;
            }

            #region Properties

            public string FileName { get { return fileName; } }
            public string DisplayName { get { return displayName; } set { displayName = value; } }
            public int FileIndex { get { return fileIndex; } set { fileIndex = value; } }
            public int FileID { get { return fileID; } set { fileID = value; } }

            public int MergeIndex
            {
                get { return mergeIndex; }
                set
                {
                    mergeIndex = value;
                    NotifyPropertyChanged("MergeIndex");
                }
            }

            public int MergeGroup
            {
                get { return mergeGroup; }
                set
                {
                    mergeGroup = value;
                    NotifyPropertyChanged("MergeGroup");
                }
            }

            #endregion

            #region Property changed

            public event PropertyChangedEventHandler PropertyChanged;

            protected void NotifyPropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

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
