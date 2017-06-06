using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace scada_analyst.Shared
{
    public partial class Window_VersionHistory : Window
    {
        public Window_VersionHistory(Window owner)
            : this(owner, VersionHistory.GetChanges(-1))
        {
            Title = "Version history";
        }

        private Window_VersionHistory(Window owner, List<VersionHistory.ProgramVersion> changes)
        {
            InitializeComponent();

            Owner = owner;

            Title = "Latest changes";

            CollectionViewSource changesViewSource = (CollectionViewSource)this.FindResource("LatestChanges");
            changesViewSource.Source = changes;
        }
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CollectionViewSource changesViewSource = (CollectionViewSource)this.FindResource("LatestChanges");
            changesViewSource.Source = null;
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
