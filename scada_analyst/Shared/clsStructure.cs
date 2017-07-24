using System;
using System.Collections.Generic;
using System.Linq;

namespace scada_analyst.Shared
{
    public class Structure : BaseStructure
    {
        #region Variables

        private DateTime startTime;
        private DateTime endTime;

        #endregion

        #region Constructor

        public Structure() { }

        private Structure(MeteoData.MetMastData input)
        {
            Position = input.Position;
            PositionsLoaded = input.PositionsLoaded;
            UnitID = input.UnitID;
            Type = input.Type;
            Bearings = input.Bearings;

            CheckDataSeriesTimes(input);
        }

        private Structure(ScadaData.TurbineData input)
        {
            Position = input.Position;
            PositionsLoaded = input.PositionsLoaded;
            UnitID = input.UnitID;
            Type = input.Type;
            Bearings = input.Bearings;
            Capacity = input.Capacity;

            CheckDataSeriesTimes(input);
        }

        #endregion

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

        #region Conversions

        public static explicit operator Structure(MeteoData.MetMastData metMast)
        {
            return new Structure(metMast);
        }

        public static explicit operator Structure(ScadaData.TurbineData turbine)
        {
            return new Structure(turbine);
        }

        #endregion

        #region Properties

        public DateTime StartTime { get { return startTime; } set { startTime = value; } }
        public DateTime EndTime { get { return endTime; } set { endTime = value; } }

        #endregion
    }
}
