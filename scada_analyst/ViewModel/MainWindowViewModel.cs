using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using scada_analyst.Controls;
using System.Collections.ObjectModel;

namespace scada_analyst
{
    public class MainWindowViewModel : ObservableObject
    {
        #region Variables

        private bool _isSelected;
        private static object _selectedItem = null;

        private ObservableCollection<DirectoryItem> _navContents = new ObservableCollection<DirectoryItem>();
        
        #endregion

        public MainWindowViewModel()
        {

        }

        private ObservableCollection<DirectoryItem> GetChangedValues()
        {
            ObservableCollection<DirectoryItem> list = new ObservableCollection<DirectoryItem>();
            
            list.Add(new DirectoryItem("Wind Speeds: Low", 0));
            list.Add(new DirectoryItem("Wind Speeds: High", 0));
            list.Add(new DirectoryItem("Power Prod: None", 0));
            list.Add(new DirectoryItem("Power Prod: High", 0));

            return list;
        }

        static void OnSelectedItemChanged()
        {
            // method for dealing with changes, raise event and do what must be done
        }

        #region Properties
      
        public ObservableCollection<DirectoryItem> NavContents { get { return GetChangedValues(); } set { _navContents = value; } }

        public static object SelectedItem
        {
            get { return _selectedItem; }
            private set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnSelectedItemChanged();
                }                
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;

                    OnPropertyChanged(nameof(IsSelected));
                    if (_isSelected)
                    {
                        SelectedItem = this;
                    }
                }
            }
        }

        #endregion
    }

    public class DirectoryItem : ObservableObject
    {
        #region Variables

        private int _intValue = 0;
        private string _strValue = "";

        private string _displayString;

        #endregion

        public DirectoryItem(string strValue)
        {
            this._strValue = strValue;

            _displayString = strValue;
        }

        public DirectoryItem(string strValue, int intValue)
        {
            this._strValue = strValue;
            this._intValue = intValue;

            _displayString = strValue + " (" + intValue + ")";
        }

        #region Properties

        public int IntegerData { get { return _intValue; } set { _intValue = value; } }
        public string StringData { get { return _strValue; } set { _strValue = value; } }

        public string DisplayString { get { return _displayString; } set { _displayString = value; } }

        #endregion
    }

}
