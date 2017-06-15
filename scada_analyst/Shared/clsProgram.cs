using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Shared
{
    public static class VersionHistory
    {
        private static List<ProgramVersion> GetAllChanges()
        {
            List<ProgramVersion> results = new List<ProgramVersion>(25);

            #region Version 0.100

            ProgramVersion v0130 = new ProgramVersion(0130, new DateTime(2017, 06, 15));
            v0130.AddChange("Visualised list with information on basic loaded data.");
            results.Add(v0130);

            ProgramVersion v0123 = new ProgramVersion(0123, new DateTime(2017, 06, 15));
            v0123.AddChange("Export option - feeds back and forth the export options.");
            v0123.AddChange("Export option - fixed TimeStamp properties for the export and load.");
            results.Add(v0123);

            ProgramVersion v0122 = new ProgramVersion(0122, new DateTime(2017, 06, 15));
            v0122.AddChange("Export option - added gearbox options.");
            v0122.AddChange("Export option - save file with properties.");
            results.Add(v0122);

            ProgramVersion v0121 = new ProgramVersion(0121, new DateTime(2017, 06, 14));
            v0121.AddChange("Export option beginning - choose which variables are useful.");
            results.Add(v0121);

            ProgramVersion v0120 = new ProgramVersion(0120, new DateTime(2017, 06, 14));
            v0120.AddChange("Task.Run methodology now implements proper progress tracking.");
            results.Add(v0120);

            ProgramVersion v0101 = new ProgramVersion(0101, new DateTime(2017, 06, 07));
            v0101.AddChange("Improved loading methodology for meteorology files.");
            v0101.AddChange("Improving turbine data loading with regards to empty variables and unknown info.");
            results.Add(v0101);

            ProgramVersion v0100 = new ProgramVersion(0100, new DateTime(2017,06,07));
            v0100.AddChange("Improved loading methodology for SCADA files.");
            v0100.AddChange("More variables loaded from turbine and temperature files.");
            results.Add(v0100);

            ProgramVersion v0014 = new ProgramVersion(0014, new DateTime(2017, 06, 06));
            v0014.AddChange("Added version history.");
            results.Add(v0014);

            #endregion

            return results;
        }

        public static List<ProgramVersion> GetChanges(int sinceVersion = -1)
        {
            return GetAllChanges().FindAll(v => v.Version > sinceVersion);
        }

        #region Support classes

        public class ProgramVersion : IComparable
        {
            #region Variables

            private DateTime releaseDate;
            private int version;
            private List<string> changes = new List<string>();

            #endregion

            public ProgramVersion(int version, DateTime releaseDate)
            {
                this.version = version;
                this.releaseDate = releaseDate;
            }

            public void AddChange(string change)
            {
                changes.Add(string.Format("- {0}", change));
            }

            public int CompareTo(object obj)
            {
                if (obj is ProgramVersion)
                {
                    return version.CompareTo(((ProgramVersion)obj).version);
                }

                return 0;
            }

            private string GetVersionString()
            {
                string versionString = version.ToString();
                string result = "";

                for (int i = 0; i < versionString.Length - 1; i++)
                {
                    result = result + versionString[i] + ".";
                }

                return result + versionString[versionString.Length - 1];
            }

            #region Accessor methods

            public string Changes
            {
                get
                {
                    StringBuilder sB = new StringBuilder();

                    for (int i = 0; i < changes.Count; i++)
                    {
                        if (i < changes.Count - 1)
                        {
                            sB.AppendLine(changes[i]);
                        }
                        else
                        {
                            sB.Append(changes[i]);
                        }
                    }

                    return sB.ToString();
                }
            }

            public int Version
            {
                get { return version; }
            }

            public string VersionAndDate
            {
                get
                {
                    return string.Format("{0} ({1})", GetVersionString(), releaseDate.ToShortDateString());
                }
            }

            #endregion
        }

        #endregion
    }
}
