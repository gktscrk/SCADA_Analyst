using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Shared
{
    public class BaseMetaData
    {
        #region Variables

        private string fileName = "";

        #endregion

        public BaseMetaData() { }

        #region Properties

        public string FileName { get { return fileName; } set { fileName = value; } }

        #endregion
    }

    public class BaseStructure
    {
        #region Variables

        private int unitID = -1;

        private List<DateTime> inclDtTm = new List<DateTime>();

        private GridPosition position;
        private Types type = Types.UNKNOWN;

        #endregion

        public BaseStructure() { }

        public enum Types
        {
            UNKNOWN,
            TURBINE,
            METMAST
        }

        #region Properties

        public int UnitID { get { return unitID; } set { unitID = value; } }

        public List<DateTime> InclDtTm { get { return inclDtTm; } set { inclDtTm = value; } }

        public GridPosition Position {  get { return position; } set { position = value; } }
        public Types Type { get { return type; } set { type = value; } }

        #endregion
    }
    
    public class BaseSampleData
    {
        #region Variables

        private int assetID = 0;
        private int sampleID = 0;
        private int stationID = 0;

        private DateTime timeStamp;
        
        #endregion
        
        #region Properties
        
        public int AssetID { get { return assetID; } set { assetID = value; } }
        public int SampleID { get { return sampleID; } set { sampleID = value; } }
        public int StationID { get { return stationID; } set { stationID = value; } }
        public DateTime TimeStamp { get { return timeStamp; } set { timeStamp = value; } }

        #endregion
    }

    public class GridPosition
    {
        #region Variables

        private double easting = 0, northing = 0;
        private double latitude = 0, longitude = 0;

        #endregion

        public GridPosition() { }

        public GridPosition(double easting, double northing)
        {
            this.easting = easting;
            this.northing = northing;
        }

        public GridPosition(GridPosition source)
        {
            easting = source.easting;
            northing = source.northing;
        }

        public GridPosition(double latitude, double longitude, Type type)
        {
            if (type == Type.GEOG)
            {
                this.latitude = latitude;
                this.longitude = longitude;
            }
        }

        public GridPosition(GridPosition source, Type type)
        {
            if (type == Type.GEOG)
            {
                latitude = source.latitude;
                longitude = source.longitude;
            }
            else
            {
                easting = source.easting;
                northing = source.northing;
            }
        }

        public enum Type
        {
            GRID,
            GEOG
        }

        #region Properties

        public double Easting
        {
            get { return easting; }
            set { value = easting; }
        }

        public double Northing
        {
            get { return northing; }
            set { value = northing; }
        }

        public double Latitude
        {
            get { return latitude; }
            set { value = latitude; }
        }

        public double Longitude
        {
            get { return longitude; }
            set { value = longitude; }
        }

        #endregion
    }

    public class Stats
    {
        #region Variables

        protected double minm = -999999;
        protected double maxm = -999999;
        protected double mean = -999999;
        protected double stdv = -999999;

        // protected int minmCol = -1, maxmCol = -1, meanCol = -1, stdvCol = -1;

        #endregion

        public Stats() { }

        #region Properties

        public double Minm { get { return minm; } set { minm = value; } }
        public double Maxm { get { return maxm; } set { maxm = value; } }
        public double Mean { get { return mean; } set { mean = value; } }
        public double Stdv { get { return stdv; } set { stdv = value; } }

        //public int MinmCol { get { return minmCol; } set { minmCol = value; } }
        //public int MaxmCol { get { return maxmCol; } set { maxmCol = value; } }
        //public int MeanCol { get { return meanCol; } set { meanCol = value; } }
        //public int StdvCol { get { return stdvCol; } set { stdvCol = value; } }

        #endregion
    }
}
