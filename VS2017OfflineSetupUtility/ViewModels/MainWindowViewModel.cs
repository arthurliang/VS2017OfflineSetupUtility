/*
    Copyright © 2017 Deepak Rathi 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace VS2017OfflineSetupUtility.ViewModels
{
    class MainWindowViewModel:BindableBase
    {

        #region DirectoryNames
        /// <summary>
        /// Contain all directory names for selected folder. Left for future purpose; binding to UI list
        /// </summary>
        private ObservableCollection<VsModule> _moduleCollection = new ObservableCollection<VsModule>();

        public ObservableCollection<VsModule> ModuleCollection
        {
            get { return _moduleCollection; }
            set { SetProperty(ref _moduleCollection, value); }
        }
        #endregion

        #region OldVersionModule
        private ObservableCollection<VsModule> _oldVersionModule = new ObservableCollection<VsModule>();
        /// <summary>
        /// Contain all directory names for selected folder
        /// </summary>
        public ObservableCollection<VsModule> OldVersionModule
        {
            get { return _oldVersionModule; }
            set
            {
                if (SetProperty(ref _oldVersionModule, value))
                    DeleteOldVersionCommand.RaiseCanExecuteChanged();
            }
        }
        #endregion

        #region SelectedFolderPath
        private string _selectedFolderPath = default(string);
        /// <summary>
        /// Contain SelectedFolderPath string
        /// </summary>
        public string SelectedFolderPath
        {
            get { return _selectedFolderPath; }
            set {
                if (SetProperty(ref _selectedFolderPath, value))
                {
                    DeleteOldVersionCommand.RaiseCanExecuteChanged();
                }
            }
        }
        #endregion

        #region SelectFolderCommand
        private DelegateCommand _selectFolderCommand;

        public DelegateCommand SelectFolderCommand
        {
            get
            {
                return _selectFolderCommand ?? (_selectFolderCommand = new DelegateCommand(() =>
                {
                    try
                    {
                        if (!SelectVs2017OfflineSetupRootFolder()) return;

                        Classification();

                        //Select all the Modules with same name from ModuleCollection
                        var duplicateModules =
                            ModuleCollection.Where(module =>
                                ModuleCollection
                                    .Except(new ObservableCollection<VsModule> { module })
                                    .Any(x => x.Name == module.Name)
                            ).ToObservableCollection();

                        //Get all the old version modules/folder from duplicateModules
                        OldVersionModule =
                            duplicateModules.Where(module =>
                                duplicateModules
                                    .Except(new ObservableCollection<VsModule> { module })
                                    .Any(x => x.Name == module.Name && x.VersionObject.CompareTo(module.VersionObject) > 0)
                            ).ToObservableCollection();

                        if (!OldVersionModule.Any())
                            MessageBox.Show("Old version folder does not exist.");
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine(exception.Message);
                    }
                }));
            }
        }

        private void Classification()
        {
            var dirInfo = new DirectoryInfo(SelectedFolderPath);
            var subDirectories = dirInfo.GetDirectories();

            foreach (var subDirectory in subDirectories)
            {
                ClassifySubDirectory(subDirectory);
            }
        }

        private void ClassifySubDirectory(DirectoryInfo subDirectory)
        {
            var vsModule = new VsModule();
            if (!subDirectory.Name.Contains(","))
            {
                return;
            }

            var stringSplit = subDirectory.Name.Split(',').ToList();
            vsModule.Name = stringSplit.FirstOrDefault();
            vsModule.Version = stringSplit[1];
            stringSplit.Remove(vsModule.Name);
            stringSplit.Remove(vsModule.Version);
            if (stringSplit.Count() > 0)
            {
                foreach (var item in stringSplit)
                    vsModule.Name = vsModule.Name + "," + item;
            }

            vsModule.FullPath = subDirectory.FullName;
            ModuleCollection.Add(vsModule);
        }

        private bool SelectVs2017OfflineSetupRootFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                InitialDirectory = @"C:\Users",
                IsFolderPicker = true,
                AddToMostRecentlyUsedList = false,
                Title = "Select VS2017 offline setup folder"
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return false;

            SelectedFolderPath = dialog.FileName;
            return true;
        }

        #endregion

        #region DeleteOldVersionCommand
        private DelegateCommand _deleteOldVersionCommand;

        public DelegateCommand DeleteOldVersionCommand
        {
            get {
                return _deleteOldVersionCommand ?? (_deleteOldVersionCommand = new DelegateCommand(() =>
                {
                    try
                    {
                        //Delete old version folder and files
                        foreach (var folder in OldVersionModule)
                        {
                            Directory.Delete(folder.FullPath, true);
                        }
                        OldVersionModule.Clear();
                        MessageBox.Show("Operation successful.");
                    }
                    catch (Exception exception)
                    {
                        System.Diagnostics.Debug.WriteLine(exception.Message);
                    }
                }, ()=>!string.IsNullOrWhiteSpace(SelectedFolderPath) && OldVersionModule?.Count>0));
            }
        }

        #endregion
    }

    public class VsModule
    {
        public string Name { get; set; }

        #region Version
        private string _version;

        public string Version
        {
            get { return _version; }
            set { _version = value;
                VersionObject = new Version(value.Split('=')[1]);
            }
        }
        #endregion

        public string FullPath { get; set; }
        public Version VersionObject { get; private set; }
    }
}
