using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Shared
{
    public class Event : BaseEvent
    {
        #region Variables

        private double extrmSpd = 0;
        private double minmmPow = 0;
        private string displayDayTime = "";

        private TimeSpan sampleLen = new TimeSpan(0, 9, 59);

        private EventAssoct assocEv = EventAssoct.NONE;
        private EventSource eSource;
        private NoPowerTime noPowTm;
        private TimeOfEvent dayTime = TimeOfEvent.UNKNOWN;
        private WeatherType weather;

        #endregion

        public Event() { }

        public Event(List<MeteoData.MeteoSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            Durat = Finit - Start;

            eSource = EventSource.METMAST;
            Type = Types.WEATHER;
            weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LOW_SP)
                {
                    if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean < extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean > extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }
        }

        public Event(List<ScadaData.ScadaSample> data)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            Durat = Finit - Start;

            eSource = EventSource.TURBINE;
            Type = Types.NOPOWER;

            for (int i = 0; i < data.Count; i++)
            {
                if (i == 0) { minmmPow = data[i].Powers.Mean; }

                if (data[i].Powers.Mean < minmmPow) { minmmPow = data[i].Powers.Mean; }

                EvTimes.Add(data[i].TimeStamp);
            }

            if (Durat.TotalMinutes < 60) { noPowTm = NoPowerTime.DMNS; }
            else if (Durat.TotalMinutes < 60 * 5) { noPowTm = NoPowerTime.HORS; }
            else if (Durat.TotalMinutes < 60 * 10) { noPowTm = NoPowerTime.DHRS; }
            else if (Durat.TotalMinutes < 60 * 24) { noPowTm = NoPowerTime.DAYS; }
        }

        public Event(List<ScadaData.ScadaSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            Durat = Finit - Start;

            eSource = EventSource.TURBINE;
            Type = Types.WEATHER;
            weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LOW_SP)
                {
                    if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean < extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean > extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }
        }

        public enum EventAssoct
        {
            // an enum to list whether the power event is associated with a wind 
            // speed event or not -- other for not

            NONE,
            LO_SP,
            HI_SP,
            OTHER
        }

        public enum EventSource
        {
            // this enum is for marking the source which created this event

            UNKNOWN,
            METMAST,
            TURBINE
        }

        public enum NoPowerTime
        {
            // this event marks the duration of the downtime for the event

            NONE,
            DMNS, // deciminutes
            HORS, // hours
            DHRS, // decihours
            DAYS // days
        }

        public enum TimeOfEvent
        {
            // an enum to list what time of day the event took place at

            UNKNOWN,
            AS_DAWN, // 18 deg to 12 deg sun below horizon
            NA_DAWN, // 12 deg to 6 deg sun below horizon
            CI_DAWN, // 6 deg below to sun at horizon 
            DAYTIME,
            CI_DUSK, // sun from horizon to 6 deg below
            NA_DUSK, // sun from 6 deg below to 12 deg below horizon
            AS_DUSK, // sun from 12 deg below to 18 deg below horizon
            NIGHTTM
        }

        public enum WeatherType
        {
            NORMAL,
            LOW_SP, // below cutin
            HI_SPD  // above cutout
        }

        #region Properties

        public double ExtrmSpd { get { return extrmSpd; } set { extrmSpd = value; } }
        public double MinmmPow { get { return minmmPow; } set { minmmPow = value; } }

        public string DisplayDayTime
        {
            get
            {
                if (DayTime == TimeOfEvent.NIGHTTM) { return "Night"; }
                else if (DayTime == TimeOfEvent.AS_DAWN) { return "Astronomical dawn"; }
                else if (DayTime == TimeOfEvent.NA_DAWN) { return "Nautical dawn"; }
                else if (DayTime == TimeOfEvent.CI_DAWN) { return "Civic dawn"; }
                else if (DayTime == TimeOfEvent.DAYTIME) { return "Day"; }
                else if (DayTime == TimeOfEvent.CI_DUSK) { return "Civic dusk"; }
                else if (DayTime == TimeOfEvent.NA_DUSK) { return "Nautical dusk"; }
                else if (DayTime == TimeOfEvent.AS_DUSK) { return "Astronomical dusk"; }
                else { return "Unknown"; }
            }
            set { displayDayTime = value; }
        }

        public EventAssoct AssocEv { get { return assocEv; } set { assocEv = value; } }
        public EventSource ESource { get { return eSource; } set { eSource = value; } }
        public NoPowerTime NoPowTm { get { return noPowTm; } set { noPowTm = value; } }
        public TimeOfEvent DayTime { get { return dayTime; } set { dayTime = value; } }
        public WeatherType Weather { get { return weather; } set { weather = value; } }

        #endregion
    }

    public class Structure : BaseStructure
    {
        #region Variables

        private DateTime startTime;
        private DateTime endTime;

        #endregion

        public Structure() { }

        private Structure(MeteoData.MetMastData metMast)
        {
            Position = metMast.Position;
            UnitID = metMast.UnitID;
            Type = metMast.Type;

            startTime = GetFirstOrLast(metMast.InclDtTm, true);
            endTime = GetFirstOrLast(metMast.InclDtTm, false);
        }

        private Structure(ScadaData.TurbineData turbine)
        {
            Position = turbine.Position;
            UnitID = turbine.UnitID;
            Type = turbine.Type;

            startTime = GetFirstOrLast(turbine.InclDtTm, true);
            endTime = GetFirstOrLast(turbine.InclDtTm, false);
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

    public class BaseEvent
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

    public class BaseStructure
    {
        #region Variables

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
        private TimeSpan deltaTime;
        
        #endregion
        
        #region Properties
        
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
