using CommunityToolkit.Mvvm.Input;
using GeneralUpdate.Infrastructure.MVVM;
using GeneralUpdate.PacketTool.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.PacketTool.ViewModels
{
    public class DifferentViewModel : ViewModeBase
    {
        private ObservableCollection<MainModel> leftModels;
        private ObservableCollection<MainModel> rightModels;
        private ObservableCollection<MainModel> middleModels;
        private AsyncRelayCommand goBackCommand;

        public DifferentViewModel() 
        {
            MessagingCenter.Subscribe<MainPage, Tuple<List<MainModel>, List<MainModel>, List<MainModel>>>(this, MessageToken.FilesMessageToken, FilesCallback);
        }

        public AsyncRelayCommand GoBackCommand { get => goBackCommand ?? (goBackCommand = new AsyncRelayCommand(GobackCallback)); }
        public ObservableCollection<MainModel> LeftModels { get => leftModels; set => leftModels = value; }
        public ObservableCollection<MainModel> RightModels { get => rightModels; set => rightModels = value; }
        public ObservableCollection<MainModel> MiddleModels { get => middleModels; set => middleModels = value; }


        private async Task GobackCallback()
        {
            await Shell.Current.GoToAsync("..");
        }

        private void FilesCallback(MainPage arg1, Tuple<List<MainModel>, List<MainModel>, List<MainModel>> arg2)
        {
            LeftModels = new ObservableCollection<MainModel>(arg2.Item1);
            MiddleModels = new ObservableCollection<MainModel>(arg2.Item2);
            RightModels = new ObservableCollection<MainModel>(arg2.Item3);
        }

    }
}
