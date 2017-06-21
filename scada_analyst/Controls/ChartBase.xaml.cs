using System;
using System.Windows.Controls;

using LiveCharts;
using LiveCharts.Events;
using LiveCharts.Wpf;

using scada_analyst.Shared;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for ChartBase.xaml
    /// </summary>
    public partial class ScrollableView : UserControl
    {
        public ScrollableView()
        {
            InitializeComponent();
        }

        private void Axis_OnRangeChanged(RangeChangedEventArgs eventargs)
        {
            var vm = (ScrollableViewModel)DataContext;

            var currentRange = eventargs.Range;

            if (currentRange < TimeSpan.TicksPerDay * 2)
            {
                vm.Formatter = x => new DateTime((long)x).ToString("t");
                return;
            }

            if (currentRange < TimeSpan.TicksPerDay * 60)
            {
                vm.Formatter = x => new DateTime((long)x).ToString("dd MMM yy");
                return;
            }

            if (currentRange < TimeSpan.TicksPerDay * 540)
            {
                vm.Formatter = x => new DateTime((long)x).ToString("MMM yy");
                return;
            }

            vm.Formatter = x => new DateTime((long)x).ToString("yyyy");
        }
    }
}
