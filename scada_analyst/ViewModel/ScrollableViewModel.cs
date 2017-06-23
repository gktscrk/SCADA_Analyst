using System;
using System.Collections.Generic;
using System.ComponentModel;

using LiveCharts;
using LiveCharts.Defaults;
using System.Linq;

namespace scada_analyst.Controls
{
    public class ScrollableViewModel : ObservableObject
    {
        #region Variables

        private double _from;
        private double _to;

        private double _bottom;
        private double _top;

        private Func<double, string> _formatter;

        #endregion 

        public ScrollableViewModel()
        {
            var l = new List<DateTimePoint>();
            l.Add(new DateTimePoint(DateTime.Now, 0));

            Formatter = x => new DateTime((long)x).ToString("dd-MM-yyyy HH:mm");

            From = DateTime.Now.Ticks;
            To = DateTime.Now.AddHours(24).Ticks;

            Bottom = 0;
            Top = 1;
        }

        public ScrollableViewModel(List<ScadaData.ScadaSample> thisEvent)
        {
            var now = thisEvent[0].TimeStamp;
            
            var l = new List<DateTimePoint>();

            for (int i = 0; i < thisEvent.Count; i++)
            {
                l.Add(new DateTimePoint(thisEvent[i].TimeStamp, thisEvent[i].Gearbox.Hs.Gens.Mean));                
            }
                        
            Formatter = x => new DateTime((long)x).ToString("dd-MM-yyyy HH:mm");

            Values = l;

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

        public List<DateTimePoint> Values { get; set; }

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