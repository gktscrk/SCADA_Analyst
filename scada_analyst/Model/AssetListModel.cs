using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Model
{
    public class AssetListModel : ObservableObject
    {
        #region Variables

        private int _assetID;
        private string _assetType;
        private string _startTime;
        private string _endingTme;

        #endregion

        #region Properties

        public int AssetID
        {
            get { return _assetID; }
            set
            {
                if (value != _assetID)
                {
                    _assetID = value;
                    OnPropertyChanged("AssetID");
                }
            }
        }

        public string AssetType
        {
            get { return _assetType; }
            set
            {
                if (value != _assetType)
                {
                    _assetType = value;
                    OnPropertyChanged("AssetType");
                }
            }
        }

        public string StartTime
        {
            get { return _startTime; }
            set
            {
                if (value != _startTime)
                {
                    _startTime = value;
                    OnPropertyChanged("StartTime");
                }
            }
        }
        
        public string EndingTme
        {
            get { return _endingTme; }
            set
            {
                if (value != _endingTme)
                {
                    _endingTme = value;
                    OnPropertyChanged("EndingTme");
                }
            }
        }

        #endregion
    }
}
