using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Events;
using LiveCharts.Wpf;

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

    public class ScrollableViewModel : ObservableObject
    {
        #region Variables

        private double _from;
        private double _to;

        private double _bottom;
        private double _top;

        private List<DateTimePoint> list = new List<DateTimePoint>();

        private Func<double, string> _formatter;

        #endregion 

        public ScrollableViewModel()
        {
            list.Clear();

            list.Add(new DateTimePoint(DateTime.Now.AddDays(-365), 0));
            list.Add(new DateTimePoint(DateTime.Now, 0));

            Formatter = x => new DateTime((long)x).ToString("dd-MM-yyyy HH:mm");

            From = list[0].DateTime.Ticks;
            To = list[1].DateTime.Ticks;

            Bottom = 0;
            Top = 100;
        }

        public ScrollableViewModel(List<ScadaData.ScadaSample> thisEvent)
        {
            list.Clear();

            for (int i = 0; i < thisEvent.Count; i++)
            {
                list.Add(new DateTimePoint(thisEvent[i].TimeStamp, thisEvent[i].Gearbox.HsGen.Mean));
            }

            Formatter = x => new DateTime((long)x).ToString("dd-MM-yyyy HH:mm");

            Values = list;

            From = thisEvent[0].TimeStamp.Ticks;
            To = thisEvent[thisEvent.Count - 1].TimeStamp.Ticks;

            Bottom = Values.Min(x => x.Value) == -9999 ? 0 : Values.Min(x => x.Value) * 0.8;
            Top = Values.Max(x => x.Value) * 1.2;
        }

        #region Properties

        public object Mapper { get; set; }

        public double Bottom
        {
            get { return _bottom; }
            set
            {
                _bottom = value;
                OnPropertyChanged(nameof(Bottom));
            }
        }

        public double Top
        {
            get { return _top; }
            set
            {
                _top = value;
                OnPropertyChanged(nameof(Top));
            }
        }

        public double From
        {
            get { return _from; }
            set
            {
                _from = value;
                OnPropertyChanged(nameof(From));
            }
        }

        public double To
        {
            get { return _to; }
            set
            {
                _to = value;
                OnPropertyChanged(nameof(To));
            }
        }

        public List<DateTimePoint> Values { get { return list; } set { list = value; } }

        public Func<double, string> Formatter
        {
            get { return _formatter; }
            set
            {
                _formatter = value;
                OnPropertyChanged(nameof(Formatter));
            }
        }

        #endregion
    }
}
