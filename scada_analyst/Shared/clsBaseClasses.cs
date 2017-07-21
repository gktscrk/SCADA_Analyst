using System;
using System.Collections.Generic;

namespace scada_analyst.Shared
{
    public class BaseEventData : ObservableObject
    {
        #region Variables

        private int fromAsset;

        private DateTime start;
        private DateTime finit;
        private TimeSpan durat;

        private Types type;

        private List<DateTime> evTimes = new List<DateTime>();

        #endregion
        
        public enum Types
        {
            UNKNOWN,
            NOPOWER,
            WEATHER
        }

        #region Properties

        public int FromAsset { get { return fromAsset; } set { fromAsset = value; } }

        public DateTime Start { get { return start; } set { start = value; } }
        public DateTime Finit { get { return finit; } set { finit = value; } }
        public TimeSpan Durat { get { return durat; } set { durat = value; } }

        public Types Type { get { return type; } set { type = value; } }

        public List<DateTime> EvTimes { get { return evTimes; } set { evTimes = value; } }

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

        private bool positionsLoaded = false;
        private int unitID = -1;
        private string _prevailingWindString = "";

        private List<DateTime> inclDtTm = new List<DateTime>();

        private GridPosition position;
        private Types type = Types.UNKNOWN;

        #endregion
        
        public enum Types
        {
            UNKNOWN,
            TURBINE,
            METMAST
        }

        #region Properties

        public bool PositionsLoaded { get { return positionsLoaded; } set { positionsLoaded = value; } }
        public int UnitID { get { return unitID; } set { unitID = value; } }

        public string PositionsLoadedDisplay { get { return PositionsLoaded == true ? "Added" : "None"; } set { PositionsLoadedDisplay = value; } }
        public string TypeString {  get { return type == Types.METMAST ? "MetMast" : type == Types.TURBINE ? "Turbine" : "Unknown"; } set { TypeString = value; } }
        public string PrevailingWindString { get { return _prevailingWindString; } set { _prevailingWindString = value; } }

        public List<DateTime> InclDtTm { get { return inclDtTm; } set { inclDtTm = value; } }

        public GridPosition Position {  get { return position; } set { position = value; } }
        public Types Type { get { return type; } set { type = value; } }

        #endregion
    }
    
    public class BaseSampleData
    {
        #region Variables

        private double error = double.NaN;

        private int assetID = 0;
        private int sampleID = 0;
        private int stationID = 0;

        private DateTime timeStamp;
        private TimeSpan deltaTime;

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

        public double Error { get { return error; } set { error = value; } }

        public int AssetID { get { return assetID; } set { assetID = value; } }
        public int SampleID { get { return sampleID; } set { sampleID = value; } }
        public int StationID { get { return stationID; } set { stationID = value; } }

        public DateTime TimeStamp { get { return timeStamp; } set { timeStamp = value; } }
        public TimeSpan SampleSeparation { get { return deltaTime; } set { deltaTime = value; } }

        #endregion
    }

    public class Stats
    {
        #region Variables

        protected double minm = double.NaN;
        protected double maxm = double.NaN;
        protected double mean = double.NaN;
        protected double stdv = double.NaN;

        protected double dMean = double.NaN;

        #endregion

        public Stats() { }

        #region Properties

        public double Minm { get { return minm; } set { minm = value; } }
        public double Maxm { get { return maxm; } set { maxm = value; } }
        public double Mean { get { return mean; } set { mean = value; } }
        public double Stdv { get { return stdv; } set { stdv = value; } }

        public double Dlta { get { return dMean; } set { dMean = value; } }

        #endregion
    }

    public class WindSpeeds : Stats
    {
        #region Variables

        private double _direction = double.NaN;

        #endregion

        #region Properties

        public double Dirc { get { return _direction; } set { _direction = value; } }
        public string DStr { get { return Common.BearingStringConversion((float)_direction); } set { DStr = value; } }

        #endregion
    }
}
