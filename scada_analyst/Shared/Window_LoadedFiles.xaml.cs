using System.Windows;
using System.Collections.ObjectModel;

using MahApps.Metro.Controls;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for Window_LoadedFiles.xaml
    /// </summary>
    public partial class Window_LoadedFiles : MetroWindow
    {
        #region Constructor

        public Window_LoadedFiles(MetroWindow owner, ObservableCollection<string> _inputData)
        {
            InitializeComponent();

            LView_LoadedFiles.ItemsSource = _inputData;
        }

        #endregion

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
