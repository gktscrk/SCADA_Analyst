using System;
using System.Collections.Generic;
using System.Data;
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
        private int _unitID = -1; // don't change from -1, otherwise unexpected behaviour might occur

        private GridPosition _position;
        private MetaDataSetup _bearings = new MetaDataSetup();
        private MetaDataSetup _capacity = new MetaDataSetup();
        private MetaDataSetup _windInfo = new MetaDataSetup();
        private Types _type = Types.UNKNOWN;

        private List<DateTime> _inclSamples = new List<DateTime>();
        
        #endregion

        #region Support Classes

        public class MetaDataSetup : ObservableObject
        {
            #region Variables

            private double _totalVal;

            private string _totalValStr;

            private List<Year> _yearList = new List<Year>();

            #endregion

            #region Constructor

            public MetaDataSetup() { }

            public MetaDataSetup(ScadaData.TurbineData _input, Mode _mode)
            {
                // this method has two entry points, one if we are dealing with bearings 
                // and another for the capacity factor calculations

                // create temporary variables for each year, etc, and add a counter
                // to add values together
                List<ScadaData.ScadaSample> _thisYearsData = new List<ScadaData.ScadaSample>();

                int _prevYear;
                int _thisYear;

                double _totalValue = 0;
                int _totalCounter = 0;

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
                            _yearList.Add(new Year(_prevYear, _thisYearsData, _mode));
                        }
                        else if (_mode == Mode.CAPACITY)
                        {
                            _yearList.Add(new Year(_prevYear, _thisYearsData, _input.RatedPower));
                        }
                        else if (_mode == Mode.WINDINFO)
                        {
                            _yearList.Add(new Year(_prevYear, _thisYearsData, _mode));
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
                            _yearList.Add(new Year(_thisYear, _thisYearsData, _mode));
                        }
                        else if (_mode == Mode.CAPACITY)
                        {
                            _yearList.Add(new Year(_thisYear, _thisYearsData, _input.RatedPower));
                        }
                        else if (_mode == Mode.WINDINFO)
                        {
                            _yearList.Add(new Year(_thisYear, _thisYearsData, _mode));
                        }
                    }

                    // increment the counters with the relevant info
                    if (_mode == Mode.CAPACITY)
                    {
                        if (!double.IsNaN(_input.DataSorted[i].Power.Mean))
                        {
                            if (i == 1) { _totalValue += _input.DataSorted[0].Power.Mean; _totalCounter++; }
                            _totalValue += _input.DataSorted[i].Power.Mean; _totalCounter++;
                        }
                    }
                    else if (_mode == Mode.WINDINFO)
                    {
                        if (!double.IsNaN(_input.DataSorted[i].Anemo.ActWinds.Mean))
                        {
                            if (i == 1) { _totalValue += _input.DataSorted[0].Anemo.ActWinds.Mean; _totalCounter++; }
                            _totalValue += _input.DataSorted[i].Anemo.ActWinds.Mean; _totalCounter++;
                        }
                    }
                }

                // lastly get the overall results from the major counters
                if (_mode == Mode.BEARINGS)
                {
                    // simply need the mode of all of the strings                        
                    _totalValStr = _input.DataSorted.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                }
                else if (_mode == Mode.CAPACITY)
                {
                    // calculating the percentage result of the total capacity
                    _totalVal = Math.Round(_totalValue / (_input.RatedPower * _totalCounter) * 100, 1);
                    _totalValStr = Common.GetStringDecimals(_totalVal, 1);
                }
                else if (_mode == Mode.WINDINFO)
                {
                    // calculating the average of all the results
                    _totalVal = Math.Round(_totalValue / _totalCounter, 2);
                    _totalValStr = Common.GetStringDecimals(_totalVal, 2);
                }
            }

            public MetaDataSetup(MeteoData.MetMastData _input, MeteoData.MeteoHeader _meteoHeader, Mode _mode)
            {
                // this is a copy of the ScadaData method for the meteorological samples

                // create temporary variables for each year, etc, and add a counter
                // to add values together
                List<MeteoData.MeteoSample> _thisYearsData = new List<MeteoData.MeteoSample>();

                int _prevYear;
                int _thisYear;

                double _totalValue = 0;
                int _counter = 0;

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
                            _yearList.Add(new Year(_prevYear, _thisYearsData, _meteoHeader, _mode));
                        }
                        else if (_mode == Mode.WINDINFO)
                        {
                            _yearList.Add(new Year(_prevYear, _thisYearsData, _meteoHeader, _mode));
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
                            _yearList.Add(new Year(_thisYear, _thisYearsData, _meteoHeader, _mode));
                        }
                        else if (_mode == Mode.WINDINFO)
                        {
                            _yearList.Add(new Year(_thisYear, _thisYearsData, _meteoHeader, _mode));
                        }
                    }

                    // increment counters
                    if (_mode == Mode.WINDINFO)
                    {
                        // increment the counter by the right value depending on what case we are dealing with
                        if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                        {
                            if (!double.IsNaN(_input.MetDataSorted[i].Speed.Metres10.Mean))
                            {
                                if (i == 1) { _totalValue += _input.MetDataSorted[0].Speed.Metres10.Mean; }
                                _totalValue += _input.MetDataSorted[i].Speed.Metres10.Mean; _counter++;
                            }
                        }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                        {
                            if (!double.IsNaN(_input.MetDataSorted[i].Speed.MetresRt.Mean))
                            {
                                if (i == 1) { _totalValue += _input.MetDataSorted[0].Speed.MetresRt.Mean; }
                                _totalValue += _input.MetDataSorted[i].Speed.MetresRt.Mean; _counter++;
                            }
                        }
                    }
                }

                // lastly get the overall results from the major counters
                if (_mode == Mode.BEARINGS)
                {
                    // simply need the mode of all of the strings    
                    if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                    {
                        _totalValStr = "Unknown";
                    }
                    else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                    {
                        _totalValStr = _input.MetDataSorted.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                    else if (_meteoHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                    {
                        _totalValStr = _input.MetDataSorted.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                }
                else if (_mode == Mode.WINDINFO)
                {
                    _totalVal = Math.Round(_totalValue / _counter, 2);
                    _totalValStr = Common.GetStringDecimals(_totalVal, 2);
                }
            }

            #endregion

            #region Support Classes

            public class Year : ObservableObject
            {
                #region Variables

                private int _yearName;

                private double _value;

                private string _valStr = "";

                // the DataTable will contain values for all weeks
                private DataTable _monthlyData;
                private DataTable _weeklyData;

                #endregion

                #region Constructor

                /// <summary>
                /// Calculate wind direction or speed values for a year.
                /// </summary>
                /// <param name="_year"></param>
                /// <param name="_yearlyData"></param>
                public Year(int _year, List<ScadaData.ScadaSample> _yearlyData, Mode _mode)
                {
                    _yearName = _year;

                    if (_mode == Mode.BEARINGS)
                    {
                        GetOverallDirectionString(_yearlyData);

                        _monthlyData = GetMonthlyDirectionData(_yearlyData);
                        _weeklyData = GetWeeklyDirectionData(_yearlyData);
                    }
                    else if (_mode == Mode.WINDINFO)
                    {
                        GetOverallAverage(_yearlyData);

                        _monthlyData = GetMonthlyWindSpeedData(_yearlyData);
                        _weeklyData = GetWeeklyWindSpeedData(_yearlyData);
                    }
                }

                /// <summary>
                /// Calculate wind direction or speed values for a year.
                /// </summary>
                /// <param name="_year"></param>
                /// <param name="_yearlyData"></param>
                public Year(int _year, List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header, Mode _mode)
                {
                    _yearName = _year;

                    if (_mode == Mode.BEARINGS)
                    {
                        GetOverallDirectionString(_yearlyData, _header);

                        _monthlyData = GetMonthlyDirectionData(_yearlyData, _header);
                        _weeklyData = GetWeeklyDirectionData(_yearlyData, _header);
                    }
                    else if (_mode == Mode.WINDINFO)
                    {
                        GetOverallAverage(_yearlyData, _header);

                        _monthlyData = GetMonthlyWindSpeedData(_yearlyData, _header);
                        _weeklyData = GetWeeklyWindSpeedData(_yearlyData, _header);
                    }
                }

                /// <summary>
                /// Calculate capacity factor values for a year.
                /// </summary>
                /// <param name="_year"></param>
                /// <param name="_yearlyData"></param>
                /// <param name="_ratedPower"></param>
                public Year(int _year, List<ScadaData.ScadaSample> _yearlyData, double _ratedPower)
                {
                    _yearName = _year;

                    GetOverallAverage(_yearlyData, _ratedPower);

                    _monthlyData = GetMonthlyPowerData(_yearlyData, _ratedPower);
                    _weeklyData = GetWeeklyPowerData(_yearlyData, _ratedPower);
                }
                
                /// <summary>
                /// This entry point allows adding a wind speed information sample of a year's length from a meteorological
                /// dataset.
                /// </summary>
                /// <param name="_yearlyData"></param>
                /// <param name="_meteoHeader"></param>
                /// <param name="_mode"></param>
                public void GetOverallAverage(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _meteoHeader)
                {
                    double _staticValues = 0; // this incrementor won't be reset into a new month
                    int _staticCounter = 0;

                    for (int i = 1; i < _yearlyData.Count; i++)
                    {
                        // this here adds the first sample to the previous month whatever else happens
                        if (i == 1)
                        {
                            if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                            else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                if (!double.IsNaN(_yearlyData[0].Speed.Metres10.Mean))
                                {
                                    _staticValues += _yearlyData[0].Speed.Metres10.Mean; _staticCounter++;
                                }
                            }
                            else if(_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                            {
                                if (!double.IsNaN(_yearlyData[0].Speed.MetresRt.Mean))
                                {
                                    _staticValues += _yearlyData[0].Speed.MetresRt.Mean; _staticCounter++;
                                }
                            }
                        }
                        
                        // only increment value if it is not NaN

                        if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                        {
                            if (!double.IsNaN(_yearlyData[i].Speed.Metres10.Mean))
                            {
                                _staticValues += _yearlyData[i].Speed.Metres10.Mean; _staticCounter++;
                            }
                        }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                        {
                            if (!double.IsNaN(_yearlyData[i].Speed.MetresRt.Mean))
                            {
                                _staticValues += _yearlyData[i].Speed.MetresRt.Mean; _staticCounter++;
                            }
                        }                        
                    }

                    // special provision if only one sample extends to the new year as the loop won't initialise
                    if (_yearlyData.Count == 1)
                    {
                        if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                        {
                            if (!double.IsNaN(_yearlyData[0].Speed.Metres10.Mean))
                            {
                                _staticValues += _yearlyData[0].Speed.Metres10.Mean; _staticCounter++;
                            }
                        }
                        else if (_meteoHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                        {
                            if (!double.IsNaN(_yearlyData[0].Speed.MetresRt.Mean))
                            {
                                _staticValues += _yearlyData[0].Speed.MetresRt.Mean; _staticCounter++;
                            }
                        }
                    }

                    // calculation for the yearly values
                    _value = Math.Round(_staticValues / _staticCounter, 2);                    
                    _valStr = Common.GetStringDecimals(_value, 2);
                }

                /// <summary>
                /// Calculate the overall yearly average data value.
                /// </summary>
                /// <param name="_yearlyData"></param>
                /// <param name="_ratedPower"></param>
                private void GetOverallAverage(List<ScadaData.ScadaSample> _yearlyData, double _ratedPower = 0)
                {
                    double _staticValues = 0;
                    int _staticCounter = 0;

                    for (int i = 1; i < _yearlyData.Count; i++)
                    {
                        // this here adds the first sample to the previous month whatever else happens
                        if (i == 1)
                        {
                            if (_ratedPower != 0)
                            {
                                if (!double.IsNaN(_yearlyData[0].Power.Mean))
                                {
                                    _staticValues += _yearlyData[0].Power.Mean; _staticCounter++;
                                }
                            }
                            else
                            { 
                                if (!double.IsNaN(_yearlyData[0].Anemo.ActWinds.Mean))
                                {
                                    _staticValues += _yearlyData[0].Anemo.ActWinds.Mean; _staticCounter++;
                                }
                            }
                        }
                        
                        // only increment value if it is not NaN
                        if (_ratedPower != 0)
                        {
                            if (!double.IsNaN(_yearlyData[i].Power.Mean))
                            {
                                // add values to the counter
                                _staticValues += _yearlyData[i].Power.Mean; _staticCounter++;
                            }
                        }
                        else
                        {
                            if (!double.IsNaN(_yearlyData[i].Anemo.ActWinds.Mean))
                            {
                                // add values to the counter
                                _staticValues += _yearlyData[i].Anemo.ActWinds.Mean; _staticCounter++;
                            }
                        }                        
                    }

                    // special provision if only one sample extends to the new year as the loop won't initialise
                    if (_yearlyData.Count == 1)
                    {
                        if (_ratedPower != 0)
                        {
                            if (!double.IsNaN(_yearlyData[0].Power.Mean))
                            {
                                // add values to the counter
                                _staticValues += _yearlyData[0].Power.Mean; _staticCounter++;
                            }
                        }
                        else
                        {
                            if (!double.IsNaN(_yearlyData[0].Anemo.ActWinds.Mean))
                            {
                                _staticValues += _yearlyData[0].Anemo.ActWinds.Mean; _staticCounter++;
                            }
                        }
                    }

                    // calculation for the yearly values
                    if (_ratedPower != 0)
                    {
                        _value = Math.Round(_staticValues / (_ratedPower * _staticCounter) * 100, 1);
                        _valStr = Common.GetStringDecimals(_value, 1);
                    }
                    else
                    {
                        _value = Math.Round(_staticValues / _staticCounter, 2);
                        _valStr = Common.GetStringDecimals(_value, 2);
                    }
                }

                /// <summary>
                /// Calculate the general string for the year.
                /// </summary>
                /// <param name="_yearlyData"></param>
                private void GetOverallDirectionString(List<ScadaData.ScadaSample> _yearlyData)
                {
                    _valStr = _yearlyData.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                }

                /// <summary>
                /// Calculate the general string for the year.
                /// </summary>
                /// <param name="_yearlyData"></param>
                private void GetOverallDirectionString(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header)
                {
                    // calculation of the yearly values - looking for the mode
                    if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { _valStr = "Unknown"; }
                    else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                    {
                        _valStr = _yearlyData.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                    else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                    {
                        _valStr = _yearlyData.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                    }
                }

                public DataTable GetMonthlyDirectionData(List<ScadaData.ScadaSample> _yearlyData)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();
                    
                    // monthly data collectopn
                    List<ScadaData.ScadaSample> _month = new List<ScadaData.ScadaSample>();

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.Month <= _incrementor && i != _yearlyData.Count - 1)
                        {
                            _month.Add(_yearlyData[i]);
                        }
                        else
                        {
                            if (i == _yearlyData.Count - 1)
                            {
                                _month.Add(_yearlyData[i]);
                            }

                            // if we are here, there must be no more data for the month
                            _table.Columns.Add("Month " + _incrementor.ToString(), typeof(string));

                            string mode = "";
                            if (_month.Count > 0)
                            {
                                mode = _month.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }
                            else { mode = "-"; }

                            newRow["Month " + _incrementor.ToString()] = mode;
                            
                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    // check if all months have an entry here
                    int _cols = _table.Columns.Count;
                    while (_cols != 12)
                    {
                        int _monthName = _cols + 1;
                        _table.Columns.Add("Month " + _monthName.ToString(), typeof(string));
                        newRow["Month " + _monthName.ToString()] = "-";
                        _cols++;
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetMonthlyDirectionData(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // monthly data collectopn
                    List<MeteoData.MeteoSample> _month = new List<MeteoData.MeteoSample>();

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.Month <= _incrementor && i != _yearlyData.Count - 1)
                        {
                            _month.Add(_yearlyData[i]);
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                _month.Add(_yearlyData[i]);
                            }

                            // if we are here, there must be no more data for the month
                            _table.Columns.Add("Month " + _incrementor.ToString(), typeof(string));
                            
                            // calculation of the values - looking for the mode
                            string mode = "";
                            if (_month.Count > 0)
                            {
                                if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { mode = "-"; }
                                else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    mode = _month.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                                }
                                else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    mode = _month.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                                }
                            }
                            else { mode = "-"; }

                            newRow["Month " + _incrementor.ToString()] = mode;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    // check if all months have an entry here
                    int _cols = _table.Columns.Count;
                    while (_cols != 12)
                    {
                        int _monthName = _cols + 1;
                        _table.Columns.Add("Month " + _monthName.ToString(), typeof(string));
                        newRow["Month " + _monthName.ToString()] = "-";
                        _cols++;
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetMonthlyPowerData(List<ScadaData.ScadaSample> _yearlyData, double _ratedPower)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // weekly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.Month <= _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (!double.IsNaN(_yearlyData[i].Power.Mean))
                            {
                                _valueCounter += _yearlyData[i].Power.Mean; _valueIncrement++;
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (!double.IsNaN(_yearlyData[i].Power.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Power.Mean; _valueIncrement++;
                                }
                            }

                            // if we are here, there must be no more data for the first week
                            _table.Columns.Add("Month " + _incrementor.ToString(), typeof(string));
                            double _add = _valueCounter != 0 ? 
                                Math.Round(_valueCounter / (_valueIncrement * _ratedPower) * 100, 1) : double.NaN;
                            newRow["Month " + _incrementor.ToString()] = Common.GetStringDecimals(_add, 1);

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    // check if all months have an entry here
                    int _cols = _table.Columns.Count;
                    while (_cols != 12)
                    {
                        int _monthName = _cols + 1;
                        _table.Columns.Add("Month " + _monthName.ToString(), typeof(double));
                        newRow["Month " + _monthName.ToString()] = double.NaN;
                        _cols++;
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetMonthlyWindSpeedData(List<ScadaData.ScadaSample> _yearlyData)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // monthly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.Month <= _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (!double.IsNaN(_yearlyData[i].Anemo.ActWinds.Mean))
                            {
                                _valueCounter += _yearlyData[i].Anemo.ActWinds.Mean; _valueIncrement++;
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (!double.IsNaN(_yearlyData[i].Anemo.ActWinds.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Anemo.ActWinds.Mean; _valueIncrement++;
                                }
                            }

                            // if we are here, there must be no more data for the month
                            _table.Columns.Add("Month " + _incrementor.ToString(), typeof(string));
                            double _add = _valueCounter != 0 ? Math.Round(_valueCounter / _valueIncrement, 2) : double.NaN;
                            newRow["Month " + _incrementor.ToString()] = Common.GetStringDecimals(_add,2);

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    // check if all months have an entry here
                    int _cols = _table.Columns.Count;
                    while (_cols != 12)
                    {
                        int _monthName = _cols + 1;
                        _table.Columns.Add("Month " + _monthName.ToString(), typeof(double));
                        newRow["Month " + _monthName.ToString()] = double.NaN;
                        _cols++;
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetMonthlyWindSpeedData(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // monthly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.Month <= _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                            else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                if (!double.IsNaN(_yearlyData[i].Speed.Metres10.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Speed.Metres10.Mean; _valueIncrement++;
                                }
                            }
                            else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                            {
                                if (!double.IsNaN(_yearlyData[i].Speed.MetresRt.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Speed.MetresRt.Mean; _valueIncrement++;
                                }
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                                else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    if (!double.IsNaN(_yearlyData[i].Speed.Metres10.Mean))
                                    {
                                        _valueCounter += _yearlyData[i].Speed.Metres10.Mean; _valueIncrement++;
                                    }
                                }
                                else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    if (!double.IsNaN(_yearlyData[i].Speed.MetresRt.Mean))
                                    {
                                        _valueCounter += _yearlyData[i].Speed.MetresRt.Mean; _valueIncrement++;
                                    }
                                }
                            }

                            // if we are here, there must be no more data for the month
                            _table.Columns.Add("Month " + _incrementor.ToString(), typeof(string));
                            double _add = _valueCounter != 0 ? Math.Round(_valueCounter / _valueIncrement, 2) : double.NaN;
                            newRow["Month " + _incrementor.ToString()] = Common.GetStringDecimals(_add, 2);

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    // check if all months have an entry here
                    int _cols = _table.Columns.Count;
                    while (_cols != 12)
                    {
                        int _monthName = _cols + 1;
                        _table.Columns.Add("Month " + _monthName.ToString(), typeof(double));
                        newRow["Month " + _monthName.ToString()] = double.NaN;
                        _cols++;
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetWeeklyDirectionData(List<ScadaData.ScadaSample> _yearlyData)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();
                    
                    // weekly data collectopn
                    List<ScadaData.ScadaSample> _week = new List<ScadaData.ScadaSample>();

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.DayOfYear <= 7 * _incrementor && i != _yearlyData.Count - 1)
                        {
                            _week.Add(_yearlyData[i]);
                        }
                        else
                        {
                            if (i == _yearlyData.Count - 1)
                            {
                                _week.Add(_yearlyData[i]);
                            }
                            
                            // if we are here, there must be no more data for the week
                            _table.Columns.Add("W" + _incrementor.ToString(), typeof(string));

                            string mode = "";
                            if (_week.Count > 0)
                            {
                                mode = _week.GroupBy(v => v.YawSys.YawPos.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                            }
                            else { mode = "-"; }

                            newRow["W" + _incrementor.ToString()] = mode;
                            
                            // finally take the incrementor to the next set of seven
                            _incrementor++;
                            _week.Clear();

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetWeeklyDirectionData(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // weekly data collectopn
                    List<MeteoData.MeteoSample> _week = new List<MeteoData.MeteoSample>();

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.DayOfYear <= 7 * _incrementor && i != _yearlyData.Count - 1)
                        {
                            _week.Add(_yearlyData[i]);
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                _week.Add(_yearlyData[i]);
                            }

                            // if we are here, there must be no more data for the week
                            _table.Columns.Add("W" + _incrementor.ToString(), typeof(string));

                            string mode = "";
                            // calculation of the values - looking for the mode
                            if (_week.Count > 0)
                            {
                                if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { mode = "-"; }
                                else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    mode = _week.GroupBy(v => v.Dircs.Metres10.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                                }
                                else if (_header.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    mode = _week.GroupBy(v => v.Dircs.MetresRt.DStrShort).OrderByDescending(g => g.Count()).First().Key;
                                }
                                else { mode = "-"; }
                            }

                            newRow["W" + _incrementor.ToString()] = mode;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;
                            _week.Clear();

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetWeeklyPowerData(List<ScadaData.ScadaSample> _yearlyData, double _ratedPower)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // weekly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.DayOfYear <= 7 * _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (!double.IsNaN(_yearlyData[i].Power.Mean))
                            {
                                _valueCounter += _yearlyData[i].Power.Mean; _valueIncrement++;
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (!double.IsNaN(_yearlyData[i].Power.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Power.Mean; _valueIncrement++;
                                }
                            }

                            // if we are here, there must be no more data for the first week
                            _table.Columns.Add("W" + _incrementor.ToString(), typeof(double));
                            double _add = _valueCounter != 0 ? Math.Round(_valueCounter / (_valueIncrement * _ratedPower) * 100, 1) : double.NaN;
                            newRow["W" + _incrementor.ToString()] = _add;

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetWeeklyWindSpeedData(List<ScadaData.ScadaSample> _yearlyData)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // weekly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.DayOfYear <= 7 * _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (!double.IsNaN(_yearlyData[i].Anemo.ActWinds.Mean))
                            {
                                _valueCounter += _yearlyData[i].Anemo.ActWinds.Mean; _valueIncrement++;
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (!double.IsNaN(_yearlyData[i].Anemo.ActWinds.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Anemo.ActWinds.Mean; _valueIncrement++;
                                }
                            }

                            // if we are here, there must be no more data for the week
                            _table.Columns.Add("W" + _incrementor.ToString(), typeof(double));
                            double _add = _valueCounter != 0 ? Math.Round(_valueCounter / _valueIncrement, 2) : double.NaN;
                            newRow["W" + _incrementor.ToString()] = _add;

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                public DataTable GetWeeklyWindSpeedData(List<MeteoData.MeteoSample> _yearlyData, MeteoData.MeteoHeader _header)
                {
                    // initialise an empty table
                    DataTable _table = new DataTable();

                    // incrementor will assign to the right table column
                    int _incrementor = 1;

                    // add new row which will be used for all of the data
                    DataRow newRow = _table.NewRow();

                    // weekly counters
                    double _valueCounter = 0;
                    int _valueIncrement = 0;

                    for (int i = 0; i < _yearlyData.Count; i++)
                    {
                        if (_yearlyData[i].TimeStamp.DayOfYear <= 7 * _incrementor && i != _yearlyData.Count - 1)
                        {
                            if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                            else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                if (!double.IsNaN(_yearlyData[i].Speed.Metres10.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Speed.Metres10.Mean; _valueIncrement++;
                                }
                            }
                            else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                            {
                                if (!double.IsNaN(_yearlyData[i].Speed.MetresRt.Mean))
                                {
                                    _valueCounter += _yearlyData[i].Speed.MetresRt.Mean; _valueIncrement++;
                                }
                            }
                        }
                        else
                        {
                            // special condition above means that last sample still needs to be incremented but then
                            // also the following things below need to be used
                            if (i == _yearlyData.Count - 1)
                            {
                                if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN) { }
                                else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    if (!double.IsNaN(_yearlyData[i].Speed.Metres10.Mean))
                                    {
                                        _valueCounter += _yearlyData[i].Speed.Metres10.Mean; _valueIncrement++;
                                    }
                                }
                                else if (_header.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    if (!double.IsNaN(_yearlyData[i].Speed.MetresRt.Mean))
                                    {
                                        _valueCounter += _yearlyData[i].Speed.MetresRt.Mean; _valueIncrement++;
                                    }
                                }
                            }

                            // if we are here, there must be no more data for the week
                            _table.Columns.Add("W" + _incrementor.ToString(), typeof(double));
                            double _add = _valueCounter != 0 ? Math.Round(_valueCounter / _valueIncrement, 2) : double.NaN;
                            newRow["W" + _incrementor.ToString()] = _add;

                            // add in previous values we'd otherwise miss, after nulling counters
                            _valueCounter = 0; _valueIncrement = 0;

                            // finally take the incrementor to the next set of seven
                            _incrementor++;

                            if (i != _yearlyData.Count - 1) { i--; }
                        }
                    }

                    _table.Rows.Add(newRow);
                    return _table;
                }

                #endregion

                #region Properties

                public int YearName { get { return _yearName; } set { _yearName = value; } }
                public double Value { get { return _value; } set { _value = value; } }

                public string ValStr { get { return _valStr; } set { _valStr = value; } }

                public DataTable MonthlyData { get { return _monthlyData; } set { _monthlyData = value; } }
                public DataTable WeeklyData { get { return _weeklyData; } set { _weeklyData = value; } }

                #endregion
            }

            public enum Mode
            {
                BEARINGS,
                CAPACITY,
                WINDINFO
            }

            #endregion

            #region Properties

            public double FullValue { get { return _totalVal; } set { _totalVal = value; } }

            public string FullStr { get { return _totalValStr; } set { _totalValStr = value; } }
            public List<Year> Years { get { return _yearList; } set { _yearList = value; } }

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
        public MetaDataSetup WindInfo { get { return _windInfo; } set { _windInfo = value; } }
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
