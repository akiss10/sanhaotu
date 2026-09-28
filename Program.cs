using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: AssemblyTitle("Sanhao Rabbit Desktop Pet")]
[assembly: AssemblyProduct("Sanhao Rabbit Desktop Pet")]
[assembly: AssemblyDescription("A standalone always-on-top desktop pet for Windows.")]
[assembly: AssemblyCompany("Sanhao Rabbit")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace SanhaoTuDesktopPet
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool smokeTest = Array.IndexOf(args, "--smoke-test") >= 0;
            string captureMenuPath = null;

            for (int index = 0; index < args.Length; index++)
            {
                if (args[index].StartsWith("--capture-menu=", StringComparison.OrdinalIgnoreCase))
                {
                    captureMenuPath = args[index].Substring("--capture-menu=".Length);
                }
            }

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;

            try
            {
                PetWindow window = new PetWindow(smokeTest);
                if (!string.IsNullOrEmpty(captureMenuPath))
                {
                    window.CaptureMenuPreview(captureMenuPath);
                    return;
                }

                application.Run(window);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "\u65E0\u6CD5\u542F\u52A8\u4E09\u597D\u5154\u684C\u9762\u5BA0\u7269\u3002\n\n" + exception.Message,
                    "\u4E09\u597D\u5154\u684C\u9762\u5BA0\u7269",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    internal sealed class Animation
    {
        public Animation(int row, int frameCount, int[] durations, bool loop)
        {
            Row = row;
            FrameCount = frameCount;
            Durations = durations;
            Loop = loop;
        }

        public int Row { get; private set; }
        public int FrameCount { get; private set; }
        public int[] Durations { get; private set; }
        public bool Loop { get; private set; }
    }

    internal sealed class PetWindow : Window
    {
        private const int CellWidth = 192;
        private const int CellHeight = 208;
        private const int AtlasColumns = 8;
        private const int AtlasRows = 9;
        private const double BaseScale = 1.25;
        private const int DragThreshold = 4;
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int VkBack = 0x08;
        private const int VkDelete = 0x2E;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;
        private const int VkProcessKey = 0xE5;
        private const int VkShift = 0x10;
        private const int VkEscape = 0x1B;
        private const int VkReturn = 0x0D;
        private const int VkC = 0x43;
        private const int VkV = 0x56;

        private static readonly SolidColorBrush TextBrush = MakeBrush("#4A3228");
        private static readonly SolidColorBrush MutedBrush = MakeBrush("#8A6F62");
        private static readonly SolidColorBrush AccentBrush = MakeBrush("#C87958");
        private static readonly SolidColorBrush HoverBrush = MakeBrush("#F5E7DA");
        private static readonly SolidColorBrush PressedBrush = MakeBrush("#EED9C8");
        private static readonly SolidColorBrush MenuBackgroundBrush = MakeBrush("#FFFBF6");
        private static readonly SolidColorBrush MenuBorderBrush = MakeBrush("#E8D6C7");
        private static readonly SolidColorBrush SoftFillBrush = MakeBrush("#F7ECE2");
        private static readonly SolidColorBrush ScaleBorderBrush = MakeBrush("#E9D8CA");
        private static readonly SolidColorBrush ScaleHoverBrush = MakeBrush("#F2E0D0");
        private static readonly SolidColorBrush DangerBrush = MakeBrush("#B65345");
        private static readonly SolidColorBrush DangerFillBrush = MakeBrush("#FCEBE7");
        private static readonly SolidColorBrush DangerHoverBrush = MakeBrush("#F7DCD6");
        private static readonly SolidColorBrush DividerBrush = MakeBrush("#EFE0D5");

        private static readonly Animation Idle = new Animation(
            0, 6, new int[] { 280, 110, 110, 140, 140, 320 }, true);

        private static readonly Animation SwingRight = new Animation(
            1, 8, new int[] { 120, 120, 120, 120, 120, 120, 120, 220 }, false);

        private static readonly Animation SwingLeft = new Animation(
            2, 8, new int[] { 120, 120, 120, 120, 120, 120, 120, 220 }, false);

        private static readonly Animation Hop = new Animation(
            3, 4, new int[] { 140, 140, 140, 280 }, false);

        private static readonly Animation Wink = new Animation(
            4, 5, new int[] { 140, 140, 140, 140, 280 }, false);

        private static readonly Animation Dizzy = new Animation(
            5, 8, new int[] { 140, 140, 140, 140, 140, 140, 140, 240 }, false);

        private static readonly Animation Waiting = new Animation(
            6, 6, new int[] { 150, 150, 150, 150, 150, 260 }, false);

        private static readonly Animation WaitingLoop = new Animation(
            6, 6, new int[] { 150, 150, 150, 150, 150, 260 }, true);

        private static readonly Animation Typing = new Animation(
            7, 6, new int[] { 120, 120, 120, 120, 120, 220 }, false);

        private static readonly Animation TypingLoop = new Animation(
            7, 6, new int[] { 120, 120, 120, 120, 120, 220 }, true);

        private static readonly Animation Smile = new Animation(
            8, 6, new int[] { 150, 150, 150, 150, 150, 280 }, false);

        private readonly Image petImage;
        private readonly BitmapSource[,] frames;
        private readonly DispatcherTimer animationTimer;
        private readonly bool smokeTest;
        private readonly Dictionary<int, RadioButton> scaleButtons;
        private readonly Popup petMenuPopup;
        private readonly DispatcherTimer keyboardIdleTimer;
        private readonly LowLevelKeyboardProc keyboardHookProc;
        private readonly DispatcherTimer hoverTimer;

        private Border keyboardSyncBadge;
        private TextBlock keyboardSyncStatusText;

        private IntPtr keyboardHook;
        private DateTime keyboardTypingUntilUtc;
        private DateTime waitingUntilUtc;
        private DateTime lastDeleteUtc;
        private DateTime foregroundCheckTimeUtc;
        private int typingSessionKeyCount;
        private int rapidDeleteCount;
        private bool smileBeforeTypingPending;
        private bool foregroundIsChatApp;

        private Animation currentAnimation;
        private int currentFrame;
        private int scalePercent;

        private bool dragging;
        private bool movedWhileDragging;
        private Point dragStartCursor;
        private double dragStartLeft;
        private double dragStartTop;

        public PetWindow(bool smokeTest)
        {
            this.smokeTest = smokeTest;
            frames = LoadFrames();
            scaleButtons = new Dictionary<int, RadioButton>();
            scalePercent = 100;
            animationTimer = new DispatcherTimer(DispatcherPriority.Render);
            animationTimer.Tick += AnimationTimer_Tick;
            keyboardIdleTimer = new DispatcherTimer(DispatcherPriority.Background);
            keyboardIdleTimer.Interval = TimeSpan.FromMilliseconds(150);
            keyboardIdleTimer.Tick += KeyboardIdleTimer_Tick;
            keyboardHookProc = KeyboardHookCallback;
            hoverTimer = new DispatcherTimer(DispatcherPriority.Background);
            hoverTimer.Interval = TimeSpan.FromSeconds(2);
            hoverTimer.Tick += HoverTimer_Tick;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Width = GetWindowWidth();
            Height = GetWindowHeight();
            Title = "\u4E09\u597D\u5154";
            Cursor = Cursors.Hand;

            petImage = new Image();
            petImage.Stretch = Stretch.Fill;
            petImage.Source = frames[0, 0];
            petImage.IsHitTestVisible = true;
            Content = petImage;

            RenderOptions.SetBitmapScalingMode(petImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(petImage, EdgeMode.Unspecified);

            petMenuPopup = BuildPetMenu();
            Loaded += PetWindow_Loaded;
            Closed += PetWindow_Closed;
            PreviewMouseLeftButtonDown += PetWindow_PreviewMouseLeftButtonDown;
            PreviewMouseMove += PetWindow_PreviewMouseMove;
            PreviewMouseLeftButtonUp += PetWindow_PreviewMouseLeftButtonUp;
            PreviewMouseRightButtonUp += PetWindow_PreviewMouseRightButtonUp;
            MouseEnter += PetWindow_MouseEnter;
            MouseLeave += PetWindow_MouseLeave;

            Play(Idle);
        }

        protected override void OnClosed(EventArgs e)
        {
            animationTimer.Stop();
            keyboardIdleTimer.Stop();
            hoverTimer.Stop();
            UninstallKeyboardHook();
            base.OnClosed(e);
        }

        internal void CaptureMenuPreview(string path)
        {
            Border menuContent = BuildMenuContent();
            menuContent.HorizontalAlignment = HorizontalAlignment.Left;
            menuContent.VerticalAlignment = VerticalAlignment.Top;

            Size renderSize = new Size(360, 720);
            menuContent.Measure(renderSize);
            menuContent.Arrange(new Rect(new Point(0, 0), renderSize));
            menuContent.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(
                (int)renderSize.Width,
                (int)renderSize.Height,
                96,
                96,
                PixelFormats.Pbgra32);
            bitmap.Render(menuContent);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private void PetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MoveToDefaultPosition();
            InstallKeyboardHook();
            keyboardIdleTimer.Start();

            if (smokeTest)
            {
                HandleTypingInput();
                bool typingStateOk = currentAnimation == TypingLoop;
                HandleDeleteInput();
                bool dizzyStateOk = currentAnimation == Dizzy;
                HandleSendInput();
                bool waitingStateOk = waitingUntilUtc > DateTime.UtcNow;
                string testStatusPath = Path.Combine(
                    Path.GetTempPath(),
                    "SanhaoTuDesktopPet-keyboard-status.txt");
                File.WriteAllText(
                    testStatusPath,
                    "HookInstalled=" + (keyboardHook != IntPtr.Zero)
                    + Environment.NewLine
                    + "TypingState=" + typingStateOk
                    + Environment.NewLine
                    + "DizzyState=" + dizzyStateOk
                    + Environment.NewLine
                    + "WaitingState=" + waitingStateOk
                    + Environment.NewLine
                    + "GlobalMode=True");

                OpenPetMenu();
                DispatcherTimer exitTimer = new DispatcherTimer();
                exitTimer.Interval = TimeSpan.FromMilliseconds(700);
                exitTimer.Tick += delegate(object timerSender, EventArgs timerArgs)
                {
                    exitTimer.Stop();
                    Application.Current.Shutdown(0);
                };
                exitTimer.Start();
            }
        }

        private void PetWindow_Closed(object sender, EventArgs e)
        {
            animationTimer.Stop();
        }

        private static SolidColorBrush MakeBrush(string color)
        {
            SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color);
            brush.Freeze();
            return brush;
        }

        private double GetWindowWidth()
        {
            return CellWidth * BaseScale * scalePercent / 100.0;
        }

        private double GetWindowHeight()
        {
            return CellHeight * BaseScale * scalePercent / 100.0;
        }

        private static BitmapSource[,] LoadFrames()
        {
            const string resourceName = "SanhaoTuDesktopPet.assets.spritesheet.png";
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException("The embedded spritesheet was not found.");
            }

            BitmapImage atlas;
            using (stream)
            {
                atlas = new BitmapImage();
                atlas.BeginInit();
                atlas.CacheOption = BitmapCacheOption.OnLoad;
                atlas.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                atlas.StreamSource = stream;
                atlas.EndInit();
                atlas.Freeze();
            }

            if (atlas.PixelWidth != CellWidth * AtlasColumns || atlas.PixelHeight != CellHeight * AtlasRows)
            {
                throw new InvalidOperationException(
                    "Unexpected spritesheet size: " + atlas.PixelWidth + "x" + atlas.PixelHeight);
            }

            BitmapSource[,] result = new BitmapSource[AtlasRows, AtlasColumns];
            for (int row = 0; row < AtlasRows; row++)
            {
                for (int column = 0; column < AtlasColumns; column++)
                {
                    Int32Rect sourceRect = new Int32Rect(
                        column * CellWidth,
                        row * CellHeight,
                        CellWidth,
                        CellHeight);
                    CroppedBitmap frame = new CroppedBitmap(atlas, sourceRect);
                    frame.Freeze();
                    result[row, column] = frame;
                }
            }

            return result;
        }

        private Popup BuildPetMenu()
        {
            Popup popup = new Popup();
            popup.Child = BuildMenuContent();
            popup.AllowsTransparency = true;
            popup.StaysOpen = false;
            popup.Placement = PlacementMode.MousePoint;
            popup.PlacementTarget = this;
            popup.PopupAnimation = PopupAnimation.Fade;
            popup.Focusable = false;
            popup.HorizontalOffset = 4;
            popup.VerticalOffset = 4;
            return popup;
        }

        private Border BuildMenuContent()
        {
            scaleButtons.Clear();

            Border outer = new Border();
            outer.Width = 286;
            outer.Margin = new Thickness(12);
            outer.Padding = new Thickness(10);
            outer.CornerRadius = new CornerRadius(18);
            outer.Background = MenuBackgroundBrush;
            outer.BorderBrush = MenuBorderBrush;
            outer.BorderThickness = new Thickness(1);
            outer.SnapsToDevicePixels = true;
            outer.Effect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 5,
                Direction = 270,
                Color = Color.FromRgb(82, 55, 42),
                Opacity = 0.25
            };

            StackPanel root = new StackPanel();
            outer.Child = root;

            root.Children.Add(BuildMenuHeader());

            root.Children.Add(CreateSectionLabel("\u52A8\u4F5C"));
            UniformGrid actionGrid = new UniformGrid();
            actionGrid.Columns = 2;
            actionGrid.Margin = new Thickness(-3, 0, -3, 0);
            actionGrid.Children.Add(CreateActionButton(
                "\u2190", "\u5DE6\u79CB\u5343", delegate { Play(SwingLeft); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u2192", "\u53F3\u79CB\u5343", delegate { Play(SwingRight); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u2191", "\u8F7B\u8DF3", delegate { Play(Hop); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u25CC", "\u7728\u773C\u56DE\u5E94", delegate { Play(Wink); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u25CE", "\u6655\u7729", delegate { Play(Dizzy); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u2026", "\u7B49\u5F85", delegate { Play(Waiting); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u2328", "\u6253\u5B57", delegate { Play(Typing); }, false));
            actionGrid.Children.Add(CreateActionButton(
                "\u263A", "\u5FAE\u7B11", delegate { Play(Smile); }, false));
            root.Children.Add(actionGrid);

            root.Children.Add(CreateSectionLabel("\u7F29\u653E"));
            UniformGrid scaleGrid = new UniformGrid();
            scaleGrid.Columns = 3;
            scaleGrid.Margin = new Thickness(-3, 0, -3, 0);
            int[] scaleOptions = new int[] { 75, 100, 125, 150, 175, 200 };
            for (int index = 0; index < scaleOptions.Length; index++)
            {
                scaleGrid.Children.Add(CreateScaleButton(scaleOptions[index]));
            }
            root.Children.Add(scaleGrid);
            UpdateScaleSelection();

            root.Children.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(6, 8, 6, 6),
                Background = DividerBrush
            });

            root.Children.Add(CreateActionButton(
                "\u2198", "\u56DE\u5230\u53F3\u4E0B\u89D2",
                delegate
                {
                    MoveToDefaultPosition();
                    Play(Hop);
                },
                false));
            root.Children.Add(CreateActionButton(
                "\u00D7", "\u9000\u51FA",
                delegate { Application.Current.Shutdown(0); },
                true));

            TextBlock footer = new TextBlock();
            footer.Text = "\u5355\u51FB\u7728\u773C\u56DE\u5E94 \u00B7 \u53CC\u51FB\u8F7B\u8DF3";
            footer.FontSize = 10.5;
            footer.Foreground = MutedBrush;
            footer.HorizontalAlignment = HorizontalAlignment.Center;
            footer.Margin = new Thickness(0, 7, 0, 1);
            root.Children.Add(footer);

            return outer;
        }

        private Border BuildMenuHeader()
        {
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0, 0);
            gradient.EndPoint = new Point(1, 1);
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(250, 237, 225), 0));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 249, 242), 1));
            gradient.Freeze();

            Border header = new Border();
            header.Background = gradient;
            header.CornerRadius = new CornerRadius(14);
            header.Padding = new Thickness(10);
            header.Margin = new Thickness(0, 0, 0, 7);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Child = grid;

            Image rabbit = new Image();
            rabbit.Source = frames[0, 0];
            rabbit.Width = 46;
            rabbit.Height = 50;
            rabbit.Stretch = Stretch.Uniform;
            rabbit.VerticalAlignment = VerticalAlignment.Center;
            RenderOptions.SetBitmapScalingMode(rabbit, BitmapScalingMode.HighQuality);
            Grid.SetColumn(rabbit, 0);
            grid.Children.Add(rabbit);

            StackPanel titleStack = new StackPanel();
            titleStack.VerticalAlignment = VerticalAlignment.Center;
            TextBlock title = new TextBlock();
            title.Text = "\u4E09\u597D\u5154";
            title.FontSize = 17;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = TextBrush;
            titleStack.Children.Add(title);

            TextBlock subtitle = new TextBlock();
            subtitle.Text = "\u684C\u9762\u5BA0\u7269 \u00B7 \u53F3\u952E\u83DC\u5355";
            subtitle.FontSize = 10.5;
            subtitle.Foreground = MutedBrush;
            subtitle.Margin = new Thickness(0, 2, 0, 0);
            titleStack.Children.Add(subtitle);
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            keyboardSyncBadge = new Border();
            keyboardSyncBadge.Background = MakeBrush("#E6F3E6");
            keyboardSyncBadge.CornerRadius = new CornerRadius(9);
            keyboardSyncBadge.Padding = new Thickness(7, 4, 7, 4);
            keyboardSyncBadge.VerticalAlignment = VerticalAlignment.Center;
            keyboardSyncStatusText = new TextBlock();
            keyboardSyncStatusText.Text = "\u5168\u5C40\u540C\u6B65";
            keyboardSyncStatusText.FontSize = 9.5;
            keyboardSyncStatusText.Foreground = MakeBrush("#4F7B50");
            keyboardSyncBadge.Child = keyboardSyncStatusText;
            Grid.SetColumn(keyboardSyncBadge, 2);
            grid.Children.Add(keyboardSyncBadge);

            return header;
        }

        private TextBlock CreateSectionLabel(string text)
        {
            TextBlock label = new TextBlock();
            label.Text = text;
            label.FontSize = 10.5;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = MutedBrush;
            label.Margin = new Thickness(6, 7, 0, 3);
            return label;
        }

        private Button CreateActionButton(string glyph, string label, Action action, bool danger)
        {
            Button button = new Button();
            button.Height = 41;
            button.Margin = new Thickness(3);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Cursor = Cursors.Hand;
            button.Style = CreateActionButtonStyle(danger);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border iconBackground = new Border();
            iconBackground.Width = 26;
            iconBackground.Height = 26;
            iconBackground.CornerRadius = new CornerRadius(9);
            iconBackground.Background = danger ? DangerFillBrush : SoftFillBrush;
            iconBackground.HorizontalAlignment = HorizontalAlignment.Left;
            iconBackground.VerticalAlignment = VerticalAlignment.Center;

            TextBlock icon = new TextBlock();
            icon.Text = glyph;
            icon.FontSize = 13;
            icon.FontWeight = FontWeights.Bold;
            icon.Foreground = danger ? DangerBrush : AccentBrush;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            iconBackground.Child = icon;
            Grid.SetColumn(iconBackground, 0);
            grid.Children.Add(iconBackground);

            TextBlock labelBlock = new TextBlock();
            labelBlock.Text = label;
            labelBlock.FontSize = 12;
            labelBlock.FontWeight = FontWeights.Medium;
            labelBlock.Foreground = danger ? DangerBrush : TextBrush;
            labelBlock.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(labelBlock, 1);
            grid.Children.Add(labelBlock);

            button.Content = grid;
            button.Click += delegate
            {
                CloseContextMenu();
                action();
            };
            return button;
        }

        private RadioButton CreateScaleButton(int percent)
        {
            RadioButton button = new RadioButton();
            button.Content = percent.ToString() + "%";
            button.GroupName = "PetScale";
            button.Tag = percent;
            button.Height = 31;
            button.Margin = new Thickness(3);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.Cursor = Cursors.Hand;
            button.Style = CreateScaleButtonStyle();
            button.Click += delegate
            {
                ApplyScale((int)button.Tag);
            };

            scaleButtons[percent] = button;
            return button;
        }

        private Style CreateActionButtonStyle(bool danger)
        {
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, danger ? DangerBrush : TextBrush));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(Control.TemplateProperty,
                CreateRoundedControlTemplate(typeof(Button), new CornerRadius(11), new Thickness(8, 4, 8, 4))));

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty,
                danger ? DangerHoverBrush : HoverBrush));

            Trigger pressed = new Trigger();
            pressed.Property = ButtonBase.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(Control.BackgroundProperty,
                danger ? MakeBrush("#F2CEC7") : PressedBrush));

            style.Triggers.Add(hover);
            style.Triggers.Add(pressed);
            return style;
        }

        private Style CreateScaleButtonStyle()
        {
            Style style = new Style(typeof(RadioButton));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SoftFillBrush));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, ScaleBorderBrush));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 11d));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(Control.TemplateProperty,
                CreateRoundedControlTemplate(typeof(RadioButton), new CornerRadius(10), new Thickness(5, 3, 5, 3))));

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, ScaleHoverBrush));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, MakeBrush("#D9BDAA")));

            Trigger selected = new Trigger();
            selected.Property = ToggleButton.IsCheckedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(Control.BackgroundProperty, AccentBrush));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));

            style.Triggers.Add(hover);
            style.Triggers.Add(selected);
            return style;
        }

        private static ControlTemplate CreateRoundedControlTemplate(
            Type controlType,
            CornerRadius cornerRadius,
            Thickness padding)
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, cornerRadius);
            border.SetValue(Border.PaddingProperty, padding);
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);
            border.SetBinding(Border.BackgroundProperty, new Binding("Background")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(TextElement.ForegroundProperty, new Binding("Foreground")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetBinding(ContentPresenter.ContentProperty, new Binding("Content")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            content.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            border.AppendChild(content);

            ControlTemplate template = new ControlTemplate(controlType);
            template.VisualTree = border;
            return template;
        }

        private void ApplyScale(int percent)
        {
            if (percent == scalePercent)
            {
                UpdateScaleSelection();
                return;
            }

            double centerX = Left + Width / 2.0;
            double bottom = Top + Height;
            scalePercent = percent;
            Width = GetWindowWidth();
            Height = GetWindowHeight();
            Left = centerX - Width / 2.0;
            Top = bottom - Height;
            ClampToVirtualScreen();
            UpdateScaleSelection();
        }

        private void UpdateScaleSelection()
        {
            foreach (KeyValuePair<int, RadioButton> entry in scaleButtons)
            {
                entry.Value.IsChecked = entry.Key == scalePercent;
            }
        }

        private void CloseContextMenu()
        {
            if (petMenuPopup != null)
            {
                petMenuPopup.IsOpen = false;
            }
        }

        private void OpenPetMenu()
        {
            petMenuPopup.PlacementTarget = this;
            petMenuPopup.Placement = PlacementMode.MousePoint;
            petMenuPopup.HorizontalOffset = 4;
            petMenuPopup.VerticalOffset = 4;
            petMenuPopup.IsOpen = false;
            petMenuPopup.IsOpen = true;
        }

        private void Play(Animation animation)
        {
            currentAnimation = animation;
            currentFrame = 0;
            animationTimer.Stop();
            ShowCurrentFrame();
            animationTimer.Interval = TimeSpan.FromMilliseconds(animation.Durations[0]);
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            animationTimer.Stop();

            if (currentFrame < currentAnimation.FrameCount - 1)
            {
                currentFrame++;
                ShowCurrentFrame();
                animationTimer.Interval = TimeSpan.FromMilliseconds(
                    currentAnimation.Durations[currentFrame]);
                animationTimer.Start();
                return;
            }

            if (currentAnimation.Loop)
            {
                currentFrame = 0;
                ShowCurrentFrame();
                animationTimer.Interval = TimeSpan.FromMilliseconds(
                    currentAnimation.Durations[currentFrame]);
                animationTimer.Start();
                return;
            }

            ReturnToDefaultState();
        }

        private void ReturnToDefaultState()
        {
            DateTime now = DateTime.UtcNow;

            if (waitingUntilUtc > now)
            {
                Play(WaitingLoop);
                return;
            }

            if (smileBeforeTypingPending)
            {
                smileBeforeTypingPending = false;
                if (keyboardTypingUntilUtc > now)
                {
                    Play(TypingLoop);
                }
                else
                {
                    Play(Idle);
                }
                return;
            }

            if (keyboardTypingUntilUtc > now)
            {
                Play(TypingLoop);
            }
            else
            {
                Play(Idle);
            }
        }

        private void ShowCurrentFrame()
        {
            petImage.Source = frames[currentAnimation.Row, currentFrame];
        }

        private void PetWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
            {
                Play(Hop);
                e.Handled = true;
                return;
            }

            dragging = true;
            movedWhileDragging = false;
            dragStartCursor = GetCursorPositionInDips();
            dragStartLeft = Left;
            dragStartTop = Top;
            CaptureMouse();
            e.Handled = true;
        }

        private void PetWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point cursor = GetCursorPositionInDips();
            double deltaX = cursor.X - dragStartCursor.X;
            double deltaY = cursor.Y - dragStartCursor.Y;

            if (Math.Abs(deltaX) >= DragThreshold || Math.Abs(deltaY) >= DragThreshold)
            {
                movedWhileDragging = true;
            }

            Left = dragStartLeft + deltaX;
            Top = dragStartTop + deltaY;

            if (movedWhileDragging)
            {
                if (deltaX >= DragThreshold && currentAnimation != SwingRight)
                {
                    Play(SwingRight);
                }
                else if (deltaX <= -DragThreshold && currentAnimation != SwingLeft)
                {
                    Play(SwingLeft);
                }
            }

            e.Handled = true;
        }

        private void PetWindow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            ReleaseMouseCapture();

            ClampToVirtualScreen();
            if (!movedWhileDragging)
            {
                Play(Wink);
            }

            e.Handled = true;
        }

        private void PetWindow_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenPetMenu();
            e.Handled = true;
        }

        private void InstallKeyboardHook()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                return;
            }

            keyboardHook = SetWindowsHookEx(
                WhKeyboardLl,
                keyboardHookProc,
                GetModuleHandle(null),
                0);
            UpdateKeyboardSyncStatus();
        }

        private void UninstallKeyboardHook()
        {
            if (keyboardHook == IntPtr.Zero)
            {
                return;
            }

            UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
            UpdateKeyboardSyncStatus();
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
            {
                int virtualKey = Marshal.ReadInt32(lParam);
                bool control = IsKeyDown(VkControl);
                bool shift = IsKeyDown(VkShift);
                bool alt = IsKeyDown(VkMenu);
                bool windows = IsKeyDown(VkLWin) || IsKeyDown(VkRWin);

                if (virtualKey == VkBack || virtualKey == VkDelete)
                {
                    DispatchKeyboardAction(HandleDeleteInput);
                }
                else if (virtualKey == VkEscape && !control && !alt && !windows)
                {
                    DispatchKeyboardAction(HandleWinkInput);
                }
                else if (control && !alt && !windows && virtualKey == VkC && IsChatAppForeground())
                {
                    DispatchKeyboardAction(HandleWinkInput);
                }
                else if (control && !alt && !windows && virtualKey == VkV && IsChatAppForeground())
                {
                    DispatchKeyboardAction(HandlePasteInput);
                }
                else if (virtualKey == VkReturn && !shift && !alt && !windows && IsChatAppForeground())
                {
                    DispatchKeyboardAction(HandleSendInput);
                }
                else if (IsTypingKey(virtualKey) && !control && !alt && !windows)
                {
                    DispatchKeyboardAction(HandleTypingInput);
                }
            }

            return CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        private void DispatchKeyboardAction(Action action)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, action);
        }

        private void UpdateKeyboardSyncStatus()
        {
            if (keyboardSyncBadge == null || keyboardSyncStatusText == null)
            {
                return;
            }

            if (keyboardHook != IntPtr.Zero)
            {
                keyboardSyncBadge.Background = MakeBrush("#E6F3E6");
                keyboardSyncStatusText.Text = "\u5168\u5C40\u540C\u6B65";
                keyboardSyncStatusText.Foreground = MakeBrush("#4F7B50");
                keyboardSyncBadge.ToolTip = "\u5728 Windows \u5168\u5C40\u751F\u6548";
            }
            else
            {
                keyboardSyncBadge.Background = MakeBrush("#FCEBE7");
                keyboardSyncStatusText.Text = "\u540C\u6B65\u4E0D\u53EF\u7528";
                keyboardSyncStatusText.Foreground = DangerBrush;
                keyboardSyncBadge.ToolTip = "\u65E0\u6CD5\u5B89\u88C5\u5168\u5C40\u952E\u76D8\u76D1\u542C";
            }
        }

        private bool IsChatAppForeground()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - foregroundCheckTimeUtc).TotalMilliseconds < 250)
            {
                return foregroundIsChatApp;
            }

            foregroundCheckTimeUtc = now;
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                foregroundIsChatApp = false;
                return false;
            }

            uint processId;
            GetWindowThreadProcessId(foregroundWindow, out processId);
            if (processId == 0)
            {
                foregroundIsChatApp = false;
                return false;
            }

            try
            {
                string processName = Process.GetProcessById((int)processId).ProcessName.ToLowerInvariant();
                foregroundIsChatApp = processName == "feishu"
                    || processName == "lark"
                    || processName == "wechat"
                    || processName == "weixin"
                    || processName == "wxwork"
                    || processName == "wecom"
                    || processName == "dingtalk"
                    || processName == "codex"
                    || processName == "chatgpt"
                    || processName == "telegram"
                    || processName == "discord"
                    || processName == "slack"
                    || processName == "qq"
                    || processName == "tim";
            }
            catch
            {
                foregroundIsChatApp = false;
            }

            return foregroundIsChatApp;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsTypingKey(int virtualKey)
        {
            if (virtualKey == VkBack || virtualKey == VkDelete || virtualKey == VkProcessKey)
            {
                return true;
            }

            if ((virtualKey >= 0x30 && virtualKey <= 0x39)
                || (virtualKey >= 0x41 && virtualKey <= 0x5A)
                || (virtualKey >= 0x60 && virtualKey <= 0x6F)
                || (virtualKey >= 0xBA && virtualKey <= 0xE2))
            {
                return true;
            }

            return virtualKey == 0x20 || virtualKey == VkReturn;
        }

        private void HandleTypingInput()
        {
            DateTime now = DateTime.UtcNow;
            bool afterWaiting = waitingUntilUtc > now;
            waitingUntilUtc = DateTime.MinValue;
            keyboardTypingUntilUtc = now.AddMilliseconds(1200);
            typingSessionKeyCount++;

            if (afterWaiting)
            {
                smileBeforeTypingPending = true;
                Play(Smile);
                return;
            }

            if (smileBeforeTypingPending && currentAnimation == Smile)
            {
                return;
            }

            smileBeforeTypingPending = false;
            if (currentAnimation != TypingLoop)
            {
                Play(TypingLoop);
            }
        }

        private void HandleDeleteInput()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastDeleteUtc).TotalMilliseconds <= 800)
            {
                rapidDeleteCount++;
            }
            else
            {
                rapidDeleteCount = 1;
            }
            lastDeleteUtc = now;

            keyboardTypingUntilUtc = now.AddMilliseconds(1200);
            waitingUntilUtc = DateTime.MinValue;
            typingSessionKeyCount = 0;
            smileBeforeTypingPending = false;
            Play(Dizzy);
        }

        private void HandleSendInput()
        {
            keyboardTypingUntilUtc = DateTime.MinValue;
            waitingUntilUtc = DateTime.UtcNow.AddSeconds(8);
            typingSessionKeyCount = 0;
            rapidDeleteCount = 0;
            smileBeforeTypingPending = false;
            Play(Hop);
        }

        private void HandlePasteInput()
        {
            waitingUntilUtc = DateTime.MinValue;
            keyboardTypingUntilUtc = DateTime.UtcNow.AddMilliseconds(1500);
            typingSessionKeyCount++;
            smileBeforeTypingPending = true;
            Play(Smile);
        }

        private void HandleWinkInput()
        {
            Play(Wink);
        }

        private void KeyboardIdleTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            if (waitingUntilUtc != DateTime.MinValue && now >= waitingUntilUtc)
            {
                waitingUntilUtc = DateTime.MinValue;
                if (currentAnimation == WaitingLoop || currentAnimation == Hop)
                {
                    Play(Idle);
                }
            }

            if (keyboardTypingUntilUtc != DateTime.MinValue && now >= keyboardTypingUntilUtc)
            {
                bool longTypingSession = typingSessionKeyCount >= 8;
                keyboardTypingUntilUtc = DateTime.MinValue;
                typingSessionKeyCount = 0;
                rapidDeleteCount = 0;
                smileBeforeTypingPending = false;

                if (currentAnimation == TypingLoop || currentAnimation == Dizzy)
                {
                    if (longTypingSession)
                    {
                        Play(Smile);
                    }
                    else
                    {
                        Play(Idle);
                    }
                }
            }
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            DateTime now = DateTime.UtcNow;
            if (keyboardTypingUntilUtc <= now
                && waitingUntilUtc <= now
                && currentAnimation == Idle)
            {
                Play(Smile);
            }
        }

        private void PetWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            hoverTimer.Stop();
            hoverTimer.Start();
        }

        private void PetWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            hoverTimer.Stop();
        }

        private Point GetCursorPositionInDips()
        {
            NativePoint nativePoint;
            if (!GetCursorPos(out nativePoint))
            {
                return new Point(0, 0);
            }

            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformFromDevice.Transform(
                    new Point(nativePoint.X, nativePoint.Y));
            }

            return new Point(nativePoint.X, nativePoint.Y);
        }

        private void MoveToDefaultPosition()
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 18;
            ClampToVirtualScreen();
        }

        private void ClampToVirtualScreen()
        {
            double minLeft = SystemParameters.VirtualScreenLeft;
            double minTop = SystemParameters.VirtualScreenTop;
            double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
            double maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;

            Left = Math.Max(minLeft, Math.Min(Left, maxLeft));
            Top = Math.Max(minTop, Math.Min(Top, maxTop));
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct LowLevelKeyboardInput
        {
            public int VirtualKeyCode;
            public int ScanCode;
            public int Flags;
            public int Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);
    }
}
