﻿#region Apache License 2.0

// Copyright 2008-2010 Christian Rodemeyer
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using SvnFind.Properties;
using Point=System.Drawing.Point;
using Size=System.Drawing.Size;

namespace SvnFind.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();

            var settings = Settings.Default;
            Width = settings.Size.Width;
            Height = settings.Size.Height;
            Left = settings.Position.X;
            Top = settings.Position.Y;

            QueryText.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            var settings = Settings.Default;
            settings.Size = new Size((int)ActualWidth,  (int)ActualHeight);
            settings.Position = new Point((int)Left, (int)Top);
            settings.Save();
        }

        MainViewModel ViewModel
        {
            get { return (MainViewModel) DataContext; }
            set { DataContext = value; }
        }

        void QueryText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                QueryText.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                Query_Click(null, null);
            }
        }

        void Query_Click(object sender, RoutedEventArgs e)
        {
            DoActionWithWaitCursor(ViewModel.Query);
            if (HitList.Items.Count > 0) HitList.ScrollIntoView(HitList.Items[0]);
        }

        void HitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HitList.SelectedItem = null;
        }

        void Head_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RevisionRange = "Head";
        }

        void All_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RevisionRange = "All";
        }

        void SvnQueryHome_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://svnquery.tigris.org/");
        }

        void Help_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("Help.htm");
        }

        void HitItemLink_Click(object sender, RoutedEventArgs e)
        {
            DoActionWithWaitCursor(delegate
            {
                var hitViewModel = (HitViewModel) ((Hyperlink) e.Source).DataContext;
                hitViewModel.ShowContent(ViewModel.QueryResult.Svn);
            });
        }

        private void ShowMessage_Click(object sender, RoutedEventArgs e)
        {
            DoActionWithWaitCursor(delegate
            {
                var hitViewModel = (HitViewModel) ((MenuItem) e.Source).DataContext;
                hitViewModel.ShowLogMessage(ViewModel.QueryResult.Svn);
            });
        }

        static void DoActionWithWaitCursor(Action a)
        {
            try
            {
                Mouse.SetCursor(Cursors.Wait);
                a();
            }
            finally
            {
                Mouse.UpdateCursor();
            }
        }

        void RevisionRange_LostFocus(object sender, RoutedEventArgs e)
        {
            // ViewModel modifies value on set, target needs manual update if wpf < 4.0
            // TODO: Remove this when migrating to .NET 4.0
            Dispatcher.BeginInvoke((Action) delegate { RevisionRange.GetBindingExpression(TextBox.TextProperty).UpdateTarget(); });
        }

        void RevisionRange_GotFocus(object sender, RoutedEventArgs e)
        {
            RevisionRange.SelectAll();
        }

        private void OpenIndex_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenIndex();
        }

      
    }
}