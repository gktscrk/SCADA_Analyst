using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Reflection;

using MahApps.Metro.Controls;

namespace scada_analyst.Shared
{
    public partial class Window_About : MetroWindow
    {
        public Window_About(MetroWindow owner)
        {
            InitializeComponent();

            Owner = owner;

            Assembly app = Assembly.GetExecutingAssembly();

            AssemblyTitleAttribute title = (AssemblyTitleAttribute)app.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0];
            AssemblyProductAttribute product = (AssemblyProductAttribute)app.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0];
            AssemblyCopyrightAttribute copyright = (AssemblyCopyrightAttribute)app.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0];
            AssemblyCompanyAttribute company = (AssemblyCompanyAttribute)app.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)[0];
            AssemblyDescriptionAttribute description = (AssemblyDescriptionAttribute)app.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)[0];
            Version version = app.GetName().Version;

            this.Title = String.Format("About {0}", title.Title);
            this.txtblkAppTitle.Text = string.Format("{0} : Version {1}", title.Title, version);
            environment_TextBlock.Text = string.Format(".NET4.5 version, {0}-bit", Environment.Is64BitProcess ? 64 : 32);
            this.txtblkCopyright.Text = copyright.Copyright;
            this.txtblkDescription.Text = description.Description;
        }

        private void SupportLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Hyperlink thisLink = (Hyperlink)sender;
            string navigateUri = thisLink.NavigateUri.ToString();
            Process.Start(new ProcessStartInfo(navigateUri));
            e.Handled = true;
        }

        private void VersionHistory_Button_Click(object sender, RoutedEventArgs e)
        {
            new Window_VersionHistory(this).ShowDialog();
        }
    }
}