using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Shared
{
    public class Structure : BaseStructure
    {
        #region Variables

        private DateTime startTime;
        private DateTime endTime;

        #endregion

        public Structure() { }

        private Structure(MeteoData.MetMastData input)
        {
            Position = input.Position;
            PositionsLoaded = input.PositionsLoaded;
            UnitID = input.UnitID;
            Type = input.Type;

            CheckDataSeriesTimes(input);
        }

        private Structure(ScadaData.TurbineData input)
        {
            Position = input.Position;
            PositionsLoaded = input.PositionsLoaded;
            UnitID = input.UnitID;
            Type = input.Type;

            CheckDataSeriesTimes(input);
        }

        public void CheckDataSeriesTimes(MeteoData.MetMastData input)
        {
            startTime = input.MetDataSorted[0].TimeStamp;
            endTime = input.MetDataSorted[input.MetDataSorted.Count - 1].TimeStamp;
        }

        public void CheckDataSeriesTimes(ScadaData.TurbineData input)
        {
            startTime = input.DataSorted[0].TimeStamp;
            endTime = input.DataSorted[input.DataSorted.Count - 1].TimeStamp;
        }

        private DateTime GetFirstOrLast(List<DateTime> times, bool getFirst)
        {
            DateTime result;

            List<DateTime> sortedTimes = times.OrderBy(s => s).ToList();

            if (getFirst)
            {
                result = sortedTimes[0];
            }
            else
            {
                result = sortedTimes[sortedTimes.Count - 1];
            }

            return result;
        }
        
        public static explicit operator Structure(MeteoData.MetMastData metMast)
        {
            return new Structure(metMast);
        }

        public static explicit operator Structure(ScadaData.TurbineData turbine)
        {
            return new Structure(turbine);
        }

        #region Properties

        public DateTime StartTime { get { return startTime; } set { startTime = value; } }
        public DateTime EndTime { get { return endTime; } set { endTime = value; } }

        #endregion
    }

    public class BaseMetaData
    {
        #region Variables

        private string fileName = "";

        #endregion
        
        #region Properties

        public string FileName { get { return fileName; } set { fileName = value; } }

        #endregion
    }

    public class BaseEventData
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

    public class BaseStructure : ObservableObject
    {
        #region Variables

        private bool positionsLoaded = false;
        private int unitID = -1;

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

        public string PositionsLoadedDisplay { get { return PositionsLoaded == true ? "Yes" : "No"; } set { PositionsLoadedDisplay = value; } }
        public int UnitID { get { return unitID; } set { unitID = value; } }

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
        public TimeSpan DeltaTime { get { return deltaTime; } set { deltaTime = value; } }

        #endregion
    }

    public class GridPosition
    {
        #region Variables

        private double easting = 0, northing = 0;
        private double latitude = 0, longitude = 0;

        private Type fileType;

        #endregion

        public GridPosition() { }

        public GridPosition(double easting, double northing)
        {
            this.easting = easting;
            this.northing = northing;

            fileType = Type.GRID;
        }

        public GridPosition(GridPosition source)
        {
            easting = source.easting;
            northing = source.northing;

            fileType = Type.GRID;
        }

        public GridPosition(double latitude, double longitude, Type inputType)
        {
            if (inputType == Type.GEOG)
            {
                this.latitude = latitude;
                this.longitude = longitude;
                fileType = Type.GEOG;
            }
        }

        public GridPosition(GridPosition source, Type type)
        {
            if (type == Type.GEOG)
            {
                latitude = source.latitude;
                longitude = source.longitude;
                fileType = Type.GEOG;
            }
        }

        public enum Type
        {
            GRID,
            GEOG,
            BOTH
        }

        #region Properties

        public double Easting { get { return easting; } set { value = easting; } }
        public double Northing { get { return northing; } set { value = northing; } }

        public double Latitude { get { return latitude; } set { value = latitude; } }
        public double Longitude { get { return longitude; } set { value = longitude; } }

        public Type FileType { get { return fileType; } set { fileType = value; } }

        #endregion
    }

    public class Stats
    {
        #region Variables

        protected double minm = double.NaN;
        protected double maxm = double.NaN;
        protected double mean = double.NaN;
        protected double stdv = double.NaN;
        
        #endregion

        public Stats() { }

        #region Properties

        public double Minm { get { return minm; } set { minm = value; } }
        public double Maxm { get { return maxm; } set { maxm = value; } }
        public double Mean { get { return mean; } set { mean = value; } }
        public double Stdv { get { return stdv; } set { stdv = value; } }
        
        #endregion
    }
}
