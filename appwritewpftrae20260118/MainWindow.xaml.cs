using Appwrite;
using Appwrite.Services;
using Microsoft.Toolkit.Uwp.Notifications;
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
using System.Speech.Recognition;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace appwritewpftrae20260118
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string SubscriptionPage = "Subscriptions";
        private const string OilMonitorPage = "OilMonitor";
        private const string LotteryPage = "Lottery";
        private const string FeatureMenuPage = "FeatureMenu";
        private const string FengTubePage = "FengTube";
        private const string FengFinancePage = "FengFinance";
        private const string LotteryApiBaseUrl = "https://api.taiwanlottery.com/TLCAPIWeB";
        private const int TubeVideoLimitPerChannel = 10;

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _oilHistoryFilePath;
        private readonly string _financeStateFilePath;
        private readonly List<LotteryPick> _superLottoPicks = new List<LotteryPick>
        {
            LotteryPick.WithSpecial("第一組", new[] { 7, 11, 23, 32, 33, 38 }, 2),
            LotteryPick.WithSpecial("第二組", new[] { 7, 11, 23, 32, 33, 38 }, 1),
            LotteryPick.WithSpecial("第三組", new[] { 19, 8, 11, 27, 37, 16 }, 8),
            LotteryPick.WithSpecial("第四組", new[] { 19, 8, 4, 3, 37, 16 }, 8)
        };
        private readonly List<LotteryPick> _lotto649Picks = new List<LotteryPick>
        {
            LotteryPick.WithoutSpecial("第一組", new[] { 19, 8, 11, 27, 37, 16 }),
            LotteryPick.WithoutSpecial("第二組", new[] { 19, 8, 4, 3, 37, 16 })
        };
        private readonly List<LotteryPick> _daily539Picks = new List<LotteryPick>
        {
            LotteryPick.WithoutSpecial("第一組", new[] { 19, 8, 11, 27, 37 }),
            LotteryPick.WithoutSpecial("第二組", new[] { 19, 8, 4, 3, 37 })
        };

        private string _statusMessage = "準備載入訂閱資料";
        private string _oilStatusMessage = "準備載入油價資料";
        private string _lotteryStatusMessage = "準備載入彩券資料";
        private string _currentPage = SubscriptionPage;
        private string _oilCurrentPriceDisplay = "--";
        private string _oilMarkerDateDisplay = "尚未抓取資料";
        private string _oilLastFetchDisplay = "尚未抓取";
        private string _lotteryLastFetchDisplay = "尚未更新";
        private string _lotteryPeriodRangeDisplay = string.Empty;
        private FeatureMenuItem _selectedFeatureMenuItem;
        private bool _isBirthdayEasterEggVisible;
        private string _easterEggBadge = string.Empty;
        private string _easterEggTitle = string.Empty;
        private string _easterEggSubtitle = string.Empty;
        private Visibility _oilChartEmptyVisibility = Visibility.Visible;
        private Forms.NotifyIcon _notifyIcon;
        private Timer _dailyTimer;
        private DateTime _lastNotifyDate = DateTime.MinValue;
        private DateTime _lastOilFetchDate = DateTime.MinValue;
        private DateTime _lastTubeFetchDate = DateTime.MinValue;
        private DateTime _lastFinanceFetchDate = DateTime.MinValue;
        private string _sleepReminderMessage = string.Empty;
        private Visibility _sleepReminderVisibility = Visibility.Collapsed;
        private Brush _sleepReminderBackground = Brushes.Transparent;
        private Brush _sleepReminderBorderBrush = Brushes.Transparent;
        private Brush _sleepReminderForeground = Brushes.White;
        private DispatcherTimer _sleepReminderTimer;
        private SpeechRecognitionEngine _voiceRecognizer;
        private VoiceCommand _pendingVoiceCommand;
        private bool _isVoiceListening;
        private string _voiceStatusMessage = "語音輸入尚未啟動";
        private string _voiceLastPhrase = "尚未收到語音";
        private string _voicePendingCommandText = "沒有待確認指令";
        private string _voiceCommandSummary = "可說：鋒兄首頁、鋒兄儀表、鋒兄訂閱、鋒兄食品、鋒兄筆記、鋒兄常用、鋒兄圖片、鋒兄影片、鋒兄音樂、鋒兄文件、鋒兄播客、鋒兄銀行、鋒兄例行、鋒兄設定、鋒兄關於、重新整理。聽到後請再說「確認」或「取消」。";
        private string _tubeStatusMessage = "鋒兄Tube 尚未載入";
        private string _tubeFreshAlertMessage = string.Empty;
        private string _financeStatusMessage = "鋒兄金融尚未載入";
        private Visibility _tubeFreshAlertVisibility = Visibility.Collapsed;

        public ObservableCollection<Subscription> Subscriptions { get; } = new ObservableCollection<Subscription>();
        public ObservableCollection<OilPriceRecord> OilPriceHistory { get; } = new ObservableCollection<OilPriceRecord>();
        public ObservableCollection<OilPriceRecord> OilRecentRecords { get; } = new ObservableCollection<OilPriceRecord>();
        public ObservableCollection<LotteryResultRow> SuperLottoRows { get; } = new ObservableCollection<LotteryResultRow>();
        public ObservableCollection<LotteryResultRow> Lotto649Rows { get; } = new ObservableCollection<LotteryResultRow>();
        public ObservableCollection<LotteryResultRow> Daily539Rows { get; } = new ObservableCollection<LotteryResultRow>();
        public ObservableCollection<FinancialMarketItem> FinancialMarkets { get; } = new ObservableCollection<FinancialMarketItem>
        {
            new FinancialMarketItem("Nikkei 225 Index", ".N225", "https://www.cnbc.com/quotes/.N225"),
            new FinancialMarketItem("KOSPI Index", ".KS11", "https://www.cnbc.com/quotes/.KS11?qsearchterm=kospi"),
            new FinancialMarketItem("ICE Brent Crude", "@LCO.1", "https://www.cnbc.com/quotes/@LCO.1"),
            new FinancialMarketItem("U.S. 30 Year Treasury", "US30Y", "https://www.cnbc.com/quotes/US30Y"),
            new FinancialMarketItem("Gold COMEX", "@GC.1", "https://www.cnbc.com/quotes/@GC.1"),
            new FinancialMarketItem("Dow Jones Industrial Average", ".DJI", "https://www.cnbc.com/quotes/.DJI"),
            new FinancialMarketItem("S&P 500 Index", ".SPX", "https://www.cnbc.com/quotes/.SPX"),
            new FinancialMarketItem("NASDAQ Composite", ".IXIC", "https://www.cnbc.com/quotes/.IXIC"),
            new FinancialMarketItem("CBOE Volatility Index", ".VIX", "https://www.cnbc.com/quotes/.VIX"),
            new FinancialMarketItem("Bitcoin/USD Coin Metrics", "BTC.CM=", "https://www.cnbc.com/quotes/BTC.CM="),
            new FinancialMarketItem("Ether/USD Coin Metrics", "ETH.CM=", "https://www.cnbc.com/quotes/ETH.CM="),
            new FinancialMarketItem("加權指數", "^TWII", "https://tw.stock.yahoo.com/s/tse.php", FinancialQuoteProvider.Yahoo),
            new FinancialMarketItem("台積電", "2330.TW", "https://tw.stock.yahoo.com/quote/2330.TW", FinancialQuoteProvider.Yahoo)
        };
        public ObservableCollection<YouTubeChannelGroup> YouTubeChannels { get; } = new ObservableCollection<YouTubeChannelGroup>
        {
            new YouTubeChannelGroup("SJdiao", "https://www.youtube.com/@SJdiao/videos"),
            new YouTubeChannelGroup("一个狠人", "https://www.youtube.com/@henren778", watchesFallIndex: true),
            new YouTubeChannelGroup("libertas1984", "https://www.youtube.com/@libertas1984/videos"),
            new YouTubeChannelGroup("sunlao", "https://www.youtube.com/@sunlao/videos"),
            new YouTubeChannelGroup("Torontobigface", "https://www.youtube.com/@Torontobigface/videos"),
            new YouTubeChannelGroup("junyulan", "https://www.youtube.com/@junyulan/videos"),
            new YouTubeChannelGroup("blackwhite_raven", "https://www.youtube.com/@blackwhite_raven/videos"),
            new YouTubeChannelGroup("quedaren", "https://www.youtube.com/@quedaren/videos"),
            new YouTubeChannelGroup("夸克说", "https://www.youtube.com/@%E5%A4%B8%E5%85%8B%E8%AF%B4"),
            new YouTubeChannelGroup("喵喵看一看", "https://www.youtube.com/@%E5%96%B5%E5%96%B5%E7%9C%8B%E4%B8%80%E7%9C%8B/videos")
        };
        public ObservableCollection<FeatureMenuItem> FeatureMenuItems { get; } = new ObservableCollection<FeatureMenuItem>
        {
            new FeatureMenuItem("鋒兄銀行\n(或電子票證)", "BANKING", "整理 Appwrite bank collection，依台灣銀行、電子票證分類查看所有資產、銀行總資產與電子票證總資產。", "BankStatsActivity"),
            new FeatureMenuItem("鋒兄食品\n(或商品)", "FOOD", "搜尋與檢視食品或商品庫存、價格、數量、商店與效期資訊。", "FoodManagementActivity"),
            new FeatureMenuItem("鋒兄筆記", "NOTES", "讀取 article collection，依標題、內容與連結快速搜尋筆記。", "FengNotesActivity"),
            new FeatureMenuItem("常用帳號", "COMMON", "把常用網站與帳號資訊分組，做成桌面端可掃描的清單入口。", "FengCommonActivity"),
            new FeatureMenuItem("US Debt", "US DEBT", "追蹤美國國債數值與歷史趨勢。", "USDebtActivity"),
            new FeatureMenuItem("鋒兄比價", "PRICE COMPARE", "銜接 Android 的 PChome / momo 價格比較工具。", "PriceCompareActivity"),
            new FeatureMenuItem("電池狀態", "BATTERY", "顯示電池目前狀態、預估時間與最後充滿資訊。", "BatteryStatusActivity"),
            new FeatureMenuItem("鋒兄工具", "FENGBRO TOOLS", "工具集合入口，包含比價與手機比較等 Android 功能。", "FengToolsActivity"),
            new FeatureMenuItem("鋒兄首頁", "HOME", "集中啟動常用模組，快速回到桌面控制台的入口語音頁。", "MainActivity"),
            new FeatureMenuItem("鋒兄儀表", "DASHBOARD", "以儀表板方式整理訂閱、油價、彩券、銀行、食品與媒體模組。", "DashboardActivity"),
            new FeatureMenuItem("圖片管理", "IMAGE", "預留圖片 collection 的搜尋、檢視、分類與語音建立入口。", "ImageActivity"),
            new FeatureMenuItem("影片管理", "VIDEO", "預留影片 collection 的片名、平台、連結、標籤與播放狀態入口。", "VideoActivity"),
            new FeatureMenuItem("音樂管理", "MUSIC", "預留音樂 collection 的歌曲、專輯、播放清單與靈感筆記入口。", "MusicActivity"),
            new FeatureMenuItem("文件管理", "DOCUMENT", "預留文件 collection 的標題、分類、連結、摘要與查找入口。", "DocumentActivity"),
            new FeatureMenuItem("播客管理", "PODCAST", "預留播客 collection 的節目、集數、重點、連結與收聽狀態入口。", "PodcastActivity"),
            new FeatureMenuItem("例行事項", "ROUTINE", "預留 routine collection 的日常任務、週期、提醒與完成狀態入口。", "RoutineActivity"),
            new FeatureMenuItem("設定", "SETTINGS", "整理啟動、通知、資料來源、語音輸入與本機偏好設定。", "SettingsActivity"),
            new FeatureMenuItem("關於", "ABOUT", "顯示桌面控制台版本、資料來源、語音指令與維護資訊。", "AboutActivity")
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            _selectedFeatureMenuItem = FeatureMenuItems.FirstOrDefault();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            _oilHistoryFilePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppwriteSubscriptionViewer",
                "oil-marker-history.json");
            _financeStateFilePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppwriteSubscriptionViewer",
                "finance-highlow.json");
            InitializeNotificationIcon();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AppwriteSubscriptionViewer/1.0");
            UpdateBirthdayEasterEgg();
            InitializeSleepReminderTimer();
            InitializeVoiceInput();
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

        public string LotteryStatusMessage
        {
            get => _lotteryStatusMessage;
            set
            {
                if (_lotteryStatusMessage == value) return;
                _lotteryStatusMessage = value;
                OnPropertyChanged(nameof(LotteryStatusMessage));
                OnPropertyChanged(nameof(ActiveStatusMessage));
            }
        }

        public bool IsSubscriptionView => string.Equals(_currentPage, SubscriptionPage, StringComparison.Ordinal);
        public bool IsOilMonitorView => string.Equals(_currentPage, OilMonitorPage, StringComparison.Ordinal);
        public bool IsLotteryView => string.Equals(_currentPage, LotteryPage, StringComparison.Ordinal);
        public bool IsFeatureMenuView => string.Equals(_currentPage, FeatureMenuPage, StringComparison.Ordinal);
        public bool IsTubeView => string.Equals(_currentPage, FengTubePage, StringComparison.Ordinal);
        public bool IsFinanceView => string.Equals(_currentPage, FengFinancePage, StringComparison.Ordinal);
        public bool IsFengToolsSelected => IsFeatureMenuView && string.Equals(SelectedFeatureTitle, "鋒兄工具", StringComparison.Ordinal);
        public bool IsBankFeatureSelected => IsFeatureMenuView && string.Equals(SelectedFeatureTitle, "鋒兄銀行\n(或電子票證)", StringComparison.Ordinal);
        public string BankClassificationNote => "電子票證\n台灣的銀行才是銀行喔！中華郵政也屬於台灣銀行；銀行以外的先歸類為電子票證喔！\n1. 所有資產\n2. 銀行總資產\n3. 電子票證總資產";

        public string SelectedFeatureTitle => _selectedFeatureMenuItem?.Title ?? "功能選單";
        public string SelectedFeatureEyebrow => _selectedFeatureMenuItem?.Eyebrow ?? "ANDROID MENU";
        public string SelectedFeatureDescription => _selectedFeatureMenuItem?.Description ?? "參考 Android appwriteandroidtrae 的主畫面選單。";
        public string SelectedFeatureActivity => _selectedFeatureMenuItem?.ActivityName ?? "MainActivity";

        public string VoiceStatusMessage
        {
            get => _voiceStatusMessage;
            set
            {
                if (_voiceStatusMessage == value) return;
                _voiceStatusMessage = value;
                OnPropertyChanged(nameof(VoiceStatusMessage));
            }
        }

        public string VoiceLastPhrase
        {
            get => _voiceLastPhrase;
            set
            {
                if (_voiceLastPhrase == value) return;
                _voiceLastPhrase = value;
                OnPropertyChanged(nameof(VoiceLastPhrase));
            }
        }

        public string VoicePendingCommandText
        {
            get => _voicePendingCommandText;
            set
            {
                if (_voicePendingCommandText == value) return;
                _voicePendingCommandText = value;
                OnPropertyChanged(nameof(VoicePendingCommandText));
            }
        }

        public string VoiceCommandSummary
        {
            get => _voiceCommandSummary;
            set
            {
                if (_voiceCommandSummary == value) return;
                _voiceCommandSummary = value;
                OnPropertyChanged(nameof(VoiceCommandSummary));
            }
        }

        public string VoiceToggleLabel => _isVoiceListening ? "停止語音" : "開始語音";

        public string CurrentPageTitle
        {
            get
            {
                if (IsSubscriptionView) return "訂閱到期提醒";
                if (IsOilMonitorView) return "油價追蹤";
                if (IsFeatureMenuView) return SelectedFeatureTitle;
                if (IsTubeView) return "鋒兄Tube";
                if (IsFinanceView) return "鋒兄金融";
                return "最瞎結婚理由";
            }
        }

        public string CurrentPageSubtitle
        {
            get
            {
                if (IsSubscriptionView)
                {
                    return "查看 Appwrite SUBSCRIPTION 集合中的資料、排序下次扣款日，並在到期前三天給出提醒。";
                }

                if (IsOilMonitorView)
                {
                    return "追蹤 Gulf Mercantile Exchange 的 OQD Daily Marker Price，保留歷史資料並用圖表快速檢視變化。";
                }

                if (IsFeatureMenuView)
                {
                    return SelectedFeatureDescription;
                }

                if (IsTubeView)
                {
                    return "鋒兄工具子選單，集中追蹤指定 YouTube 頻道，每個頻道顯示最新 10 部影片，3 天內有新片會在首頁提醒。";
                }

                if (IsFinanceView)
                {
                    return "鋒兄工具子選單，追蹤 CNBC 指數、商品、債券與加密貨幣報價，突破本機記錄時標註創新高或創新低。";
                }

                return "根據台灣彩券官方 API 列出威力彩、大樂透、今彩539近三個月每期號碼，並比對你指定的號碼組。";
            }
        }

        public string CurrentActionLabel
        {
            get
            {
                if (IsSubscriptionView) return "重新整理";
                if (IsOilMonitorView) return "抓最新牌價";
                if (IsFeatureMenuView) return "確認選單";
                if (IsTubeView) return "更新Tube";
                if (IsFinanceView) return "更新金融";
                return "更新彩券資料";
            }
        }

        public string ActiveStatusMessage
        {
            get
            {
                if (IsSubscriptionView) return StatusMessage;
                if (IsOilMonitorView) return OilStatusMessage;
                if (IsFeatureMenuView) return $"已選取 {SelectedFeatureTitle}";
                if (IsTubeView) return TubeStatusMessage;
                if (IsFinanceView) return FinanceStatusMessage;
                return LotteryStatusMessage;
            }
        }

        public string FooterText
        {
            get
            {
                if (IsSubscriptionView) return "資料來源：Appwrite Databases / SUBSCRIPTION";
                if (IsOilMonitorView) return "資料來源：Gulf Mercantile Exchange / OQD Daily Marker Price";
                if (IsFeatureMenuView) return "選單參考：github.com/goldshoot0720/appwriteandroidtrae";
                if (IsTubeView) return "資料來源：YouTube channel RSS feeds";
                if (IsFinanceView) return "資料來源：CNBC Quotes";
                return "資料來源：台灣彩券官方 API";
            }
        }

        public string TubeStatusMessage
        {
            get => _tubeStatusMessage;
            set
            {
                if (_tubeStatusMessage == value) return;
                _tubeStatusMessage = value;
                OnPropertyChanged(nameof(TubeStatusMessage));
                OnPropertyChanged(nameof(ActiveStatusMessage));
            }
        }

        public string FinanceStatusMessage
        {
            get => _financeStatusMessage;
            set
            {
                if (_financeStatusMessage == value) return;
                _financeStatusMessage = value;
                OnPropertyChanged(nameof(FinanceStatusMessage));
                OnPropertyChanged(nameof(ActiveStatusMessage));
            }
        }

        public string TubeFreshAlertMessage
        {
            get => _tubeFreshAlertMessage;
            set
            {
                if (_tubeFreshAlertMessage == value) return;
                _tubeFreshAlertMessage = value;
                OnPropertyChanged(nameof(TubeFreshAlertMessage));
            }
        }

        public Visibility TubeFreshAlertVisibility
        {
            get => _tubeFreshAlertVisibility;
            set
            {
                if (_tubeFreshAlertVisibility == value) return;
                _tubeFreshAlertVisibility = value;
                OnPropertyChanged(nameof(TubeFreshAlertVisibility));
            }
        }

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

        public string LotteryLastFetchDisplay
        {
            get => _lotteryLastFetchDisplay;
            set
            {
                if (_lotteryLastFetchDisplay == value) return;
                _lotteryLastFetchDisplay = value;
                OnPropertyChanged(nameof(LotteryLastFetchDisplay));
            }
        }

        public string LotteryPeriodRangeDisplay
        {
            get => _lotteryPeriodRangeDisplay;
            set
            {
                if (_lotteryPeriodRangeDisplay == value) return;
                _lotteryPeriodRangeDisplay = value;
                OnPropertyChanged(nameof(LotteryPeriodRangeDisplay));
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

        public string SleepReminderMessage
        {
            get => _sleepReminderMessage;
            set
            {
                if (_sleepReminderMessage == value) return;
                _sleepReminderMessage = value;
                OnPropertyChanged(nameof(SleepReminderMessage));
            }
        }

        public Visibility SleepReminderVisibility
        {
            get => _sleepReminderVisibility;
            set
            {
                if (_sleepReminderVisibility == value) return;
                _sleepReminderVisibility = value;
                OnPropertyChanged(nameof(SleepReminderVisibility));
            }
        }

        public Brush SleepReminderBackground
        {
            get => _sleepReminderBackground;
            set
            {
                if (_sleepReminderBackground == value) return;
                _sleepReminderBackground = value;
                OnPropertyChanged(nameof(SleepReminderBackground));
            }
        }

        public Brush SleepReminderBorderBrush
        {
            get => _sleepReminderBorderBrush;
            set
            {
                if (_sleepReminderBorderBrush == value) return;
                _sleepReminderBorderBrush = value;
                OnPropertyChanged(nameof(SleepReminderBorderBrush));
            }
        }

        public Brush SleepReminderForeground
        {
            get => _sleepReminderForeground;
            set
            {
                if (_sleepReminderForeground == value) return;
                _sleepReminderForeground = value;
                OnPropertyChanged(nameof(SleepReminderForeground));
            }
        }

        public bool IsBirthdayEasterEggVisible
        {
            get => _isBirthdayEasterEggVisible;
            set
            {
                if (_isBirthdayEasterEggVisible == value) return;
                _isBirthdayEasterEggVisible = value;
                OnPropertyChanged(nameof(IsBirthdayEasterEggVisible));
            }
        }

        public string EasterEggBadge
        {
            get => _easterEggBadge;
            set
            {
                if (_easterEggBadge == value) return;
                _easterEggBadge = value;
                OnPropertyChanged(nameof(EasterEggBadge));
            }
        }

        public string EasterEggTitle
        {
            get => _easterEggTitle;
            set
            {
                if (_easterEggTitle == value) return;
                _easterEggTitle = value;
                OnPropertyChanged(nameof(EasterEggTitle));
            }
        }

        public string EasterEggSubtitle
        {
            get => _easterEggSubtitle;
            set
            {
                if (_easterEggSubtitle == value) return;
                _easterEggSubtitle = value;
                OnPropertyChanged(nameof(EasterEggSubtitle));
            }
        }

        public async Task InitializeLogicAsync()
        {
            LoadOilPriceHistoryFromDisk();
            await LoadSubscriptionsAsync();
            await RefreshOilDataAsync(forceFetch: false);
            await LoadLotteryDataAsync();
            await LoadFengTubeVideosAsync(showNotification: true);
            await LoadFengFinanceAsync(showNotification: true);
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

        private void InitializeSleepReminderTimer()
        {
            _sleepReminderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _sleepReminderTimer.Tick += (_, __) => CheckAndUpdateSleepReminder(DateTime.Now);
            _sleepReminderTimer.Start();
            CheckAndUpdateSleepReminder(DateTime.Now);
        }

        private void CheckAndUpdateSleepReminder(DateTime now)
        {
            if (now.Hour >= 0 && now.Hour <= 2)
            {
                SleepReminderMessage = "請入睡";
                SleepReminderBackground = new SolidColorBrush(Color.FromRgb(59, 50, 18));
                SleepReminderBorderBrush = new SolidColorBrush(Color.FromRgb(214, 169, 39));
                SleepReminderForeground = new SolidColorBrush(Color.FromRgb(255, 228, 154));
                SleepReminderVisibility = Visibility.Visible;
                return;
            }

            if (now.Hour >= 3 && now.Hour <= 6)
            {
                SleepReminderMessage = "請入睡";
                SleepReminderBackground = new SolidColorBrush(Color.FromRgb(66, 23, 23));
                SleepReminderBorderBrush = new SolidColorBrush(Color.FromRgb(224, 82, 82));
                SleepReminderForeground = new SolidColorBrush(Color.FromRgb(255, 213, 213));
                SleepReminderVisibility = Visibility.Visible;
                return;
            }

            SleepReminderMessage = string.Empty;
            SleepReminderVisibility = Visibility.Collapsed;
        }

        private void InitializeVoiceInput()
        {
            try
            {
                var recognizerInfo = SelectVoiceRecognizer();
                _voiceRecognizer = recognizerInfo == null
                    ? new SpeechRecognitionEngine()
                    : new SpeechRecognitionEngine(recognizerInfo);
                _voiceRecognizer.SetInputToDefaultAudioDevice();
                _voiceRecognizer.LoadGrammar(BuildVoiceGrammar(_voiceRecognizer.RecognizerInfo?.Culture ?? CultureInfo.CurrentCulture));
                _voiceRecognizer.SpeechRecognized += VoiceRecognizer_SpeechRecognized;
                _voiceRecognizer.SpeechRecognitionRejected += (_, __) =>
                {
                    Dispatcher.Invoke(() => VoiceStatusMessage = "沒有聽清楚，請再說一次。");
                };
                VoiceCommandSummary = BuildVoiceCommandSummary();
                VoiceStatusMessage = "語音已就緒，按「開始語音」後可下指令。";
            }
            catch (Exception ex)
            {
                VoiceStatusMessage = $"語音初始化失敗：{ex.Message}";
            }
        }

        private static RecognizerInfo SelectVoiceRecognizer()
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            return recognizers.FirstOrDefault(info => info.Culture.Name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase))
                   ?? recognizers.FirstOrDefault(info => info.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                   ?? recognizers.FirstOrDefault(info => info.Culture.Equals(CultureInfo.CurrentCulture))
                   ?? recognizers.FirstOrDefault();
        }

        private Grammar BuildVoiceGrammar(CultureInfo culture)
        {
            var choices = new Choices();
            foreach (var phrase in BuildVoiceCommands().SelectMany(command => command.Phrases).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                choices.Add(phrase);
            }

            foreach (var phrase in ConfirmPhrases.Concat(CancelPhrases))
            {
                choices.Add(phrase);
            }

            var builder = new GrammarBuilder(choices)
            {
                Culture = culture
            };

            return new Grammar(builder);
        }

        private void VoiceRecognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || e.Result.Confidence < 0.45)
            {
                Dispatcher.Invoke(() => VoiceStatusMessage = "語音信心不足，請靠近一點再說。");
                return;
            }

            Dispatcher.Invoke(() => HandleVoicePhrase(e.Result.Text));
        }

        private void HandleVoicePhrase(string phrase)
        {
            VoiceLastPhrase = $"聽到：{phrase}";
            var normalized = NormalizeVoicePhrase(phrase);

            if (IsConfirmPhrase(normalized))
            {
                ExecutePendingVoiceCommand();
                return;
            }

            if (IsCancelPhrase(normalized))
            {
                _pendingVoiceCommand = null;
                VoicePendingCommandText = "已取消，沒有待確認指令";
                VoiceStatusMessage = "語音指令已取消。";
                return;
            }

            var command = BuildVoiceCommands()
                .FirstOrDefault(item => item.Phrases.Any(candidate => NormalizeVoicePhrase(candidate) == normalized));
            if (command == null)
            {
                VoiceStatusMessage = "聽到了，但還不是已支援的指令。可說「語音說明」查看範例。";
                return;
            }

            _pendingVoiceCommand = command;
            VoicePendingCommandText = $"待確認：{command.Description}";
            VoiceStatusMessage = "已排入雙重確認，請說「確認」執行，或說「取消」放棄。";
        }

        private void ExecutePendingVoiceCommand()
        {
            if (_pendingVoiceCommand == null)
            {
                VoiceStatusMessage = "目前沒有待確認的語音指令。";
                return;
            }

            var command = _pendingVoiceCommand;
            _pendingVoiceCommand = null;
            VoicePendingCommandText = "沒有待確認指令";

            switch (command.Action)
            {
                case VoiceCommandAction.Subscription:
                    _currentPage = SubscriptionPage;
                    UpdatePageState();
                    break;
                case VoiceCommandAction.Oil:
                    _currentPage = OilMonitorPage;
                    UpdatePageState();
                    RenderOilPriceChart();
                    break;
                case VoiceCommandAction.Lottery:
                    _currentPage = LotteryPage;
                    UpdatePageState();
                    break;
                case VoiceCommandAction.FengTube:
                    _currentPage = FengTubePage;
                    UpdatePageState();
                    _ = LoadFengTubeVideosAsync(showNotification: true);
                    break;
                case VoiceCommandAction.FengFinance:
                    _currentPage = FengFinancePage;
                    UpdatePageState();
                    _ = LoadFengFinanceAsync(showNotification: true);
                    break;
                case VoiceCommandAction.Feature:
                    ShowFeatureMenu(command.TargetTitle);
                    break;
                case VoiceCommandAction.Refresh:
                    _ = RefreshCurrentViewFromVoiceAsync();
                    break;
                case VoiceCommandAction.Help:
                    VoiceStatusMessage = VoiceCommandSummary;
                    return;
            }

            VoiceStatusMessage = $"已執行：{command.Description}";
        }

        private async Task RefreshCurrentViewFromVoiceAsync()
        {
            if (IsSubscriptionView)
            {
                await LoadSubscriptionsAsync();
                await CheckAndNotifyExpiringSubscriptions();
                return;
            }

            if (IsOilMonitorView)
            {
                await RefreshOilDataAsync(forceFetch: true);
                return;
            }

            if (IsLotteryView)
            {
                await LoadLotteryDataAsync();
                return;
            }

            if (IsTubeView)
            {
                await LoadFengTubeVideosAsync(showNotification: true);
                return;
            }

            if (IsFinanceView)
            {
                await LoadFengFinanceAsync(showNotification: true);
                return;
            }

            VoiceStatusMessage = $"已確認 {SelectedFeatureTitle}，此頁目前是語音入口頁。";
        }

        private List<VoiceCommand> BuildVoiceCommands()
        {
            string[] Expand(params string[] aliases)
            {
                var verbs = new[]
                {
                    string.Empty,
                    "開啟",
                    "打開",
                    "前往",
                    "切到",
                    "進入",
                    "顯示",
                    "查看",
                    "我要看",
                    "我要開",
                    "幫我開",
                    "幫我打開",
                    "請開",
                    "請打開",
                    "帶我去"
                };
                return aliases
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .SelectMany(alias => verbs.Select(verb => $"{verb}{alias}"))
                    .Concat(aliases.Select(alias => $"鋒兄{alias}"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return new List<VoiceCommand>
            {
                VoiceCommand.ForFeature("鋒兄首頁", "開啟鋒兄首頁", Expand("鋒兄首頁", "首頁", "主畫面", "主頁", "首頁入口", "桌面控制台", "控制台", "回首頁", "回主畫面")),
                VoiceCommand.ForFeature("鋒兄儀表", "開啟鋒兄儀表", Expand("鋒兄儀表", "儀表", "儀表板", "dashboard", "總覽", "狀態總覽", "數據總覽", "統計總覽")),
                new VoiceCommand(VoiceCommandAction.Subscription, null, "開啟鋒兄訂閱", Expand("鋒兄訂閱", "訂閱", "訂閱提醒", "訂閱到期", "到期提醒", "付款提醒", "扣款提醒", "月費", "會員訂閱")),
                VoiceCommand.ForFeature("鋒兄食品\n(或商品)", "開啟鋒兄食品", Expand("鋒兄食品", "食品", "商品", "食物", "美食", "美食管理", "商品管理", "食物庫存", "食品庫存", "商品庫存", "吃的", "餐點", "食材")),
                VoiceCommand.ForFeature("鋒兄筆記", "開啟鋒兄筆記", Expand("鋒兄筆記", "筆記", "記事", "文章", "鋒兄文章", "文字筆記", "備忘錄", "知識庫", "靈感")),
                VoiceCommand.ForFeature("常用帳號", "開啟鋒兄常用", Expand("鋒兄常用", "常用", "常用帳號", "帳號", "網站帳號", "常用網站", "登入資料", "帳密入口")),
                VoiceCommand.ForFeature("圖片管理", "開啟鋒兄圖片", Expand("鋒兄圖片", "圖片", "圖片管理", "相簿", "照片", "圖庫", "影像", "圖片庫", "照片庫")),
                VoiceCommand.ForFeature("影片管理", "開啟鋒兄影片", Expand("鋒兄影片", "影片", "影片管理", "視頻", "影片庫", "片單", "追劇", "短片", "影音")),
                VoiceCommand.ForFeature("音樂管理", "開啟鋒兄音樂", Expand("鋒兄音樂", "音樂", "音樂管理", "歌曲", "歌單", "播放清單", "專輯", "音樂庫", "聲音收藏")),
                VoiceCommand.ForFeature("文件管理", "開啟鋒兄文件", Expand("鋒兄文件", "文件", "文件管理", "文檔", "檔案", "資料夾", "文件庫", "合約", "報告")),
                VoiceCommand.ForFeature("播客管理", "開啟鋒兄播客", Expand("鋒兄播客", "播客", "Podcast", "podcast", "節目", "節目清單", "收聽清單", "音頻節目")),
                VoiceCommand.ForFeature("鋒兄銀行\n(或電子票證)", "開啟鋒兄銀行", Expand("鋒兄銀行", "銀行", "銀行統計", "中華郵政", "郵政", "電子票證", "所有資產", "銀行總資產", "電子票證總資產", "帳戶", "存款", "提款", "轉帳", "財務", "卡片", "錢包")),
                VoiceCommand.ForFeature("例行事項", "開啟鋒兄例行", Expand("鋒兄例行", "例行", "例行事項", "routine", "日常", "每日任務", "固定任務", "習慣", "待辦")),
                VoiceCommand.ForFeature("設定", "開啟鋒兄設定", Expand("鋒兄設定", "設定", "偏好設定", "系統設定", "通知設定", "啟動設定", "語音設定", "本機設定")),
                VoiceCommand.ForFeature("關於", "開啟鋒兄關於", Expand("鋒兄關於", "關於", "關於鋒兄", "版本資訊", "程式資訊", "維護資訊", "說明頁")),
                new VoiceCommand(VoiceCommandAction.FengTube, null, "開啟鋒兄Tube", Expand("鋒兄Tube", "鋒兄tube", "Tube", "tube", "YouTube", "youtube", "影片頻道", "頻道追蹤", "最新影片")),
                new VoiceCommand(VoiceCommandAction.FengFinance, null, "開啟鋒兄金融", Expand("鋒兄金融", "金融", "金融市場", "市場報價", "股市", "指數", "美股", "比特幣", "黃金", "原油")),
                new VoiceCommand(VoiceCommandAction.Oil, null, "開啟油價追蹤", Expand("油價", "油價追蹤", "鋒兄油價", "汽油", "柴油", "油價圖表", "油價紀錄")),
                new VoiceCommand(VoiceCommandAction.Lottery, null, "開啟彩券比對", Expand("彩券", "最瞎結婚理由", "樂透", "大樂透", "威力彩", "今彩", "彩券比對")),
                new VoiceCommand(VoiceCommandAction.Refresh, null, "重新整理目前頁面", Expand("重新整理", "刷新", "更新", "重整", "抓最新", "重新載入", "同步資料", "更新資料")),
                new VoiceCommand(VoiceCommandAction.Help, null, "顯示語音說明", Expand("語音說明", "語音幫助", "指令說明", "可以說什麼", "語音指令", "語音範例", "幫助"))
            };
        }

        private string BuildVoiceCommandSummary()
        {
            var commandCount = BuildVoiceCommands().Sum(command => command.Phrases.Count);
            return $"可說首頁、儀表、訂閱、食品、筆記、常用、圖片、影片、音樂、文件、播客、銀行、例行、設定、關於，也可說重新整理、語音說明。已載入 {commandCount} 種說法；聽到後請再說「確認」或「取消」。";
        }

        private static readonly string[] ConfirmPhrases = { "確認", "確定", "好", "好的", "可以", "執行", "開始", "沒錯", "對", "是", "同意" };
        private static readonly string[] CancelPhrases = { "取消", "不要", "放棄", "停止", "算了", "不是", "否", "不用", "先不要" };

        private static string NormalizeVoicePhrase(string value)
        {
            return (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool IsConfirmPhrase(string value)
        {
            return ConfirmPhrases.Any(phrase => NormalizeVoicePhrase(phrase) == value);
        }

        private static bool IsCancelPhrase(string value)
        {
            return CancelPhrases.Any(phrase => NormalizeVoicePhrase(phrase) == value);
        }

        private void VoiceToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleVoiceListening();
        }

        private void ToggleVoiceListening()
        {
            if (_voiceRecognizer == null)
            {
                VoiceStatusMessage = "語音辨識尚未可用，請確認 Windows 語音元件與麥克風。";
                return;
            }

            try
            {
                if (_isVoiceListening)
                {
                    _voiceRecognizer.RecognizeAsyncStop();
                    _isVoiceListening = false;
                    VoiceStatusMessage = "語音輸入已停止。";
                }
                else
                {
                    _voiceRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                    _isVoiceListening = true;
                    VoiceStatusMessage = "正在聆聽，請說功能名稱，接著說「確認」。";
                }

                OnPropertyChanged(nameof(VoiceToggleLabel));
            }
            catch (Exception ex)
            {
                VoiceStatusMessage = $"語音切換失敗：{ex.Message}";
            }
        }

        private void UpdateBirthdayEasterEgg()
        {
            var today = DateTime.Today;
            if (today.Month == 4 && today.Day == 3)
            {
                EasterEggBadge = "APRIL 03 SPECIAL";
                EasterEggTitle = "塗哥生日快樂特效";
                EasterEggSubtitle = "今彩539頭獎得主鋒兄";
                IsBirthdayEasterEggVisible = true;
                return;
            }

            if (today.Month == 11 && today.Day == 27)
            {
                EasterEggBadge = "NOVEMBER 27 SPECIAL";
                EasterEggTitle = "鋒兄生日快樂特效";
                EasterEggSubtitle = "高考三級資訊處理榜首鋒兄";
                IsBirthdayEasterEggVisible = true;
                return;
            }

            EasterEggBadge = string.Empty;
            EasterEggTitle = string.Empty;
            EasterEggSubtitle = string.Empty;
            IsBirthdayEasterEggVisible = false;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsSubscriptionView)
            {
                await LoadSubscriptionsAsync();
                await CheckAndNotifyExpiringSubscriptions();
                return;
            }

            if (IsOilMonitorView)
            {
                await RefreshOilDataAsync(forceFetch: true);
                return;
            }

            if (IsFeatureMenuView)
            {
                OnPropertyChanged(nameof(ActiveStatusMessage));
                return;
            }

            if (IsTubeView)
            {
                await LoadFengTubeVideosAsync(showNotification: true);
                return;
            }

            if (IsFinanceView)
            {
                await LoadFengFinanceAsync(showNotification: true);
                return;
            }

            await LoadLotteryDataAsync();
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

        private void LotteryMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = LotteryPage;
            UpdatePageState();
        }

        private void BankStatsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("鋒兄銀行\n(或電子票證)");
        }

        private void FoodManagementMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("鋒兄食品\n(或商品)");
        }

        private void FengNotesMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("鋒兄筆記");
        }

        private void FengCommonMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("常用帳號");
        }

        private void UsDebtMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("US Debt");
        }

        private void PriceCompareMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("鋒兄比價");
        }

        private void BatteryStatusMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("電池狀態");
        }

        private void FengToolsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFeatureMenu("鋒兄工具");
        }

        private async void FengTubeMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = FengTubePage;
            UpdatePageState();
            if (!YouTubeChannels.Any(channel => channel.Videos.Any()))
            {
                await LoadFengTubeVideosAsync(showNotification: true);
            }
        }

        private async void FengFinanceMenuButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = FengFinancePage;
            UpdatePageState();
            if (FinancialMarkets.All(item => string.IsNullOrWhiteSpace(item.LastDisplay)))
            {
                await LoadFengFinanceAsync(showNotification: true);
            }
        }

        private void OpenTubeVideoButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string url && !string.IsNullOrWhiteSpace(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void ShowFeatureMenu(string title)
        {
            _selectedFeatureMenuItem = FeatureMenuItems.FirstOrDefault(item => string.Equals(item.Title, title, StringComparison.Ordinal))
                                       ?? FeatureMenuItems.FirstOrDefault();
            _currentPage = FeatureMenuPage;
            UpdatePageState();
        }

        private void UpdatePageState()
        {
            OnPropertyChanged(nameof(IsSubscriptionView));
            OnPropertyChanged(nameof(IsOilMonitorView));
            OnPropertyChanged(nameof(IsLotteryView));
            OnPropertyChanged(nameof(IsFeatureMenuView));
            OnPropertyChanged(nameof(IsTubeView));
            OnPropertyChanged(nameof(IsFinanceView));
            OnPropertyChanged(nameof(IsFengToolsSelected));
            OnPropertyChanged(nameof(IsBankFeatureSelected));
            OnPropertyChanged(nameof(CurrentPageTitle));
            OnPropertyChanged(nameof(CurrentPageSubtitle));
            OnPropertyChanged(nameof(CurrentActionLabel));
            OnPropertyChanged(nameof(ActiveStatusMessage));
            OnPropertyChanged(nameof(FooterText));
            OnPropertyChanged(nameof(SelectedFeatureTitle));
            OnPropertyChanged(nameof(SelectedFeatureEyebrow));
            OnPropertyChanged(nameof(SelectedFeatureDescription));
            OnPropertyChanged(nameof(SelectedFeatureActivity));
        }

        private async Task LoadFengTubeVideosAsync(bool showNotification)
        {
            TubeStatusMessage = "正在載入鋒兄Tube頻道...";
            var freshVideos = new List<YouTubeVideoItem>();
            var loadedChannels = 0;

            foreach (var channel in YouTubeChannels)
            {
                channel.Status = "載入中";
            }

            var channelResults = await Task.WhenAll(YouTubeChannels.Select(async channel =>
            {
                try
                {
                    var videos = await FetchYouTubeChannelVideosAsync(channel.SourceUrl);
                    return new YouTubeChannelLoadResult(channel, videos, null);
                }
                catch (Exception ex)
                {
                    return new YouTubeChannelLoadResult(channel, new List<YouTubeVideoItem>(), ex.Message);
                }
            }));

            foreach (var result in channelResults)
            {
                var channel = result.Channel;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    channel.Status = $"載入失敗：{result.ErrorMessage}";
                    continue;
                }

                var videos = result.Videos.Take(TubeVideoLimitPerChannel).ToList();
                channel.Videos.Clear();
                foreach (var video in videos)
                {
                    channel.Videos.Add(video);
                }

                channel.DisplayName = videos.FirstOrDefault()?.ChannelTitle ?? channel.DisplayName;
                channel.UpdateBadge = channel.WatchesFallIndex ? BuildFallIndexUpdateBadge(videos) : string.Empty;
                channel.Status = channel.Videos.Count == 0 ? "目前沒有讀到影片" : $"最新 {channel.Videos.Count} 部";
                freshVideos.AddRange(channel.Videos.Where(video => video.PublishedAt >= DateTimeOffset.Now.AddDays(-3)));
                loadedChannels++;
            }

            if (freshVideos.Count > 0)
            {
                var newest = freshVideos.OrderByDescending(video => video.PublishedAt).First();
                TubeFreshAlertMessage = $"鋒兄Tube 近 3 天有 {freshVideos.Count} 部新影片，最新：{newest.ChannelTitle} - {newest.Title}";
                TubeFreshAlertVisibility = Visibility.Visible;
                if (showNotification)
                {
                    ShowDesktopNotification("鋒兄Tube 新影片", TubeFreshAlertMessage);
                }
            }
            else
            {
                TubeFreshAlertMessage = "鋒兄Tube 近 3 天沒有新影片。";
                TubeFreshAlertVisibility = Visibility.Collapsed;
            }

            TubeStatusMessage = $"鋒兄Tube 已更新 {loadedChannels}/{YouTubeChannels.Count} 個頻道，時間 {DateTime.Now:HH:mm:ss}";
        }

        private async Task LoadFengFinanceAsync(bool showNotification)
        {
            FinanceStatusMessage = "正在載入鋒兄金融報價...";
            var state = LoadFinanceHighLowState();
            var breakoutMessages = new List<string>();

            try
            {
                var cnbcItems = FinancialMarkets.Where(item => item.Provider == FinancialQuoteProvider.Cnbc).ToList();
                var yahooItems = FinancialMarkets.Where(item => item.Provider == FinancialQuoteProvider.Yahoo).ToList();
                var symbols = string.Join("|", cnbcItems.Select(item => item.Symbol));
                var quoteMap = new Dictionary<string, CnbcQuote>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(symbols))
                {
                    var url = "https://quote.cnbc.com/quote-html-webservice/quote.htm"
                        + "?noform=1&partnerId=2&fund=1&exthrs=0&output=json&symbolType=symbol&requestMethod=extended&symbols="
                        + Uri.EscapeDataString(symbols);
                    var json = await _httpClient.GetStringAsync(url);
                    quoteMap = ParseCnbcQuotes(json);
                }

                foreach (var item in yahooItems)
                {
                    var quote = await FetchYahooQuoteAsync(item.Symbol);
                    quoteMap[NormalizeFinanceSymbol(item.Symbol)] = quote;
                }

                var loadedCount = 0;

                foreach (var item in FinancialMarkets)
                {
                    if (!quoteMap.TryGetValue(NormalizeFinanceSymbol(item.Symbol), out var quote))
                    {
                        item.Status = "未讀到資料";
                        continue;
                    }

                    item.Last = quote.Last;
                    item.Change = quote.Change;
                    item.ChangePercent = quote.ChangePercent;
                    item.LastUpdated = DateTime.Now;
                    item.Status = "已更新";
                    loadedCount++;

                    if (quote.Last.HasValue)
                    {
                        var badge = UpdateFinanceHighLowState(state, item.Symbol, quote.Last.Value);
                        item.HighLowBadge = badge;
                        if (!string.IsNullOrWhiteSpace(badge))
                        {
                            breakoutMessages.Add($"{item.Name} {badge} {item.LastDisplay}");
                        }
                    }
                }

                SaveFinanceHighLowState(state);
                FinanceStatusMessage = $"鋒兄金融已更新 {loadedCount}/{FinancialMarkets.Count} 項，時間 {DateTime.Now:HH:mm:ss}";
                if (showNotification && breakoutMessages.Count > 0)
                {
                    ShowDesktopNotification("鋒兄金融", string.Join("；", breakoutMessages.Take(3)));
                }
            }
            catch (Exception ex)
            {
                FinanceStatusMessage = $"鋒兄金融載入失敗：{ex.Message}";
            }
        }

        private Dictionary<string, CnbcQuote> ParseCnbcQuotes(string json)
        {
            var result = new Dictionary<string, CnbcQuote>(StringComparer.OrdinalIgnoreCase);
            using (var document = JsonDocument.Parse(json))
            {
                foreach (var element in EnumerateJsonObjects(document.RootElement))
                {
                    var fields = FlattenJsonObject(element);
                    if (!TryGetField(fields, out var symbol, "symbol"))
                    {
                        continue;
                    }

                    if (!TryGetDecimalField(fields, out var last, "last", "lastprice", "lasttradeprice", "price"))
                    {
                        continue;
                    }

                    TryGetDecimalField(fields, out var change, "change", "netchange");
                    TryGetDecimalField(fields, out var changePercent, "changepct", "changepercent", "percentagechange");
                    result[NormalizeFinanceSymbol(symbol)] = new CnbcQuote(last, change, changePercent);
                }
            }

            return result;
        }

        private async Task<CnbcQuote> FetchYahooQuoteAsync(string symbol)
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1m";
            var json = await _httpClient.GetStringAsync(url);
            using (var document = JsonDocument.Parse(json))
            {
                var result = document.RootElement.GetProperty("chart").GetProperty("result")[0];
                var meta = result.GetProperty("meta");
                var last = GetOptionalDecimal(meta, "regularMarketPrice");
                var previousClose = GetOptionalDecimal(meta, "previousClose") ?? GetOptionalDecimal(meta, "chartPreviousClose");
                decimal? change = null;
                decimal? changePercent = null;
                if (last.HasValue && previousClose.HasValue && previousClose.Value != 0)
                {
                    change = last.Value - previousClose.Value;
                    changePercent = change.Value / previousClose.Value * 100;
                }

                return new CnbcQuote(last, change, changePercent);
            }
        }

        private static decimal? GetOptionalDecimal(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String
                && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static IEnumerable<JsonElement> EnumerateJsonObjects(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                yield return element;
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var child in EnumerateJsonObjects(property.Value))
                    {
                        yield return child;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in EnumerateJsonObjects(item))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static Dictionary<string, string> FlattenJsonObject(JsonElement element)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Walk(JsonElement current)
            {
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                foreach (var property in current.EnumerateObject())
                {
                    var key = NormalizeFinanceField(property.Name);
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        Walk(property.Value);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            Walk(item);
                        }
                    }
                    else if (!fields.ContainsKey(key))
                    {
                        fields[key] = property.Value.ToString();
                    }
                }
            }

            Walk(element);
            return fields;
        }

        private static bool TryGetField(Dictionary<string, string> fields, out string value, params string[] names)
        {
            foreach (var name in names.Select(NormalizeFinanceField))
            {
                if (fields.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetDecimalField(Dictionary<string, string> fields, out decimal value, params string[] names)
        {
            value = 0;
            if (!TryGetField(fields, out var rawValue, names))
            {
                return false;
            }

            rawValue = rawValue.Replace(",", string.Empty).Replace("%", string.Empty).Trim();
            return decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private Dictionary<string, FinanceHighLowState> LoadFinanceHighLowState()
        {
            try
            {
                if (!File.Exists(_financeStateFilePath))
                {
                    return new Dictionary<string, FinanceHighLowState>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(_financeStateFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, FinanceHighLowState>>(json)
                       ?? new Dictionary<string, FinanceHighLowState>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, FinanceHighLowState>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveFinanceHighLowState(Dictionary<string, FinanceHighLowState> state)
        {
            var directory = System.IO.Path.GetDirectoryName(_financeStateFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_financeStateFilePath, json);
        }

        private static string UpdateFinanceHighLowState(Dictionary<string, FinanceHighLowState> state, string symbol, decimal last)
        {
            var key = NormalizeFinanceSymbol(symbol);
            if (!state.TryGetValue(key, out var record))
            {
                state[key] = new FinanceHighLowState { High = last, Low = last };
                return string.Empty;
            }

            var badge = string.Empty;
            if (last > record.High)
            {
                record.High = last;
                badge = "創新高";
            }

            if (last < record.Low)
            {
                record.Low = last;
                badge = "創新低";
            }

            return badge;
        }

        private static string NormalizeFinanceSymbol(string symbol)
        {
            return (symbol ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeFinanceField(string name)
        {
            return Regex.Replace(name ?? string.Empty, @"[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
        }

        private async Task<List<YouTubeVideoItem>> FetchYouTubeChannelVideosAsync(string channelUrl)
        {
            var html = await _httpClient.GetStringAsync(channelUrl);
            var feedUrl = ResolveYouTubeFeedUrl(html);
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                throw new InvalidOperationException("找不到頻道 RSS");
            }

            var xml = await _httpClient.GetStringAsync(feedUrl);
            var document = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace media = "http://search.yahoo.com/mrss/";

            return document.Root?
                       .Elements(atom + "entry")
                       .Select(entry =>
                       {
                           var link = entry.Elements(atom + "link").FirstOrDefault()?.Attribute("href")?.Value ?? string.Empty;
                           var publishedText = entry.Element(atom + "published")?.Value;
                           DateTimeOffset.TryParse(publishedText, out var publishedAt);
                           return new YouTubeVideoItem
                           {
                               Title = WebUtility.HtmlDecode(entry.Element(atom + "title")?.Value ?? "未命名影片"),
                               Url = link,
                               ChannelTitle = WebUtility.HtmlDecode(entry.Element(atom + "author")?.Element(atom + "name")?.Value ?? string.Empty),
                               PublishedAt = publishedAt,
                               ThumbnailUrl = entry.Element(media + "group")?.Element(media + "thumbnail")?.Attribute("url")?.Value ?? string.Empty
                           };
                       })
                       .OrderByDescending(video => video.PublishedAt)
                       .Take(TubeVideoLimitPerChannel)
                       .ToList()
                   ?? new List<YouTubeVideoItem>();
        }

        private static string ResolveYouTubeFeedUrl(string html)
        {
            var rssMatch = Regex.Match(html, @"""rssUrl"":""(?<url>[^""]+)""");
            if (rssMatch.Success)
            {
                return DecodeYouTubeJsonUrl(rssMatch.Groups["url"].Value);
            }

            var externalIdMatch = Regex.Match(html, @"""externalId"":""(?<id>UC[^""]+)""");
            if (externalIdMatch.Success)
            {
                return $"https://www.youtube.com/feeds/videos.xml?channel_id={externalIdMatch.Groups["id"].Value}";
            }

            var channelIdMatch = Regex.Match(html, @"""channelId"":""(?<id>UC[^""]+)""");
            if (channelIdMatch.Success)
            {
                return $"https://www.youtube.com/feeds/videos.xml?channel_id={channelIdMatch.Groups["id"].Value}";
            }

            return string.Empty;
        }

        private static string DecodeYouTubeJsonUrl(string value)
        {
            return WebUtility.HtmlDecode(value)
                .Replace("\\u0026", "&")
                .Replace("\\/", "/");
        }

        private static string BuildFallIndexUpdateBadge(IEnumerable<YouTubeVideoItem> videos)
        {
            foreach (var video in videos)
            {
                var match = Regex.Match(video.Title ?? string.Empty, @"倒台(指數|指数)\D*(?<value>\d+(?:[.．]\d+)?)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return $"更新 {match.Groups["value"].Value.Replace('．', '.')}";
                }
            }

            return string.Empty;
        }

        private async Task LoadSubscriptionsAsync()
        {
            try
            {
                StatusMessage = "正在載入訂閱資料...";

                var config = ReadConfig();
                if (!config.IsValid)
                {
                    StatusMessage = "Appwrite 設定不完整，請先檢查 App.config。";
                    return;
                }

                var databases = new Databases(BuildClient(config));
                var documents = await databases.ListDocuments(
                    databaseId: config.DatabaseId,
                    collectionId: config.SubscriptionCollectionId,
                    queries: new List<string> { Query.Limit(100) });

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
                    foreach (var item in list)
                    {
                        Subscriptions.Add(item);
                    }
                });

                StatusMessage = $"已載入 {Subscriptions.Count} 筆訂閱資料。";
            }
            catch (AppwriteException ex)
            {
                StatusMessage = $"載入訂閱資料失敗：{ex.Message}";
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
                OilStatusMessage = forceFetch ? "正在抓取最新 OQD Marker..." : "正在整理本機油價資料...";

                if (!forceFetch &&
                    OilPriceHistory.Count > 0 &&
                    DateTime.Now.Hour < ReadOilFetchHour() &&
                    OilPriceHistory.Any(x => x.MarkerDate.Date == DateTime.Today))
                {
                    UpdateOilSummary();
                    OilStatusMessage = "今天的油價資料已存在，暫時沿用本機紀錄。";
                    return;
                }

                var latestRecord = await FetchLatestOilMarkerAsync();
                UpsertOilRecord(latestRecord);
                SaveOilPriceHistoryToDisk();
                UpdateOilSummary();
                OilStatusMessage = $"已更新 OQD Marker：{latestRecord.Price:0.00}";
                _lastOilFetchDate = DateTime.Today;
            }
            catch (Exception ex)
            {
                UpdateOilSummary();
                OilStatusMessage = $"油價資料抓取失敗：{ex.Message}";
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
            var formats = new[] { "dd MMM-yyyy", "dd MMM, yyyy", "d MMM-yyyy", "d MMM, yyyy" };
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
            OilCurrentPriceDisplay = latest == null ? "--" : latest.Price.ToString("0.00", CultureInfo.InvariantCulture);
            OilMarkerDateDisplay = latest == null ? "尚未抓取資料" : $"Marker 日期 {latest.MarkerDate:yyyy-MM-dd}";
            OilLastFetchDisplay = latest == null ? "尚未抓取" : latest.CapturedAt.ToString("yyyy-MM-dd HH:mm");

            OilRecentRecords.Clear();
            foreach (var record in OilPriceHistory.OrderByDescending(r => r.MarkerDate).Take(12))
            {
                OilRecentRecords.Add(record);
            }

            OilChartEmptyVisibility = OilPriceHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderOilPriceChart();
        }
        private async Task LoadLotteryDataAsync()
        {
            try
            {
                LotteryStatusMessage = "正在載入台灣彩券官方資料...";

                var startMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-2);
                var endMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                LotteryPeriodRangeDisplay = $"查詢區間：{startMonth:yyyy-MM} 到 {endMonth:yyyy-MM}";

                var superTask = FetchLotteryDrawsAsync("/Lottery/SuperLotto638Result", "superLotto638Res", startMonth, endMonth, 6, true);
                var lottoTask = FetchLotteryDrawsAsync("/Lottery/Lotto649Result", "lotto649Res", startMonth, endMonth, 6, true);
                var dailyTask = FetchLotteryDrawsAsync("/Lottery/Daily539Result", "daily539Res", startMonth, endMonth, 5, false);

                await Task.WhenAll(superTask, lottoTask, dailyTask);

                FillLotteryRows(SuperLottoRows, superTask.Result, _superLottoPicks);
                FillLotteryRows(Lotto649Rows, lottoTask.Result, _lotto649Picks);
                FillLotteryRows(Daily539Rows, dailyTask.Result, _daily539Picks);

                LotteryLastFetchDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                LotteryStatusMessage = $"威力彩 {SuperLottoRows.Count} 期、大樂透 {Lotto649Rows.Count} 期、今彩539 {Daily539Rows.Count} 期已更新。";
            }
            catch (Exception ex)
            {
                LotteryStatusMessage = $"彩券資料更新失敗：{ex.Message}";
            }
        }

        private async Task<List<LotteryDrawResult>> FetchLotteryDrawsAsync(
            string apiPath,
            string resultProperty,
            DateTime startMonth,
            DateTime endMonth,
            int mainNumberCount,
            bool hasSpecialNumber)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}?period=&month={2:yyyy-MM}&endMonth={3:yyyy-MM}&pageNum=1&pageSize=200",
                LotteryApiBaseUrl,
                apiPath,
                startMonth,
                endMonth);

            using (var stream = await _httpClient.GetStreamAsync(url))
            using (var document = await JsonDocument.ParseAsync(stream))
            {
                var list = new List<LotteryDrawResult>();
                var root = document.RootElement;
                var content = root.GetProperty("content");
                var results = content.GetProperty(resultProperty);

                foreach (var item in results.EnumerateArray())
                {
                    var sortedNumbers = ReadIntArray(item, "drawNumberSize");
                    var appearNumbers = ReadIntArray(item, "drawNumberAppear");
                    var mainNumbers = sortedNumbers.Take(mainNumberCount).ToList();
                    var specialNumber = hasSpecialNumber && sortedNumbers.Count > mainNumberCount
                        ? (int?)sortedNumbers[mainNumberCount]
                        : null;

                    list.Add(new LotteryDrawResult
                    {
                        Period = item.GetProperty("period").GetRawText().Trim('"'),
                        LotteryDate = item.GetProperty("lotteryDate").GetDateTime(),
                        MainNumbers = mainNumbers,
                        DisplayNumbers = appearNumbers.Take(mainNumberCount).ToList(),
                        SpecialNumber = specialNumber,
                        HasSpecialNumber = hasSpecialNumber
                    });
                }

                return list.OrderByDescending(x => x.LotteryDate).ToList();
            }
        }

        private static List<int> ReadIntArray(JsonElement element, string propertyName)
        {
            var list = new List<int>();
            foreach (var item in element.GetProperty(propertyName).EnumerateArray())
            {
                list.Add(item.GetInt32());
            }

            return list;
        }

        private static void FillLotteryRows(
            ObservableCollection<LotteryResultRow> target,
            IEnumerable<LotteryDrawResult> draws,
            IReadOnlyList<LotteryPick> picks)
        {
            target.Clear();
            foreach (var draw in draws)
            {
                var comparisons = picks
                    .Select(pick => ComparePick(draw, pick))
                    .ToDictionary(result => result.PickLabel, result => result.ResultText);

                target.Add(new LotteryResultRow
                {
                    Period = draw.Period,
                    DrawDate = draw.LotteryDate.ToString("yyyy-MM-dd"),
                    WinningNumbers = draw.DisplayText,
                    Pick1 = comparisons.ContainsKey("第一組") ? comparisons["第一組"] : string.Empty,
                    Pick2 = comparisons.ContainsKey("第二組") ? comparisons["第二組"] : string.Empty,
                    Pick3 = comparisons.ContainsKey("第三組") ? comparisons["第三組"] : string.Empty,
                    Pick4 = comparisons.ContainsKey("第四組") ? comparisons["第四組"] : string.Empty
                });
            }
        }

        private static LotteryPickComparisonResult ComparePick(LotteryDrawResult draw, LotteryPick pick)
        {
            var matchCount = pick.MainNumbers.Count(number => draw.MainNumbers.Contains(number));
            var specialMatched = pick.SpecialNumber.HasValue && draw.SpecialNumber == pick.SpecialNumber.Value;
            var text = pick.SpecialNumber.HasValue
                ? $"主號中 {matchCount} 碼 / 特別號{(specialMatched ? "有中" : "未中")}"
                : $"主號中 {matchCount} 碼";

            return new LotteryPickComparisonResult
            {
                PickLabel = pick.Label,
                MainMatchCount = matchCount,
                SpecialMatched = specialMatched,
                ResultText = text
            };
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
                    Stroke = new SolidColorBrush(Color.FromRgb(31, 46, 72)),
                    StrokeThickness = 1
                });
            }

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(158, 193, 255)),
                StrokeThickness = 3
            };

            for (var index = 0; index < ordered.Count; index++)
            {
                var x = ordered.Count == 1
                    ? leftPadding + plotWidth / 2
                    : leftPadding + (plotWidth * index / (ordered.Count - 1));
                var normalized = (double)((ordered[index].Price - minPrice) / (maxPrice - minPrice));
                var y = topPadding + plotHeight - (plotHeight * normalized);
                polyline.Points.Add(new Point(x, y));

                OilChartCanvas.Children.Add(new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(158, 193, 255)),
                    Stroke = new SolidColorBrush(Color.FromRgb(12, 22, 40)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(142, 161, 189)),
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
                Text = "訂閱到期提醒"
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
            contextMenu.MenuItems.Add("顯示視窗", (s, e) => RestoreWindow());
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

                if (now.Hour >= 9 && _lastTubeFetchDate.Date != now.Date)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await LoadFengTubeVideosAsync(showNotification: true);
                    });
                    _lastTubeFetchDate = now.Date;
                }

                if (now.Hour >= 9 && _lastFinanceFetchDate.Date != now.Date)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await LoadFengFinanceAsync(showNotification: true);
                    });
                    _lastFinanceFetchDate = now.Date;
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
                    queries: new List<string> { Query.Limit(100) });

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
                    var daysText = daysLeft == 0 ? "今天到期" : daysLeft == 1 ? "明天到期" : $"還有 {daysLeft} 天到期";
                    var accountPart = string.IsNullOrWhiteSpace(sub.Account) ? string.Empty : $"帳號 {sub.Account} - ";
                    return $"{accountPart}{sub.Name}，{daysText}，日期 {sub.NextDate.Value:yyyy-MM-dd}";
                }).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificationList.ItemsSource = messages;
                    NotificationPanel.Visibility = Visibility.Visible;
                });

                foreach (var message in messages)
                {
                    ShowDesktopNotification("訂閱到期提醒", message);
                    await Task.Delay(3500);
                }
            }
            catch
            {
            }
        }

        private void ShowDesktopNotification(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
                return;
            }
            catch
            {
            }

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _notifyIcon.BalloonTipTitle = title;
                    _notifyIcon.BalloonTipText = message;
                    _notifyIcon.ShowBalloonTip(5000);
                });
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
            var client = new Client().SetEndpoint(config.Endpoint).SetProject(config.ProjectId);
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                client.SetKey(config.ApiKey);
            }

            return client;
        }

        private string ReadOilSourceUrl()
        {
            var config = ReadConfig();
            return string.IsNullOrWhiteSpace(config.OilMarkerUrl) ? "https://www.gulfmerc.com/" : config.OilMarkerUrl;
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
    public class LotteryResultRow
    {
        public string Period { get; set; }
        public string DrawDate { get; set; }
        public string WinningNumbers { get; set; }
        public string Pick1 { get; set; }
        public string Pick2 { get; set; }
        public string Pick3 { get; set; }
        public string Pick4 { get; set; }
    }

    public class FeatureMenuItem
    {
        public FeatureMenuItem(string title, string eyebrow, string description, string activityName)
        {
            Title = title;
            Eyebrow = eyebrow;
            Description = description;
            ActivityName = activityName;
        }

        public string Title { get; }
        public string Eyebrow { get; }
        public string Description { get; }
        public string ActivityName { get; }
    }

    public class YouTubeChannelGroup : INotifyPropertyChanged
    {
        private string _displayName;
        private string _status = "等待載入";
        private string _updateBadge = string.Empty;

        public YouTubeChannelGroup(string displayName, string sourceUrl, bool watchesFallIndex = false)
        {
            _displayName = displayName;
            SourceUrl = sourceUrl;
            WatchesFallIndex = watchesFallIndex;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName == value) return;
                _displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public string SourceUrl { get; }
        public bool WatchesFallIndex { get; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public string UpdateBadge
        {
            get => _updateBadge;
            set
            {
                if (_updateBadge == value) return;
                _updateBadge = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateBadge)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateBadgeVisibility)));
            }
        }

        public Visibility UpdateBadgeVisibility => string.IsNullOrWhiteSpace(UpdateBadge) ? Visibility.Collapsed : Visibility.Visible;

        public ObservableCollection<YouTubeVideoItem> Videos { get; } = new ObservableCollection<YouTubeVideoItem>();
    }

    public class FinancialMarketItem : INotifyPropertyChanged
    {
        private decimal? _last;
        private decimal? _change;
        private decimal? _changePercent;
        private string _highLowBadge = string.Empty;
        private string _status = "等待載入";
        private DateTime? _lastUpdated;

        public FinancialMarketItem(string name, string symbol, string sourceUrl, FinancialQuoteProvider provider = FinancialQuoteProvider.Cnbc)
        {
            Name = name;
            Symbol = symbol;
            SourceUrl = sourceUrl;
            Provider = provider;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; }
        public string Symbol { get; }
        public string SourceUrl { get; }
        public FinancialQuoteProvider Provider { get; }

        public decimal? Last
        {
            get => _last;
            set
            {
                if (_last == value) return;
                _last = value;
                Notify(nameof(Last));
                Notify(nameof(LastDisplay));
            }
        }

        public decimal? Change
        {
            get => _change;
            set
            {
                if (_change == value) return;
                _change = value;
                Notify(nameof(Change));
                Notify(nameof(ChangeDisplay));
            }
        }

        public decimal? ChangePercent
        {
            get => _changePercent;
            set
            {
                if (_changePercent == value) return;
                _changePercent = value;
                Notify(nameof(ChangePercent));
                Notify(nameof(ChangePercentDisplay));
            }
        }

        public string HighLowBadge
        {
            get => _highLowBadge;
            set
            {
                if (_highLowBadge == value) return;
                _highLowBadge = value;
                Notify(nameof(HighLowBadge));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                Notify(nameof(Status));
            }
        }

        public DateTime? LastUpdated
        {
            get => _lastUpdated;
            set
            {
                if (_lastUpdated == value) return;
                _lastUpdated = value;
                Notify(nameof(LastUpdated));
                Notify(nameof(LastUpdatedDisplay));
            }
        }

        public string LastDisplay => Last.HasValue ? Last.Value.ToString("#,0.####", CultureInfo.InvariantCulture) : "--";
        public string ChangeDisplay => Change.HasValue ? Change.Value.ToString("+#,0.####;-#,0.####;0", CultureInfo.InvariantCulture) : "--";
        public string ChangePercentDisplay => ChangePercent.HasValue ? $"{ChangePercent.Value:+0.##;-0.##;0}%" : "--";
        public string LastUpdatedDisplay => LastUpdated.HasValue ? LastUpdated.Value.ToString("HH:mm:ss") : "--";

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum FinancialQuoteProvider
    {
        Cnbc,
        Yahoo
    }

    internal class CnbcQuote
    {
        public CnbcQuote(decimal? last, decimal? change, decimal? changePercent)
        {
            Last = last;
            Change = change;
            ChangePercent = changePercent;
        }

        public decimal? Last { get; }
        public decimal? Change { get; }
        public decimal? ChangePercent { get; }
    }

    public class FinanceHighLowState
    {
        public decimal High { get; set; }
        public decimal Low { get; set; }
    }

    public class YouTubeVideoItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string ChannelTitle { get; set; }
        public string ThumbnailUrl { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string PublishedDisplay => PublishedAt == default ? "未知時間" : PublishedAt.LocalDateTime.ToString("yyyy/MM/dd HH:mm");
        public string AgeDisplay
        {
            get
            {
                if (PublishedAt == default) return "未知";
                var age = DateTimeOffset.Now - PublishedAt;
                if (age.TotalHours < 24) return $"{Math.Max(1, (int)Math.Round(age.TotalHours))} 小時前";
                return $"{Math.Max(1, (int)Math.Round(age.TotalDays))} 天前";
            }
        }

        public bool IsFresh => PublishedAt >= DateTimeOffset.Now.AddDays(-3);
        public string FreshBadge => IsFresh ? "3天內新片" : string.Empty;
    }

    internal class YouTubeChannelLoadResult
    {
        public YouTubeChannelLoadResult(YouTubeChannelGroup channel, List<YouTubeVideoItem> videos, string errorMessage)
        {
            Channel = channel;
            Videos = videos;
            ErrorMessage = errorMessage;
        }

        public YouTubeChannelGroup Channel { get; }
        public List<YouTubeVideoItem> Videos { get; }
        public string ErrorMessage { get; }
    }

    internal enum VoiceCommandAction
    {
        Subscription,
        Oil,
        Lottery,
        FengTube,
        FengFinance,
        Feature,
        Refresh,
        Help
    }

    internal class VoiceCommand
    {
        public VoiceCommand(VoiceCommandAction action, string targetTitle, string description, params string[] phrases)
        {
            Action = action;
            TargetTitle = targetTitle;
            Description = description;
            Phrases = phrases.ToList();
        }

        public VoiceCommandAction Action { get; }
        public string TargetTitle { get; }
        public string Description { get; }
        public List<string> Phrases { get; }

        public static VoiceCommand ForFeature(string targetTitle, string description, params string[] phrases)
        {
            return new VoiceCommand(VoiceCommandAction.Feature, targetTitle, description, phrases);
        }
    }

    internal class LotteryDrawResult
    {
        public string Period { get; set; }
        public DateTime LotteryDate { get; set; }
        public List<int> MainNumbers { get; set; } = new List<int>();
        public List<int> DisplayNumbers { get; set; } = new List<int>();
        public int? SpecialNumber { get; set; }
        public bool HasSpecialNumber { get; set; }

        public string DisplayText
        {
            get
            {
                var main = string.Join(" ", DisplayNumbers.Select(number => number.ToString("00")));
                return HasSpecialNumber && SpecialNumber.HasValue
                    ? $"{main} | 特別號 {SpecialNumber.Value:00}"
                    : main;
            }
        }
    }

    internal class LotteryPick
    {
        public string Label { get; set; }
        public List<int> MainNumbers { get; set; } = new List<int>();
        public int? SpecialNumber { get; set; }

        public static LotteryPick WithSpecial(string label, IEnumerable<int> mainNumbers, int specialNumber)
        {
            return new LotteryPick
            {
                Label = label,
                MainNumbers = mainNumbers.OrderBy(number => number).ToList(),
                SpecialNumber = specialNumber
            };
        }

        public static LotteryPick WithoutSpecial(string label, IEnumerable<int> mainNumbers)
        {
            return new LotteryPick
            {
                Label = label,
                MainNumbers = mainNumbers.OrderBy(number => number).ToList()
            };
        }
    }

    internal class LotteryPickComparisonResult
    {
        public string PickLabel { get; set; }
        public int MainMatchCount { get; set; }
        public bool SpecialMatched { get; set; }
        public string ResultText { get; set; }
    }
}
