using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using scada_analyst.Shared;

namespace scada_analyst
{
    class Analysis
    {
        #region Variables

        private List<Distance> _distances = new List<Distance>();

        #endregion

        public Analysis() { }

        private void GetDistancesToEachAsset(List<Structure> theseAssets)
        {
            _distances = new List<Distance>();

            // this method will take the loaded assets and create a resultant object for distances
            // from each one to each other one

            // for each asset there will be n-1 distances to other assets, hence a list of results
            // the object must hold both the originating asset and all the ones originating from it

            for (int i = 0; i < theseAssets.Count; i++)
            {
                // for every asset we have, the same thing needs to happen but we also need to ignore this asset
                for (int j = 0; j < theseAssets.Count; j++)
                {
                    if (i != j)
                    {
                        // then get geographic distance using the suitable formula
                        double thisDistance = GetGeographicDistance(theseAssets[i].Position, theseAssets[j].Position);

                        // add this to the list of distances
                        _distances.Add(new Distance(theseAssets[i].UnitID, theseAssets[j].UnitID, thisDistance));
                    }
                }                
            }
        }

        public void GetDistances(List<Structure> theseAssets)
        {
            GetDistancesToEachAsset(theseAssets);
        }

        private double GetGeographicDistance(GridPosition position1, GridPosition position2)
        {
            double latMid, m_per_deg_lat, m_per_deg_lon, deltaLat, deltaLon;

            latMid = (position1.Latitude + position2.Latitude) / 2.0;

            m_per_deg_lat = 111132.954 - 559.822 * Math.Cos(2.0 * latMid) + 1.175 * Math.Cos(4.0 * latMid);
            m_per_deg_lon = (Math.PI / 180) * 6367449 * Math.Cos(latMid);

            deltaLat = Math.Abs(position1.Latitude - position2.Latitude);
            deltaLon = Math.Abs(position1.Longitude - position2.Longitude);

            return Math.Round(Math.Sqrt(Math.Pow(deltaLat * m_per_deg_lat, 2) + Math.Pow(deltaLon * m_per_deg_lon, 2)), 3);
        }

        #region Support Classes

        public class Distance : ObservableObject
        {
            #region Variables

            private int _from;
            private int _to;

            private double _distance;

            #endregion

            public Distance() { }

            public Distance(int from, int to, double distance)
            {
                _from = from;
                _to = to;
                _distance = distance;
            }

            #region Properties

            public int From { get { return _from; } set { _from = value; } }
            public int To { get { return _to; } set { _to = value; } }

            public double Distances { get { return _distance; } set { _distance = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public List<Distance> Distances { get { return _distances; } set { _distances = value; } }

        #endregion
    }
}
