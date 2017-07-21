using System;
using System.Collections.Generic;

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

        private List<string> _fileName = new List<string>();

        #endregion
        
        #region Properties

        public List<string> FileName { get { return _fileName; } set { _fileName = value; } }

        #endregion
    }

    public class BaseStructure : ObservableObject
    {
        #region Variables

        private bool _positionsLoaded = false;
        private int _unitID = -1;
        private string _prevailingWindString = "";

        private List<DateTime> _inclSamples = new List<DateTime>();

        private GridPosition _position;
        private Types _type = Types.UNKNOWN;

        #endregion

        #region Support Classes

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
        public string PrevailingWindString { get { return _prevailingWindString; } set { _prevailingWindString = value; } }
        public string TypeString {  get { return _type == Types.METMAST ? "MetMast" : _type == Types.TURBINE ? "Turbine" : "Unknown"; } set { TypeString = value; } }
        
        public List<DateTime> InclSamples { get { return _inclSamples; } set { _inclSamples = value; } }

        public GridPosition Position {  get { return _position; } set { _position = value; } }
        public Types Type { get { return _type; } set { _type = value; } }

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
