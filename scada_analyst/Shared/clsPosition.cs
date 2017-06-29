namespace scada_analyst.Shared
{
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

}
