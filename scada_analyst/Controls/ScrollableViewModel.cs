using System;
using System.Collections.Generic;
using System.ComponentModel;

using LiveCharts.Defaults;

namespace scada_analyst.Controls
{
    public class ScrollableViewModel : ObservableObject
    {
        #region Variables

        private double _from;
        private double _to;

        private Func<double, string> _formatter;

        #endregion 

        public ScrollableViewModel(List<ScadaData.ScadaSample> thisEvent)
        {
            var now = thisEvent[0].TimeStamp;
            
            var l = new List<DateTimePoint>();

            for (int i = 0; i < thisEvent.Count; i++)
            {
                l.Add(new DateTimePoint(thisEvent[i].TimeStamp, thisEvent[i].Gearbox.Hs.Gens.Mean));                
            }

            Formatter = x => new DateTime((long)x).ToString("yyyy");

            Values = l;

            From = thisEvent[0].TimeStamp.Ticks;
            To = thisEvent[thisEvent.Count - 1].TimeStamp.Ticks;
        }

        #region Properties

        public object Mapper { get; set; }

        public double From
        {
            get { return _from; }
            set
            {
                _from = value;
                OnPropertyChanged("From");
            }
        }

        public double To
        {
            get { return _to; }
            set
            {
                _to = value;
                OnPropertyChanged("To");
            }
        }

        public List<DateTimePoint> Values { get; set; }

        public Func<double, string> Formatter
        {
            get { return _formatter; }
            set
            {
                _formatter = value;
                OnPropertyChanged("Formatter");
            }
        }

        #endregion
    }
}