using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool
{
    internal partial class AccessLogControl : UserControl, ITaskProgress
    {
        private NotificationChannel _events;
        private ITaskProgress _progress;
        private CancellationToken _cancellationToken;

        private int _filterVersion;

        public AccessLogControl()
        {
            InitializeComponent();
        }

        public void Init(ArchiveSet archiveSet, ResourceUsageCache resourceUsages, NotificationChannel events, ITaskProgress progress, CancellationToken cancellationToken)
        {
            ArchiveSet = archiveSet;
            ResourceUsages = resourceUsages;

            _events = events;
            _progress = progress;
            _cancellationToken = cancellationToken;
            SubscribeToEvents(_events);
        }

        protected virtual void SubscribeToEvents(NotificationChannel events)
        {
        }

        protected virtual void UnsubscribeFromEvents(NotificationChannel events)
        {
        }

        protected bool EnableLogging
        {
            get;
            set;
        } = true;

        protected ArchiveSet ArchiveSet
        {
            get;
            private set;
        }

        protected ResourceUsageCache ResourceUsages
        {
            get;
            private set;
        }

        private void OnEnableLoggingChanged(object? sender, RoutedEventArgs e)
        {
            EnableLogging = _btnEnableLogging.IsChecked ?? false;
        }

        private void OnClearLogClick(object? sender, RoutedEventArgs e)
        {
            _lstLog.Clear();
        }

        private async void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            await CopyToClipboardAsync();
        }

        private async void OnLogKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.A)
                _lstLog.SelectAll();
            else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.C)
                await CopyToClipboardAsync();
        }

        private async Task CopyToClipboardAsync()
        {
            string csv = RowsToCsv(_lstLog.SelectedItems);
            Task? task = App.Clipboard?.SetTextAsync(csv);
            if (task != null)
                await task;
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            string? filePath = await App.SaveFilePickerAsync(
                "Save log to file",
                new()
                {
                    { "CSV files", ["*.csv"] }
                }
            );
            if (filePath == null)
                return;

            string csv = RowsToCsv(_lstLog.Items);
            File.WriteAllText(filePath, csv);
        }

        private string RowsToCsv(IList? rows)
        {
            if (rows == null)
                return string.Empty;

            StringBuilder result = new();
            foreach (IListViewEntry row in rows)
            {
                for (int colIdx = 0; colIdx < _lstLog.Columns.Count; colIdx++)
                {
                    if (colIdx > 0)
                        result.Append(',');

                    result.Append(row[colIdx]);
                }
                result.AppendLine();
            }
            return result.ToString();
        }

        private void OnFilterGotFocus(object? sender, GotFocusEventArgs e)
        {
            _txtFilter.Text = _lstLog.Filter;
            _txtFilter.SelectAll();
            _txtFilter.Opacity = 1;
        }

        private async void OnFilterChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_txtFilter.IsFocused)
                return;

            string? filter = _txtFilter.Text;
            int version = ++_filterVersion;
            await Task.Delay(1000);
            if (version != _filterVersion)
                return;

            _lstLog.Filter = filter;
        }

        private void OnFilterLostFocus(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lstLog.Filter))
            {
                _txtFilter.Text = "Filter";
                _txtFilter.Opacity = 0.5;
            }
        }

        private void OnLogSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateExtractButton();
        }

        private void UpdateExtractButton()
        {
            _btnExtract.IsEnabled = IsEnabled && _lstLog.Selection.Count > 0;
        }

        private async void OnLogDoubleClick(object? sender, TappedEventArgs e)
        {
            await ExtractAsync();
        }

        private async void OnExtractClick(object? sender, RoutedEventArgs e)
        {
            await ExtractAsync();
        }

        private async Task ExtractAsync()
        {
            try
            {
                string folderPath = Path.Combine(
                    AppContext.BaseDirectory,
                    CdcGameInfo.Get(ArchiveSet.Game).ShortName
                );
                await ExtractAsync(folderPath, this, _cancellationToken);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
            finally
            {
                ArchiveSet.CloseStreams();
            }
        }

        protected virtual async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
        }

        void ITaskProgress.Begin(string statusText)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ((ITaskProgress)this).Begin(statusText));
                return;
            }

            _progress.Begin(statusText);
            _lstLog.IsEnabled = false;
            _btnExtract.IsEnabled = false;
        }

        void ITaskProgress.Report(float progress)
        {
            _progress.Report(progress);
        }

        void ITaskProgress.End()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ((ITaskProgress)this).End());
                return;
            }

            _progress.End();
            _lstLog.IsEnabled = true;
            _btnExtract.IsEnabled = true;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            if (_events != null)
                UnsubscribeFromEvents(_events);
        }
    }
}
