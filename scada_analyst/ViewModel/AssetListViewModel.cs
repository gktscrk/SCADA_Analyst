using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using scada_analyst.Model;

namespace scada_analyst
{
    public class AssetListViewModel : ObservableObject
    {
        #region Variables

        private AssetListModel _currentAsset;

        #endregion

        public AssetListViewModel() { }

        #region Properties

        public AssetListModel CurrentAsset
        {
            get { return _currentAsset; }
            set
            {
                if (value != _currentAsset)
                {
                    _currentAsset = value;
                    OnPropertyChanged("CurrentAsset");
                }
            }
        }

        #endregion
    }
}
