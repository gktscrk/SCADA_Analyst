using System;
using System.Collections.Generic;
using System.Linq;

namespace scada_analyst.Shared
{
    public class BaseEventData : ObservableObject
    {
        #region Variables

        private int _sourceAsset;

        private DateTime _start;
        private DateTime _finit;
        private TimeSpan _durat;

        private Types _type;

        private List<DateTime> _evTimes = new List<DateTime>();

        #endregion
        
        #region Support Classes

        public enum Types
        {
            UNKNOWN,
            NOPOWER,
            WEATHER
        }

        #endregion 

        #region Properties

        public int SourceAsset { get { return _sourceAsset; } set { _sourceAsset = value; } }

        public DateTime Start { get { return _start; } set { _start = value; } }
        public DateTime Finit { get { return _finit; } set { _finit = value; } }
        public TimeSpan Durat { get { return _durat; } set { _durat = value; } }

        public Types Type { get { return _type; } set { _type = value; } }

        public List<DateTime> EvTimes { get { return _evTimes; } set { _evTimes = value; } }

        #endregion
    }

    public class BaseMetaData
    {
        #region Variables

        // a list for including the asset IDs for all loaded turbines
        private List<int> _included = new List<int>();
        private List<int> _years = new List<int>();

        // a list for all loaded filenames
        private List<string> _fileName = new List<string>();
        
        #endregion

        #region Properties

        public List<int> Included { get { return _included; } set { _included = value; } }
        public List<int> Years { get { return _years; } set { _years = value; } }

        public List<string> FileName { get { return _fileName; } set { _fileName = value; } }
        
        #endregion
    }

    public class BaseStructure : ObservableObject
    {
        #region Variables

        private bool _positionsLoaded = false;
        private int _unitID = -1;

        private GridPosition _position;
        private MetaDataSetup _bearings = new MetaDataSetup();
        private MetaDataSetup _capacity = new MetaDataSetup();
        private Types _type = Types.UNKNOWN;

        private List<DateTime> _inclSamples = new List<DateTime>();
        
        #endregion

        #region Support Classes

        public class MetaDataSetup : ObservableObject
        {
            #region Variables

            private double _fullValue;

            private string _fullStr;

            private List<Year> _years = new List<Year>();

            #endregion

            #region Constructor

            public MetaDataSetup() { }

            public MetaDataSetup(MeteoData.MetMastData _input, MeteoData.MeteoHeader _meteoHeader, Mode _mode)
            {
                // this is a copy of the ScadaData method for the meteorological samples

                // create temporary variables for each year, etc, and add a counter
                // to add values together
                List<MeteoData.MeteoSample> _thisYearsData = new List<MeteoData.MeteoSample>();

                int _prevYear;
                int _thisYear;
                
                for (int i = 1; i < _input.MetDataSorted.Count; i++)
                {
                    // create the triggering option
                    _thisYear = _input.MetDataSorted[i].TimeStamp.Year;
                    _prevYear = _input.MetDataSorted[i - 1].TimeStamp.Year;

                    // if we are in the first position we also need to add in the zeroth info
                    if (i == 1) { _thisYearsData.Add(_input.MetDataSorted[0]); }

                    // if we have a new year, we need to add a new year into the data
                    if (_prevYear != _thisYear)
                    {
                        if (_mode == Mode.BEARINGS)
                        {
                            _years.Add(new Year(_thisYearsData, _meteoHeader));
                        }

                        // clear the info as has already been used
                        _thisYearsData.Clear();
                    }

                    // add samples to list
                    _thisYearsData.Add(_input.MetDataSorted[i]);

                    // if we are in the last position in the file we need to add a new year
                    if (i == _input.MetDataSorted.Count - 1)
                    {
                        if (_mode == Mode.BEARINGS)
                        {
                            _years.Add(new Year(_thisYearsData, _meteoHeader));
                        }
                    }
                }

                // lastly get the overall results from the major counters
                if (_mode == Mode.BEARINGS)
                {
                    // simply need the mode of all of the strings    
                    if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                    {
                        _fullStr = "Unknown";
                    }
                    else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                    {
                        _fullStr = _input.MetDataSorted.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                    else
                    {
                        _fullStr = _input.MetDataSorted.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                }
            }

            public MetaDataSetup(ScadaData.TurbineData _input, Mode _mode)
            {
                // this method has two entry points, one if we are dealing with bearings 
                // and another for the capacity factor calculations

                // create temporary variables for each year, etc, and add a counter
                // to add values together
                List<ScadaData.ScadaSample> _thisYearsData = new List<ScadaData.ScadaSample>();

                int _prevYear;
                int _thisYear;

                double _totalPower = 0;

                for (int i = 1; i < _input.DataSorted.Count; i++)
                {
                    // create the triggering option
                    _thisYear = _input.DataSorted[i].TimeStamp.Year;
                    _prevYear = _input.DataSorted[i - 1].TimeStamp.Year;

                    // if we are in the first position we also need to add in the zeroth info
                    if (i == 1) { _thisYearsData.Add(_input.DataSorted[0]); }

                    // if we have a new year, we need to add a new year into the data
                    if (_prevYear != _thisYear)
                    {
                        if (_mode == Mode.BEARINGS)
                        {
                            _years.Add(new Year(_thisYearsData));
                        }
                        else if (_mode == Mode.CAPACITY)
                        {
                            _years.Add(new Year(_thisYearsData, _input.RatedPower));
                        }

                        // clear the info as has already been used
                        _thisYearsData.Clear();
                    }

                    // add samples to list
                    _thisYearsData.Add(_input.DataSorted[i]);

                    // if we are in the last position in the file we need to add a new year
                    if (i == _input.DataSorted.Count - 1)
                    {
                        if (_mode == Mode.BEARINGS)
                        {
                            _years.Add(new Year(_thisYearsData));
                        }
                        else if (_mode == Mode.CAPACITY)
                        {
                            _years.Add(new Year(_thisYearsData, _input.RatedPower));
                        }
                    }

                    // increment the counters with the relevant info
                    if (_mode == Mode.CAPACITY)
                    {
                        if (!double.IsNaN(_input.DataSorted[i].Power.Mean))
                        {
                            if (i == 1) { _totalPower += _input.DataSorted[0].Power.Mean; }
                            _totalPower += _input.DataSorted[i].Power.Mean;
                        }
                    }
                }

                // lastly get the overall results from the major counters
                if (_mode == Mode.BEARINGS)
                {
                    // simply need the mode of all of the strings                        
                    _fullStr = _input.DataSorted.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                }
                else if (_mode == Mode.CAPACITY)
                {
                    _fullValue = Math.Round(_totalPower / (_input.RatedPower * _input.DataSorted.Count) * 100, 1);
                    _fullStr = Common.GetStringDecimals(_fullValue, 1);
                }
            }

            #endregion

            #region Support Classes

            public class Year : ObservableObject
            {
                #region Variables

                private int _year;

                private double _value;

                private string _yearStr = "";

                // this tuple will contain twelve values, each for one month
                private List<Tuple<int, double, string>> _values = new List<Tuple<int, double, string>>();

                #endregion

                #region Constructor

                /// <summary>
                /// This entry point facilitates the addition of a new set of yearly data for wind bearing data.
                /// </summary>
                /// <param name="_yearlyData"></param>
                public Year(List<ScadaData.ScadaSample> _yearlyData)
                {
                    // get year
                    _year = _yearlyData[0].TimeStamp.Year;

                    // create variables
                    int _prevMonth;
                    int _thisMonth;

                    List<ScadaData.ScadaSample> _monthData = new List<ScadaData.ScadaSample>();

                    for (int i = 1; i < _yearlyData.Count; i++)
                    {
                        // create tracking variables for the month
                        _thisMonth = _yearlyData[i].TimeStamp.Month;
                        _prevMonth = _yearlyData[i - 1].TimeStamp.Month;

                        // this here adds the first sample to the previous month whatever else happens
                        if (i == 1) { _monthData.Add(_yearlyData[0]); }

                        // conditional to check if the month is the same as the previous one
                        if (_thisMonth != _prevMonth)
                        {
                            string mode = _monthData.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            // if calculated, add it to the Tuple and reset relevant counters
                            _values.Add(new Tuple<int, double, string>(_prevMonth, double.NaN, mode));

                            _monthData.Clear();
                            _monthData.Add(_yearlyData[i]);
                        }
                        else if (_thisMonth == _prevMonth)
                        {
                            _monthData.Add(_yearlyData[i]);
                        }

                        // the last samples also need to be added to the values list to not miss out on anything
                        // so this check is necessary in case the last samples are not of a different month
                        if (i == _yearlyData.Count - 1)
                        {
                            string mode = _monthData.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            // if calculated, add it to the Tuple and reset relevant counters
                            _values.Add(new Tuple<int, double, string>(_prevMonth, double.NaN, mode));
                        }
                    }

                    // special provision if only one sample extends to the new year as the loop won't initialise
                    if (_yearlyData.Count == 1)
                    {
                        _values.Add(new Tuple<int, double, string>
                            (_yearlyData[0].TimeStamp.Month, double.NaN, _yearlyData[0].YawSys.YawPos.DStrShort));
                    }

                    // calculation of the yearly values - looking for the mode
                    _yearStr = _yearlyData.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                }

                public Year(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _meteoHeader)
                {
                    // get year
                    _year = _yearlyData[0].TimeStamp.Year;

                    // create variables
                    int _prevMonth;
                    int _thisMonth;

                    List<MeteoData.MeteoSample> _monthData = new List<MeteoData.MeteoSample>();

                    for (int i = 1; i < _yearlyData.Count; i++)
                    {
                        // create tracking variables for the month
                        _thisMonth = _yearlyData[i].TimeStamp.Month;
                        _prevMonth = _yearlyData[i - 1].TimeStamp.Month;

                        // this here adds the first sample to the previous month whatever else happens
                        if (i == 1) { _monthData.Add(_yearlyData[0]); }

                        // conditional to check if the month is the same as the previous one
                        if (_thisMonth != _prevMonth)
                        {
                            string mode = "";

                            if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                            {
                                mode = "Unknown";
                            }
                            else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                mode = _monthData.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }
                            else
                            {
                                mode = _monthData.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }

                            // if calculated, add it to the Tuple and reset relevant counters
                            _values.Add(new Tuple<int, double, string>(_prevMonth, double.NaN, mode));

                            _monthData.Clear();
                            _monthData.Add(_yearlyData[i]);
                        }
                        else if (_thisMonth == _prevMonth)
                        {
                            _monthData.Add(_yearlyData[i]);
                        }

                        // the last samples also need to be added to the values list to not miss out on anything
                        // so this check is necessary in case the last samples are not of a different month
                        if (i == _yearlyData.Count - 1)
                        {
                            string mode = "";

                            if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                            {
                                mode = "Unknown";
                            }
                            else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                mode = _monthData.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }
                            else
                            {
                                mode = _monthData.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }

                            // if calculated, add it to the Tuple and reset relevant counters
                            _values.Add(new Tuple<int, double, string>(_prevMonth, double.NaN, mode));
                        }
                    }

                    // special provision if only one sample extends to the new year as the loop won't initialise
                    if (_yearlyData.Count == 1)
                    {
                        string mode = "";

                        if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                        {
                            mode = "Unknown";
                        }
                        else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                        {
                            mode = _monthData.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                        }
                        else
                        {
                            mode = _monthData.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                        }

                        _values.Add(new Tuple<int, double, string>
                            (_yearlyData[0].TimeStamp.Month, double.NaN, mode));
                    }

                    // calculation of the yearly values - looking for the mode
                    if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                    {
                        _yearStr = "Unknown";
                    }
                    else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                    {
                        _yearStr = _yearlyData.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                    else
                    {
                        _yearStr = _yearlyData.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                }

                /// <summary>
                /// This entry point is for adding a new set of year data for capacity-based calculations.
                /// </summary>
                /// <param name="_yearlyData"></param>
                /// <param name="_ratedPower"></param>
                public Year(List<ScadaData.ScadaSample> _yearlyData, double _ratedPower)
                {
                    // get year
                    _year = _yearlyData[0].TimeStamp.Year;

                    // create variables
                    int _prevMonth;
                    int _thisMonth;
                    int _samplesPerMonth = 0;

                    double _powerCounter = 0;
                    double _monthCounter = 0;

                    for (int i = 1; i < _yearlyData.Count; i++)
                    {
                        // create tracking variables for the month
                        _thisMonth = _yearlyData[i].TimeStamp.Month;
                        _prevMonth = _yearlyData[i - 1].TimeStamp.Month;

                        // this here adds the first sample to the previous month whatever else happens
                        if (i == 1)
                        {
                            if (!double.IsNaN(_yearlyData[0].Power.Mean))
                            {
                                _monthCounter += _yearlyData[0].Power.Mean; _samplesPerMonth++;
                                _powerCounter += _yearlyData[0].Power.Mean;
                            }
                        }

                        // conditional to check if the month is the same as the previous one
                        if (_thisMonth != _prevMonth)
                        {
                            // monthValue represents the capacity factor for the month
                            double _monthValue = Math.Round(_monthCounter / (_ratedPower * _samplesPerMonth) * 100, 1);

                            // if calculated, add it to the Tuple and reset relevant counters
                            _values.Add(new Tuple<int, double, string>(_prevMonth, _monthValue, Common.GetStringDecimals(_monthValue, 1)));
                            _monthCounter = 0; _samplesPerMonth = 0;

                            // only increment value if it is not NaN
                            if (!double.IsNaN(_yearlyData[i].Power.Mean))
                            {
                                // add values to the counter
                                _monthCounter += _yearlyData[i].Power.Mean; _samplesPerMonth++;
                                _powerCounter += _yearlyData[i].Power.Mean;
                            }
                        }
                        else if (_thisMonth == _prevMonth)
                        {
                            if (!double.IsNaN(_yearlyData[i].Power.Mean))
                            {
                                // add values to the counter
                                _monthCounter += _yearlyData[i].Power.Mean; _samplesPerMonth++;
                                _powerCounter += _yearlyData[i].Power.Mean;
                            }
                        }

                        // the last samples also need to be added to the values list to not miss out on anything
                        // so this check is necessary in case the last samples are not of a different month
                        if (i == _yearlyData.Count - 1)
                        {
                            double _monthValue = Math.Round(_monthCounter / (_ratedPower * _samplesPerMonth) * 100, 1);
                            _values.Add(new Tuple<int, double, string>(_thisMonth, _monthValue, Common.GetStringDecimals(_monthValue, 1)));
                        }
                    }

                    // special provision if only one sample extends to the new year as the loop won't initialise
                    if (_yearlyData.Count == 1)
                    {
                        if (!double.IsNaN(_yearlyData[0].Power.Mean))
                        {
                            // add values to the counter
                            _monthCounter += _yearlyData[0].Power.Mean; _samplesPerMonth++;

                            double _monthValue = Math.Round(_monthCounter / (_ratedPower * _samplesPerMonth) * 100, 1);
                            _values.Add(new Tuple<int, double, string>
                                (_yearlyData[0].TimeStamp.Month, _monthValue, Common.GetStringDecimals(_monthValue, 1)));

                            _powerCounter += _yearlyData[0].Power.Mean;
                        }
                    }

                    // calculation for the yearly values
                    _value = Math.Round(_powerCounter / (_ratedPower * _yearlyData.Count) * 100, 1);
                    _yearStr = Common.GetStringDecimals(_value, 1);
                }

                #endregion

                #region Properties

                public int Years { get { return _year; } set { _year = value; } }
                public double Value { get { return _value; } set { _value = value; } }

                public string YearStr { get { return _yearStr; } set { _yearStr = value; } }

                public List<Tuple<int, double, string>> Values { get { return _values; } set { _values = value; } }

                #endregion
            }

            public enum Mode
            {
                BEARINGS,
                CAPACITY
            }

            #endregion

            #region Properties

            public double FullValue { get { return _fullValue; } set { _fullValue = value; } }

            public string FullStr { get { return _fullStr; } set { _fullStr = value; } }

            public List<Year> Years { get { return _years; } set { _years = value; } }

            #endregion
        }

        public enum Types
        {
            UNKNOWN,
            TURBINE,
            METMAST
        }

        #endregion

        #region Properties

        public bool PositionsLoaded { get { return _positionsLoaded; } set { _positionsLoaded = value; } }
        public int UnitID { get { return _unitID; } set { _unitID = value; } }

        public string PositionsLoadedDisplay { get { return PositionsLoaded == true ? "Added" : "None"; } set { PositionsLoadedDisplay = value; } }
        public string TypeString {  get { return _type == Types.METMAST ? "MetMast" : _type == Types.TURBINE ? "Turbine" : "Unknown"; } set { TypeString = value; } }
        
        public GridPosition Position {  get { return _position; } set { _position = value; } }
        public MetaDataSetup Bearings { get { return _bearings; } set { _bearings = value; } }
        public MetaDataSetup Capacity { get { return _capacity; } set { _capacity = value; } }
        public Types Type { get { return _type; } set { _type = value; } }

        public List<DateTime> InclSamples { get { return _inclSamples; } set { _inclSamples = value; } }
        
        #endregion
    }
    
    public class BaseSampleData
    {
        #region Variables

        private double _error = double.NaN;

        private int _assetID = 0;
        private int _sampleID = 0;
        private int _stationID = 0;

        private DateTime _timeStamp;
        private DateTime _timeStampEnd;
        private TimeSpan _deltaTime;

        #endregion

        protected double GetVals(double value, string[] data, double index)
        {
            if (double.IsNaN(value) && index != -1)
            {
                if (Common.CanConvert<double>(data[(int)index]))
                {
                    return Convert.ToDouble(data[(int)index]);
                }
                else
                {
                    return value;
                }
            }
            else
            {
                return value;
            }
        }

        #region Properties

        public double Error { get { return _error; } set { _error = value; } }

        public int AssetID { get { return _assetID; } set { _assetID = value; } }
        public int SampleID { get { return _sampleID; } set { _sampleID = value; } }
        public int StationID { get { return _stationID; } set { _stationID = value; } }

        public DateTime TimeStamp { get { return _timeStamp; } set { _timeStamp = value; } }
        public DateTime TimeStampEnd { get { return _timeStampEnd; } set { _timeStampEnd = value; } }
        public TimeSpan SampleSeparation { get { return _deltaTime; } set { _deltaTime = value; } }

        #endregion
    }

    public class Stats
    {
        #region Variables

        private string _description = "";

        protected double _minm = double.NaN;
        protected double _maxm = double.NaN;
        protected double _mean = double.NaN;
        protected double _stdv = double.NaN;

        protected double _dMean = double.NaN;

        #endregion

        #region Properties

        public string Description { get { return _description; } set { _description = value; } }

        public double Minm { get { return _minm; } set { _minm = value; } }
        public double Maxm { get { return _maxm; } set { _maxm = value; } }
        public double Mean { get { return _mean; } set { _mean = value; } }
        public double Stdv { get { return _stdv; } set { _stdv = value; } }

        public double Dlta { get { return _dMean; } set { _dMean = value; } }

        #endregion
    }

    public class Equipment
    {
        #region Variables

        private string _name = "";

        #endregion

        #region Properties

        public string Name { get { return _name; } set { _name = value; } }

        #endregion
    }

    #region Units : Stats Derivatives

    public class Current : Phased { }

    public class Direction : Stats
    {
        #region Properties

        public string DStr { get { return Common.BearingStringConversion((float)_mean); } set { DStr = value; } }
        public string DStrShort { get { return Common.BearingStringConversionShort((float)_mean); } set { DStrShort = value; } }

        #endregion
    }

    public class Frequency : Stats
    {
        #region Variables

        protected double endValue = double.NaN;
        protected int endValCol = -1;

        #endregion

        #region Properties

        public double EndValue { get { return endValue; } set { endValue = value; } }
        public int EndValCol { get { return endValCol; } set { endValCol = value; } }

        #endregion
    }

    public class Humidity : Stats { }

    public class Phased
    {
        #region Variables

        protected Stats _phR = new Stats();
        protected Stats _phS = new Stats();
        protected Stats _phT = new Stats();

        #endregion

        #region Properties

        public Stats PhR { get { return _phR; } set { _phR = value; } }
        public Stats PhS { get { return _phS; } set { _phS = value; } }
        public Stats PhT { get { return _phT; } set { _phT = value; } }

        #endregion
    }

    public class Power : Stats
    {
        #region Variables

        protected double endValue = double.NaN;
        protected int endValCol = -1;

        #endregion

        #region Properties

        public double EndValue { get { return endValue; } set { endValue = value; } }
        public int EndValCol { get { return endValCol; } set { endValCol = value; } }

        #endregion
    }

    public class Pressure : Stats { }
    public class Revolutions : Stats { }
    public class Speed : Stats { }
    public class Temperature : Stats { }
    public class Voltage : Phased { }

    #endregion 
}
