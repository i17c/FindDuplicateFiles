﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FindDuplicateFiles.Common;
using FindDuplicateFiles.Extensions;
using FindDuplicateFiles.SearchFile;

namespace FindDuplicateFiles
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 已选取的要搜索的文件夹
        /// </summary>
        public ObservableCollection<string> SearchFolders { get; set; }
        /// <summary>
        /// 是否正在进行搜索
        /// </summary>
        private bool _isSearching;
        private readonly SearchDuplicateJob _searchDuplicateJob = new();

        /// <summary>
        /// 重复文件集合
        /// </summary>
        public ObservableCollection<DuplicateFileInfo> DuplicateFiles { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            LoadingAppConfig();
            LoadingTheme(GlobalArgs.AppConfig.Theme);

            BindingItemsSource();
            BindingSearchEvent();

            InitializeSearchConfig();
            InitializeLoading();
        }

        private void LoadingAppConfig()
        {
            string configPath = $"{AppDomain.CurrentDomain.BaseDirectory}{GlobalArgs.AppConfigPath}";
            string configString = File.ReadAllText(configPath);
            GlobalArgs.AppConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfigInfo>(configString);
        }

        private void InitializeSearchConfig()
        {
            //匹配方式
            ChkFileName.IsChecked = true;
            ChkFileSize.IsChecked = false;

            //选项
            ChkIgnoreSizeZero.IsChecked = true;
            ChkIgnoreHiddenFile.IsChecked = true;
            ChkIgnoreSmallFile.IsChecked = true;

            SearchFolders.Clear();
        }
        private void InitializeLoading()
        {
            DoubleAnimation da = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(3)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            RotateTransform rt = new RotateTransform();
            ImgLoading.RenderTransform = rt;
            rt.BeginAnimation(RotateTransform.AngleProperty, da);
            PanelLoading.Height = 0;
            PanelLoading.Margin = new Thickness(0, 0, 0, 0);
        }

        /// <summary>
        /// UI数据绑定
        /// </summary>
        private void BindingItemsSource()
        {
            SearchFolders = new ObservableCollection<string>();
            ListBoxSearchFolders.ItemsSource = SearchFolders;

            DuplicateFiles = new ObservableCollection<DuplicateFileInfo>();
            ListViewDuplicateFile.ItemsSource = DuplicateFiles;

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(ListViewDuplicateFile.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Key");
            view.GroupDescriptions.Add(groupDescription);

        }

        /// <summary>
        /// 绑定查找任务的内部事件
        /// </summary>
        private void BindingSearchEvent()
        {
            _searchDuplicateJob.ExecutedMessage = ExecutedMessage;
            _searchDuplicateJob.Duplicate = FindDuplicateFile;
        }

        private void ExecutedMessage(string message)
        {
            if (LblMessage.Dispatcher.CheckAccess())
            {
                LblMessage.Content = message;
            }
            else
            {
                LblMessage.Dispatcher.Invoke(() => { LblMessage.Content = message; });
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.ImgMaximize.Source = new BitmapImage(new Uri("pack://application:,,,/Images/maximize.png"));
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.ImgMaximize.Source = new BitmapImage(new Uri("pack://application:,,,/Images/restore.png"));
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnAddSearchFolder_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.ShowDialog();
            var path = fbd.SelectedPath;
            if (path.IsEmpty())
            {
                return;
            }

            if (SearchFolders.Contains(path))
            {
                return;
            }

            SearchFolders.Add(path);
        }

        private void BtnRemoveSearchFolder_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag;
            if (tag == null)
            {
                return;
            }

            SearchFolders.Remove(tag.ToString());
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchFolders.Count == 0)
            {
                MessageBox.Show("请选择要查找的文件夹");
                return;
            }

            //匹配方式校验
            SearchMatchEnum searchMatch = 0;
            if (ChkFileName.IsChecked == true)
            {
                searchMatch = searchMatch | SearchMatchEnum.FileName;
            }
            if (ChkFileSize.IsChecked == true)
            {
                searchMatch = searchMatch | SearchMatchEnum.FileSize;
            }
            if (searchMatch == 0)
            {
                MessageBox.Show("请选择匹配方式");
                return;
            }

            //选项
            SearchOptionEnum searchOption = 0;
            if (ChkIgnoreSizeZero.IsChecked == true)
            {
                searchOption = searchOption | SearchOptionEnum.IgnoreSizeZero;
            }

            if (ChkIgnoreHiddenFile.IsChecked == true)
            {
                searchOption = searchOption | SearchOptionEnum.IgnoreHiddenFile;
            }

            if (ChkIgnoreSmallFile.IsChecked == true)
            {
                searchOption = searchOption | SearchOptionEnum.IgnoreSmallFile;
            }

            if (_isSearching)
            {
                SetEndSearchStyle();
                _searchDuplicateJob.Stop();
            }
            else
            {
                SetBeginSearchStyle();
                DuplicateFiles.Clear();
                var config = new SearchConfigs()
                {
                    Folders = new List<string>(SearchFolders.ToList()),
                    SearchMatch = searchMatch,
                    SearchOption = searchOption
                };
                _searchDuplicateJob.Start(config);
            }
        }

        /// <summary>
        /// 开始搜索
        /// </summary>
        private void SetBeginSearchStyle()
        {
            PanelLoading.Height = 25;
            PanelLoading.Margin = new Thickness(5, 8, 5, 8);
            TxtSearch.Text = "停止";
            ImgSearch.Source = new BitmapImage(new Uri("pack://application:,,,/Images/stop.png"));
            _isSearching = true;
        }
        /// <summary>
        /// 结束搜索
        /// </summary>
        private void SetEndSearchStyle()
        {
            PanelLoading.Height = 0;
            PanelLoading.Margin = new Thickness(0, 0, 0, 0);
            TxtSearch.Text = "开始";
            ImgSearch.Source = new BitmapImage(new Uri("pack://application:,,,/Images/search.png"));
            _isSearching = false;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (_isSearching)
            {
                MessageBox.Show("任务执行中，禁止重置");
                return;
            }
            InitializeSearchConfig();
        }

        private void ChangeTheme_Click(object sender, RoutedEventArgs e)
        {

            var tag = (sender as Button)?.Tag;
            if (tag == null)
            {
                MessageBox.Show("修改主题失败：系统错误");
                return;
            }

            string theme = tag.ToString();

            LoadingTheme(theme);
            GlobalArgs.AppConfig.Theme = theme;
            string appConfigString = System.Text.Json.JsonSerializer.Serialize(GlobalArgs.AppConfig);
            string configPath = $"{AppDomain.CurrentDomain.BaseDirectory}{GlobalArgs.AppConfigPath}";
            File.WriteAllText(configPath, appConfigString);
        }

        private void LoadingTheme(string themeName)
        {
            try
            {
                var mResourceSkin = new ResourceDictionary()
                {
                    Source = new Uri($"/Themes/Theme{themeName}.xaml", UriKind.RelativeOrAbsolute)
                };
                Application.Current.Resources.MergedDictionaries[0] = mResourceSkin;
            }
            catch (IOException ex)
            {
                MessageBox.Show($"主题加载失败，{ex.Message}");
            }
        }

        private void FindDuplicateFile(string key, SimpleFileInfo simpleFile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DuplicateFiles.Add(new DuplicateFileInfo() { Key = key, Name = simpleFile.Name, Path = simpleFile.Path, Size = $"{Math.Round(simpleFile.Size, 2)} KB" });
            });
        }
    }
}