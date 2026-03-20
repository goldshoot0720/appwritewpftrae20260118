using Appwrite;
using Appwrite.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace appwritewpftrae20260118
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string SubscriptionPage = "Subscriptions";
        private const string OilMonitorPage = "OilMonitor";

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _oilHistoryFilePath;

        private string _statusMessage;
        private string _oilStatusMessage;
        private string _currentPage = SubscriptionPage;
        private string _oilCurrentPriceDisplay = "--";
        private string _oilMarkerDateDisplay = "尚未取得資料";
        private string _oilLastFetchDisplay = "尚未抓取";
        private Visibility _oilChartEmptyVisibility = Visibility.Visible;
        private Forms.NotifyIcon _notifyIcon;
        private Timer _dailyTimer;
        private DateTime _lastNotifyDate = DateTime.MinValue;
        private DateTime _lastOilFetchDate = DateTime.MinValue;

        public ObservableCollection<Subscription> Subscriptions { get; } = new ObservableCollection<Subscription>();
        public ObservableCollection<OilPriceRecord> OilPriceHistory { get; } = new ObservableCollection<OilPriceRecord>();
        public ObservableCollection<OilPriceRecord> OilRecentRecords { get; } = new ObservableCollection<OilPriceRecord>();

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            _oilHistoryFilePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppwriteSubscriptionViewer",
                "oil-marker-history.json");
            InitializeNotificationIcon();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AppwriteSubscriptionViewer/1.0");
            UpdatePageState();
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(ActiveStatusMessage));
            }
        }

        public string OilStatusMessage
        {
            get => _oilStatusMessage;
            set
            {
                if (_oilStatusMessage == value) return;
                _oilStatusMessage = value;
                OnPropertyChanged(nameof(OilStatusMessage));
                OnPropertyChanged(nameof(ActiveStatusMessage));
            }
        }

        public bool IsSubscriptionView => string.Equals(_currentPage, SubscriptionPage, StringComparison.Ordinal);
        public bool IsOilMonitorView => string.Equals(_currentPage, OilMonitorPage, StringComparison.Ordinal);

        public string CurrentPageTitle => IsSubscriptionView ? "訂閱總覽" : "原油監測";
        public string CurrentPageSubtitle => IsSubscriptionView
            ? "透過內部 Appwrite SUBSCRIPTION 資料表集中檢視所有訂閱資訊"
            : "根據 Gulf Mercantile Exchange 的 OQD Daily Marker Price，每天下午 1 點自動抓取並累積成本機歷史圖表。";
        public string CurrentActionLabel => IsSubscriptionView ? "重新整理訂閱" : "立即抓取油價";
        public string ActiveStatusMessage => IsSubscriptionView ? StatusMessage : OilStatusMessage;
        public string FooterText => IsSubscriptionView
            ? "資料來源｜Appwrite Databases / SUBSCRIPTION"
            : "資料來源｜Gulf Mercantile Exchange / OQD Daily Marker Price";

        public string OilCurrentPriceDisplay
        {
            get => _oilCurrentPriceDisplay;
            set
            {
                if (_oilCurrentPriceDisplay == value) return;
                _oilCurrentPriceDisplay = value;
                OnPropertyChanged(nameof(OilCurrentPriceDisplay));
            }
        }

        public string OilMarkerDateDisplay
        {
            get => _oilMarkerDateDisplay;
            set
            {
                if (_oilMarkerDateDisplay == value) return;
                _oilMarkerDateDisplay = value;
                OnPropertyChanged(nameof(OilMarkerDateDisplay));
            }
        }

        public string OilLastFetchDisplay
        {
            get => _oilLastFetchDisplay;
            set
            {
                if (_oilLastFetchDisplay == value) return;
                _oilLastFetchDisplay = value;
                OnPropertyChanged(nameof(OilLastFetchDisplay));
            }
        }

        public Visibility OilChartEmptyVisibility
        {
            get => _oilChartEmptyVisibility;
            set
            {
                if (_oilChartEmptyVisibility == value) return;
                _oilChartEmptyVisibility = value;
                OnPropertyChanged(nameof(OilChartEmptyVisibility));
            }
        }

        public async Task InitializeLogicAsync()
        {
            LoadOilPriceHistoryFromDisk();
            await LoadSubscriptionsAsync();
            await RefreshOilDataAsync(forceFetch: false);
            await CheckAndNotifyExpiringSubscriptions();
            _lastNotifyDate = DateTime.Today;
            ScheduleDailyTasks();
        }

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
            if (IsSubscriptionView)
            {
                await LoadSubscriptionsAsync();
                await CheckAndNotifyExpiringSubscriptions();
            }
            else
            {
                await RefreshOilDataAsync(forceFetch: true);
            }
        }

        private void SubscriptionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = SubscriptionPage;
            UpdatePageState();
        }

        private void OilMonitorMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = OilMonitorPage;
            UpdatePageState();
            RenderOilPriceChart();
        }

        private void UpdatePageState()
        {
            OnPropertyChanged(nameof(IsSubscriptionView));
            OnPropertyChanged(nameof(IsOilMonitorView));
            OnPropertyChanged(nameof(CurrentPageTitle));
            OnPropertyChanged(nameof(CurrentPageSubtitle));
            OnPropertyChanged(nameof(CurrentActionLabel));
            OnPropertyChanged(nameof(ActiveStatusMessage));
            OnPropertyChanged(nameof(FooterText));
        }

        private async Task LoadSubscriptionsAsync()
        {
            try
            {
                StatusMessage = "正在載入訂閱資料...";

                var config = ReadConfig();
                if (!config.IsValid)
                {
                    StatusMessage = "Appwrite 設定不完整，請檢查 App.config。";
                    return;
                }

                var databases = new Databases(BuildClient(config));
                var documents = await databases.ListDocuments(
                    databaseId: config.DatabaseId,
                    collectionId: config.SubscriptionCollectionId,
                    queries: new List<string> { Query.Limit(100) }
                );

                var list = new List<Subscription>();
                foreach (var document in documents.Documents)
                {
                    var data = document.Data ?? new Dictionary<string, object>();
                    list.Add(new Subscription
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
                    });
                }

                list.Sort((a, b) =>
                {
                    if (!a.NextDate.HasValue && !b.NextDate.HasValue) return 0;
                    if (!a.NextDate.HasValue) return 1;
                    if (!b.NextDate.HasValue) return -1;
                    return a.NextDate.Value.CompareTo(b.NextDate.Value);
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Subscriptions.Clear();
                    foreach (var sub in list)
                    {
                        Subscriptions.Add(sub);
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
                StatusMessage = $"發生未預期錯誤：{ex.Message}";
            }
        }

        private async Task RefreshOilDataAsync(bool forceFetch)
        {
            try
            {
                OilStatusMessage = forceFetch ? "正在抓取 OQD Marker..." : "正在同步原油資料...";

                if (!forceFetch && OilPriceHistory.Count > 0 && DateTime.Now.Hour < ReadOilFetchHour() && OilPriceHistory.Any(x => x.MarkerDate.Date == DateTime.Today))
                {
                    UpdateOilSummary();
                    OilStatusMessage = "使用今日已保存的原油資料。";
                    return;
                }

                var latestRecord = await FetchLatestOilMarkerAsync();
                UpsertOilRecord(latestRecord);
                SaveOilPriceHistoryToDisk();
                UpdateOilSummary();
                OilStatusMessage = $"已更新 OQD Marker：{latestRecord.Price.ToString("0.00", CultureInfo.InvariantCulture)}";
                _lastOilFetchDate = DateTime.Today;
            }
            catch (Exception ex)
            {
                UpdateOilSummary();
                OilStatusMessage = $"原油抓取失敗：{ex.Message}";
            }
        }

        private async Task<OilPriceRecord> FetchLatestOilMarkerAsync()
        {
            var url = ReadOilSourceUrl();
            var html = await _httpClient.GetStringAsync(url);
            var plainText = HtmlToPlainText(html);

            var lines = plainText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].IndexOf("OQD Daily Marker Price", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var price = ExtractPrice(lines, i + 1);
                var markerDate = ExtractDate(lines, i + 1);
                if (price.HasValue && markerDate.HasValue)
                {
                    return new OilPriceRecord
                    {
                        MarkerDate = markerDate.Value.Date,
                        Price = price.Value,
                        CapturedAt = DateTime.Now,
                        SourceUrl = url
                    };
                }
            }

            throw new InvalidOperationException("找不到 OQD Daily Marker Price。");
        }

        private static decimal? ExtractPrice(IReadOnlyList<string> lines, int startIndex)
        {
            for (var i = startIndex; i < Math.Min(lines.Count, startIndex + 8); i++)
            {
                var candidate = lines[i].Replace(",", string.Empty);
                if (decimal.TryParse(candidate, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
                {
                    return price;
                }
            }

            return null;
        }

        private static DateTime? ExtractDate(IReadOnlyList<string> lines, int startIndex)
        {
            var formats = new[]
            {
                "dd MMM-yyyy",
                "dd MMM, yyyy",
                "d MMM-yyyy",
                "d MMM, yyyy"
            };

            for (var i = startIndex; i < Math.Min(lines.Count, startIndex + 10); i++)
            {
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(lines[i], format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        return parsed;
                    }
                }

                if (DateTime.TryParse(lines[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
                {
                    return fallback;
                }
            }

            return null;
        }

        private static string HtmlToPlainText(string html)
        {
            var withLineBreaks = Regex.Replace(html, @"<(br|/p|/div|/li|/h\d)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withLineBreaks, "<.*?>", " ");
            var decoded = WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(decoded, @"[ \t]+", " ");
        }

        private void LoadOilPriceHistoryFromDisk()
        {
            try
            {
                if (!File.Exists(_oilHistoryFilePath))
                {
                    UpdateOilSummary();
                    return;
                }

                var json = File.ReadAllText(_oilHistoryFilePath);
                var records = JsonSerializer.Deserialize<List<OilPriceRecord>>(json) ?? new List<OilPriceRecord>();

                OilPriceHistory.Clear();
                foreach (var record in records.OrderBy(r => r.MarkerDate))
                {
                    OilPriceHistory.Add(record);
                }

                UpdateOilSummary();
            }
            catch
            {
                OilPriceHistory.Clear();
                UpdateOilSummary();
            }
        }

        private void SaveOilPriceHistoryToDisk()
        {
            var directory = System.IO.Path.GetDirectoryName(_oilHistoryFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var records = OilPriceHistory.OrderBy(r => r.MarkerDate).ToList();
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_oilHistoryFilePath, json);
        }

        private void UpsertOilRecord(OilPriceRecord latestRecord)
        {
            var existing = OilPriceHistory.FirstOrDefault(r => r.MarkerDate.Date == latestRecord.MarkerDate.Date);
            if (existing == null)
            {
                OilPriceHistory.Add(latestRecord);
            }
            else
            {
                existing.Price = latestRecord.Price;
                existing.CapturedAt = latestRecord.CapturedAt;
                existing.SourceUrl = latestRecord.SourceUrl;
            }

            var ordered = OilPriceHistory.OrderBy(r => r.MarkerDate).ToList();
            OilPriceHistory.Clear();
            foreach (var record in ordered)
            {
                OilPriceHistory.Add(record);
            }
        }

        private void UpdateOilSummary()
        {
            var latest = OilPriceHistory.OrderByDescending(r => r.MarkerDate).FirstOrDefault();
            OilCurrentPriceDisplay = latest == null
                ? "--"
                : latest.Price.ToString("0.00", CultureInfo.InvariantCulture);
            OilMarkerDateDisplay = latest == null
                ? "尚未取得資料"
                : $"Marker 日期 {latest.MarkerDate:yyyy-MM-dd}";
            OilLastFetchDisplay = latest == null
                ? "尚未抓取"
                : latest.CapturedAt.ToString("yyyy-MM-dd HH:mm");

            OilRecentRecords.Clear();
            foreach (var record in OilPriceHistory.OrderByDescending(r => r.MarkerDate).Take(12))
            {
                OilRecentRecords.Add(record);
            }

            OilChartEmptyVisibility = OilPriceHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderOilPriceChart();
        }

        private void RenderOilPriceChart()
        {
            if (OilChartCanvas == null)
            {
                return;
            }

            OilChartCanvas.Children.Clear();
            if (OilPriceHistory.Count == 0 || OilChartCanvas.ActualWidth < 80 || OilChartCanvas.ActualHeight < 80)
            {
                OilChartEmptyVisibility = Visibility.Visible;
                return;
            }

            OilChartEmptyVisibility = Visibility.Collapsed;

            var width = OilChartCanvas.ActualWidth;
            var height = OilChartCanvas.ActualHeight;
            const double leftPadding = 48;
            const double rightPadding = 24;
            const double topPadding = 20;
            const double bottomPadding = 36;

            var plotWidth = width - leftPadding - rightPadding;
            var plotHeight = height - topPadding - bottomPadding;

            var ordered = OilPriceHistory.OrderBy(r => r.MarkerDate).ToList();
            var minPrice = ordered.Min(r => r.Price);
            var maxPrice = ordered.Max(r => r.Price);
            if (minPrice == maxPrice)
            {
                minPrice -= 1;
                maxPrice += 1;
            }

            for (var i = 0; i < 4; i++)
            {
                var y = topPadding + (plotHeight / 3.0) * i;
                OilChartCanvas.Children.Add(new Line
                {
                    X1 = leftPadding,
                    X2 = width - rightPadding,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 46, 72)),
                    StrokeThickness = 1
                });
            }

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 193, 255)),
                StrokeThickness = 3
            };

            for (var index = 0; index < ordered.Count; index++)
            {
                var x = ordered.Count == 1
                    ? leftPadding + plotWidth / 2
                    : leftPadding + (plotWidth * index / (ordered.Count - 1));
                var normalized = (double)((ordered[index].Price - minPrice) / (maxPrice - minPrice));
                var y = topPadding + plotHeight - (plotHeight * normalized);
                polyline.Points.Add(new System.Windows.Point(x, y));

                OilChartCanvas.Children.Add(new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 193, 255)),
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 22, 40)),
                    StrokeThickness = 2,
                    Margin = new Thickness(x - 4, y - 4, 0, 0)
                });
            }

            OilChartCanvas.Children.Add(polyline);

            AddChartLabel($"{maxPrice:0.00}", 6, topPadding - 8, HorizontalAlignment.Left);
            AddChartLabel($"{minPrice:0.00}", 6, topPadding + plotHeight - 8, HorizontalAlignment.Left);
            AddChartLabel(ordered.First().MarkerDate.ToString("MM-dd"), leftPadding, height - bottomPadding + 8, HorizontalAlignment.Left);
            AddChartLabel(ordered.Last().MarkerDate.ToString("MM-dd"), width - rightPadding - 40, height - bottomPadding + 8, HorizontalAlignment.Left);
        }

        private void AddChartLabel(string text, double left, double top, HorizontalAlignment alignment)
        {
            OilChartCanvas.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 161, 189)),
                FontSize = 11,
                Width = 60,
                TextAlignment = alignment == HorizontalAlignment.Left ? TextAlignment.Left : TextAlignment.Right,
                Margin = new Thickness(left, top, 0, 0)
            });
        }

        private void OilChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderOilPriceChart();
        }

        private void InitializeNotificationIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Icon = Drawing.SystemIcons.Information,
                Text = "訂閱與原油監測"
            };

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    RestoreWindow();
                }
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

            var contextMenu = new Forms.ContextMenu();
            contextMenu.MenuItems.Add("顯示", (s, e) => RestoreWindow());
            contextMenu.MenuItems.Add("結束", (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Application.Current.Shutdown();
            });
            _notifyIcon.ContextMenu = contextMenu;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            base.OnClosing(e);
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void ScheduleDailyTasks()
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

                if (now.Hour >= 18 && _lastNotifyDate.Date != now.Date)
                {
                    await CheckAndNotifyExpiringSubscriptions();
                    _lastNotifyDate = now.Date;
                }

                if (now.Hour >= ReadOilFetchHour() && _lastOilFetchDate.Date != now.Date)
                {
                    await RefreshOilDataAsync(forceFetch: true);
                    _lastOilFetchDate = now.Date;
                }
            };
        }

        private async Task CheckAndNotifyExpiringSubscriptions()
        {
            try
            {
                var config = ReadConfig();
                if (!config.IsValid)
                {
                    return;
                }

                var databases = new Databases(BuildClient(config));
                var today = DateTime.Today;
                var threeDaysLater = today.AddDays(3);

                var allDocuments = await databases.ListDocuments(
                    databaseId: config.DatabaseId,
                    collectionId: config.SubscriptionCollectionId,
                    queries: new List<string> { Query.Limit(100) }
                );

                var expiring = new List<Subscription>();
                foreach (var document in allDocuments.Documents)
                {
                    var data = document.Data ?? new Dictionary<string, object>();
                    var nextDate = GetNullableDateTime(data, "nextdate");
                    if (!nextDate.HasValue) continue;

                    var date = nextDate.Value.Date;
                    if (date >= today && date <= threeDaysLater)
                    {
                        expiring.Add(new Subscription
                        {
                            Id = document.Id,
                            Name = GetString(data, "name"),
                            Account = GetString(data, "account"),
                            NextDate = nextDate
                        });
                    }
                }

                if (expiring.Count == 0)
                {
                    Application.Current.Dispatcher.Invoke(() => NotificationPanel.Visibility = Visibility.Collapsed);
                    return;
                }

                var messages = expiring.Select(sub =>
                {
                    var daysLeft = (sub.NextDate.Value.Date - today).Days;
                    var daysText = daysLeft == 0 ? "今天到期" : daysLeft == 1 ? "明天到期" : $"{daysLeft} 天後到期";
                    var accountPart = string.IsNullOrWhiteSpace(sub.Account) ? string.Empty : $"帳號 {sub.Account} 的 ";
                    return $"{accountPart}{sub.Name} {daysText}，日期 {sub.NextDate.Value:yyyy-MM-dd}。";
                }).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificationList.ItemsSource = messages;
                    NotificationPanel.Visibility = Visibility.Visible;
                });

                foreach (var message in messages)
                {
                    _notifyIcon.BalloonTipTitle = "訂閱到期提醒";
                    _notifyIcon.BalloonTipText = message;
                    _notifyIcon.ShowBalloonTip(5000);
                    await Task.Delay(3500);
                }
            }
            catch
            {
            }
        }

        private static AppwriteConfig ReadConfig()
        {
            return new AppwriteConfig
            {
                Endpoint = ConfigurationManager.AppSettings["AppwriteEndpoint"],
                ProjectId = ConfigurationManager.AppSettings["AppwriteProjectId"],
                DatabaseId = ConfigurationManager.AppSettings["AppwriteDatabaseId"],
                SubscriptionCollectionId = ConfigurationManager.AppSettings["AppwriteSubscriptionCollectionId"],
                ApiKey = ConfigurationManager.AppSettings["AppwriteApiKey"],
                OilMarkerUrl = ConfigurationManager.AppSettings["OilMarkerUrl"],
                OilFetchHour = ConfigurationManager.AppSettings["OilFetchHour"]
            };
        }

        private static Client BuildClient(AppwriteConfig config)
        {
            var client = new Client()
                .SetEndpoint(config.Endpoint)
                .SetProject(config.ProjectId);

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                client.SetKey(config.ApiKey);
            }

            return client;
        }

        private string ReadOilSourceUrl()
        {
            var config = ReadConfig();
            return string.IsNullOrWhiteSpace(config.OilMarkerUrl)
                ? "https://www.gulfmerc.com/"
                : config.OilMarkerUrl;
        }

        private int ReadOilFetchHour()
        {
            var config = ReadConfig();
            return int.TryParse(config.OilFetchHour, out var hour) ? hour : 13;
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
            if (value is int intValue) return intValue;
            return int.TryParse(value.ToString(), out var parsed) ? parsed : (int?)null;
        }

        private static DateTime? GetNullableDateTime(IDictionary<string, object> data, string key)
        {
            if (data == null) return null;
            if (!data.TryGetValue(key, out var value) || value == null) return null;
            if (value is DateTime dateValue) return dateValue;
            return DateTime.TryParse(value.ToString(), out var parsed) ? parsed : (DateTime?)null;
        }
    }

    internal class AppwriteConfig
    {
        public string Endpoint { get; set; }
        public string ProjectId { get; set; }
        public string DatabaseId { get; set; }
        public string SubscriptionCollectionId { get; set; }
        public string ApiKey { get; set; }
        public string OilMarkerUrl { get; set; }
        public string OilFetchHour { get; set; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(ProjectId) &&
            !string.IsNullOrWhiteSpace(DatabaseId) &&
            !string.IsNullOrWhiteSpace(SubscriptionCollectionId);
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

        public string NextDateString => NextDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        public string CreatedAtString => FormatDateTimeString(CreatedAt);
        public string UpdatedAtString => FormatDateTimeString(UpdatedAt);

        private static string FormatDateTimeString(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return DateTime.TryParse(value, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm") : value;
        }
    }

    public class OilPriceRecord
    {
        public DateTime MarkerDate { get; set; }
        public decimal Price { get; set; }
        public DateTime CapturedAt { get; set; }
        public string SourceUrl { get; set; }

        public string MarkerDateDisplay => MarkerDate.ToString("yyyy-MM-dd");
        public string CapturedAtDisplay => $"抓取時間 {CapturedAt:yyyy-MM-dd HH:mm}";
        public string PriceDisplay => Price.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
