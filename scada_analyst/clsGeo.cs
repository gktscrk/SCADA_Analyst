using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using scada_analyst.Shared;

namespace scada_analyst
{
    public class GeoData : BaseMetaData
    {
        #region Variables

        private string[] geoNames =
            { "latitude" , "longitude" , "lat" , "lon" , "easting" , "east" , "northing" , "north" };

        private List<GeoSample> geoInfo = new List<GeoSample>();

        #endregion

        public GeoData(string filename, IProgress<int> progress)
        {
            this.FileName = filename;

            LoadGeography(progress);
        }

        private void LoadGeography(IProgress<int> progress)
        {
            using (StreamReader sR = new StreamReader(FileName))
            {
                try
                {
                    int count = 0;
                    bool readHeader = false;

                    geoInfo = new List<GeoSample>();

                    while (!sR.EndOfStream)
                    {
                        if (readHeader == false)
                        {
                            string header = sR.ReadLine();
                            header = header.ToLower().Replace("\"", String.Empty);
                            readHeader = true;

                            if (!Common.ContainsAny(header, geoNames)) { throw new WrongFileTypeException(); }
                        }

                        string line = sR.ReadLine();

                        if (!line.Equals(""))
                        {
                            line = line.Replace("\"", String.Empty);

                            string[] splits = Common.GetSplits(line, ',');

                            geoInfo.Add(new GeoSample(splits));
                        }

                        count++;

                        if (count % 1 == 0)
                        {
                            if (progress != null)
                            {
                                progress.Report((int)
                                    ((double)sR.BaseStream.Position * 100 / sR.BaseStream.Length));
                            }
                        }
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    sR.Close();
                }
            }
        }

        #region Support Classes

        public class GeoSample : BaseSampleData
        {
            #region Variables

            private double height;

            private GeoSampleType geoType;
            private GridPosition position;

            #endregion

            public GeoSample() { }

            public GeoSample(string[] data)
            {
                //"TurbineUID","Latitude","Longitude","Height","Type"

                AssetID = Common.CanConvert<int>(data[0]) ? Convert.ToInt32(data[0]) : 0;

                if (Common.CanConvert<double>(data[1]) && Common.CanConvert<double>(data[2]))
                {
                    Position = new GridPosition(Convert.ToDouble(data[1]), 
                        Convert.ToDouble(data[2]), GridPosition.Type.GEOG);
                }

                height = Common.CanConvert<double>(data[3]) ? Convert.ToDouble(data[3]) : 0;

                if (data[4].ToLower() == "turbine")
                {
                    geoType = GeoSampleType.TURBINE;
                }
                else if (data[4].ToLower() == "metmast")
                {
                    geoType = GeoSampleType.METMAST;
                }
                else
                {
                    geoType = GeoSampleType.UNKNOWN;
                }                
            }

            #region Support Classes

            public enum GeoSampleType
            {
                UNKNOWN,
                TURBINE,
                METMAST
            }

            #endregion

            #region Properties

            public double Height { get { return height; } set { height = value; } }

            public GeoSampleType GeoType { get { return geoType; } set { geoType = value; } }
            public GridPosition Position { get { return position; } set { position = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public List<GeoSample> GeoInfo { get { return geoInfo; } }

        #endregion
    }
}
