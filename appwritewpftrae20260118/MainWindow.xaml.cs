using Appwrite;
using Appwrite.Models;
using Appwrite.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Timers;
using Forms = System.Windows.Forms;
using System.Drawing;

namespace appwritewpftrae20260118
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<Subscription> Subscriptions { get; } = new ObservableCollection<Subscription>();

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            InitializeNotificationIcon();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeLogicAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSubscriptionsAsync();
        }

        private Forms.NotifyIcon _notifyIcon;
        private Timer _dailyTimer;
        private DateTime _lastNotifyDate = DateTime.MinValue;

        public async Task InitializeLogicAsync()
        {
            await LoadSubscriptionsAsync();
            await CheckAndNotifyExpiringSubscriptions();
            _lastNotifyDate = DateTime.Today;
            ScheduleDailyExpiryCheck();
        }

        private async Task LoadSubscriptionsAsync()
        {
            try
            {
                StatusMessage = "正在載入訂閱資料...";

                var endpoint = ConfigurationManager.AppSettings["AppwriteEndpoint"];
                var projectId = ConfigurationManager.AppSettings["AppwriteProjectId"];
                var databaseId = ConfigurationManager.AppSettings["AppwriteDatabaseId"];
                var subscriptionCollectionId = ConfigurationManager.AppSettings["AppwriteSubscriptionCollectionId"];

                if (string.IsNullOrWhiteSpace(endpoint) ||
                    string.IsNullOrWhiteSpace(projectId) ||
                    string.IsNullOrWhiteSpace(databaseId) ||
                    string.IsNullOrWhiteSpace(subscriptionCollectionId))
                {
                    StatusMessage = "Appwrite 設定不完整，請在 App.config 中確認設定值。";
                    return;
                }

                var client = new Client()
                    .SetEndpoint(endpoint)
                    .SetProject(projectId);

                var databases = new Databases(client);

                var documents = await databases.ListDocuments(
                    databaseId: databaseId,
                    collectionId: subscriptionCollectionId
                );

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Subscriptions.Clear();

                    foreach (var document in documents.Documents)
                    {
                        var data = document.Data ?? new Dictionary<string, object>();

                        var subscription = new Subscription
                        {
                            Id = document.Id,
                            Name = GetString(data, "name"),
                            Site = GetString(data, "site"),
                            Price = GetNullableInt(data, "price"),
                            NextDate = GetNullableDateTime(data, "nextdate"),
                            Note = GetString(data, "note"),
                            Account = GetString(data, "account"),
                            CreatedAt = document.CreatedAt,
                            UpdatedAt = document.UpdatedAt
                        };

                        Subscriptions.Add(subscription);
                    }
                });

                StatusMessage = $"已載入 {Subscriptions.Count} 筆訂閱資料。";
            }
            catch (AppwriteException ex)
            {
                StatusMessage = $"載入失敗：{ex.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"發生錯誤：{ex.Message}";
            }
        }

        private void InitializeNotificationIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Icon = SystemIcons.Information,
                Text = "訂閱到期提醒"
            };

            // 左鍵單擊開啟 (回應使用者需求)
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    RestoreWindow();
                }
            };

            // 雙擊開啟
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

            // 右鍵選單
            var contextMenu = new Forms.ContextMenu();
            contextMenu.MenuItems.Add("開啟", (s, e) => RestoreWindow());
            contextMenu.MenuItems.Add("離開", (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Application.Current.Shutdown();
            });
            _notifyIcon.ContextMenu = contextMenu;
        }

        private void RestoreWindow()
        {
            // 如果視窗還沒載入或已關閉，可能需要重新建立，但此處假設 Application.Current.MainWindow 存在
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void ScheduleDailyExpiryCheck()
        {
            _dailyTimer = new Timer
            {
                Interval = TimeSpan.FromMinutes(5).TotalMilliseconds,
                AutoReset = true,
                Enabled = true
            };

            _dailyTimer.Elapsed += async (s, e) =>
            {
                var now = DateTime.Now;

                if (now.Hour < 18) return;
                if (_lastNotifyDate.Date == now.Date) return;

                await CheckAndNotifyExpiringSubscriptions();
                _lastNotifyDate = now.Date;
            };
        }

        private async Task CheckAndNotifyExpiringSubscriptions()
        {
            try
            {
                var endpoint = ConfigurationManager.AppSettings["AppwriteEndpoint"];
                var projectId = ConfigurationManager.AppSettings["AppwriteProjectId"];
                var databaseId = ConfigurationManager.AppSettings["AppwriteDatabaseId"];
                var subscriptionCollectionId = ConfigurationManager.AppSettings["AppwriteSubscriptionCollectionId"];

                if (string.IsNullOrWhiteSpace(endpoint) ||
                    string.IsNullOrWhiteSpace(projectId) ||
                    string.IsNullOrWhiteSpace(databaseId) ||
                    string.IsNullOrWhiteSpace(subscriptionCollectionId))
                {
                    return;
                }

                var client = new Client()
                    .SetEndpoint(endpoint)
                    .SetProject(projectId);

                var databases = new Databases(client);

                var today = DateTime.Today;
                var threeDaysLater = today.AddDays(3);

                var allDocuments = await databases.ListDocuments(
                    databaseId: databaseId,
                    collectionId: subscriptionCollectionId
                );

                var expiring = new List<Subscription>();

                foreach (var document in allDocuments.Documents)
                {
                    var data = document.Data ?? new Dictionary<string, object>();
                    var nextDate = GetNullableDateTime(data, "nextdate");

                    if (!nextDate.HasValue) continue;

                    var d = nextDate.Value.Date;
                    if (d >= today && d <= threeDaysLater)
                    {
                        expiring.Add(new Subscription
                        {
                            Id = document.Id,
                            Name = GetString(data, "name"),
                            NextDate = nextDate
                        });
                    }
                }

                if (expiring.Count == 0) return;

                var message = BuildExpiryMessage(expiring);

                _notifyIcon.BalloonTipTitle = "訂閱到期提醒";
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(5000);
            }
            catch
            {
            }
        }

        private static string BuildExpiryMessage(List<Subscription> expiring)
        {
            if (expiring == null || expiring.Count == 0) return "";

            if (expiring.Count == 1)
            {
                var s = expiring[0];
                var dateText = s.NextDate?.ToString("yyyy-MM-dd") ?? "";
                return $"「{s.Name}」將在 {dateText} 到期。";
            }

            var count = expiring.Count;
            var first = expiring[0];
            var dateTextFirst = first.NextDate?.ToString("yyyy-MM-dd") ?? "";

            return $"有 {count} 個訂閱在 3 天內到期，最近的是「{first.Name}」({dateTextFirst})。";
        }

        private static string GetString(IDictionary<string, object> data, string key)
        {
            if (data == null) return null;
            if (!data.TryGetValue(key, out var value) || value == null) return null;
            return value.ToString();
        }

        private static int? GetNullableInt(IDictionary<string, object> data, string key)
        {
            if (data == null) return null;
            if (!data.TryGetValue(key, out var value) || value == null) return null;

            if (value is int i) return i;
            if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            return null;
        }

        private static DateTime? GetNullableDateTime(IDictionary<string, object> data, string key)
        {
            if (data == null) return null;
            if (!data.TryGetValue(key, out var value) || value == null) return null;

            if (value is DateTime dt) return dt;
            if (DateTime.TryParse(value.ToString(), out var parsed)) return parsed;
            return null;
        }
    }

    public class Subscription
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Site { get; set; }
        public int? Price { get; set; }
        public DateTime? NextDate { get; set; }
        public string Note { get; set; }
        public string Account { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }

        public string NextDateString => NextDate?.ToString("yyyy-MM-dd") ?? "";
        public string CreatedAtString => FormatDateTimeString(CreatedAt);
        public string UpdatedAtString => FormatDateTimeString(UpdatedAt);

        private static string FormatDateTimeString(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (DateTime.TryParse(value, out var dt)) return dt.ToString("yyyy-MM-dd HH:mm");
            return value;
        }
    }
}
