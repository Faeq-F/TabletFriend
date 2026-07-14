// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TabletFriend.Docking;
using TabletFriend.TabletMode;
using WpfAppBar;

namespace TabletFriend
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		private LayoutManager _layout;
		private ThemeManager _theme;
		private LayoutListManager _layoutList;
		private ThemeListManager _themeList;
		private AutomaticLayoutSwitcher _layoutSwitcher;
		private TrayManager _tray;
		private FileManager _file;
		private KeyboardHook _keyboardHook;

		public double LayoutWidth { get; set; }
		public double LayoutHeight { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string property)
		{
			// i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev i hate bizdev
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
		}

		public MainWindow()
		{
			var focusMonitor = new AppFocusMonitor(); // Has to be at the very top or else it hangs on starup. Why? No idea. 

			SystemEvents.DisplaySettingsChanged += OnSizeChanged;
			SizeChanged += OnWindowSizeChanged;

			Directory.SetCurrentDirectory(AppState.CurrentDirectory);

			Topmost = true;
			InitializeComponent();
			MouseDown += OnMouseDown;

			_file = new FileManager();

			ToggleManager.Init();

			_theme = new ThemeManager();
			_layout = new LayoutManager();
			Settings.Load();

			Installer.TryInstall();

			_ = UpdateChecker.Check();


			_layoutList = new LayoutListManager();
			_themeList = new ThemeListManager();
			ContextMenu = new System.Windows.Controls.ContextMenu();

			OnUpdateLayoutList();


			_layoutSwitcher = new AutomaticLayoutSwitcher(focusMonitor);
			_tray = new TrayManager(this, _layoutList, _themeList, focusMonitor);


			if (AppState.Settings.AddToAutostart)
			{
				AutostartManager.SetAutostart();
			}
			else
			{
				AutostartManager.ResetAutostart();
			}

			
			EventBeacon.Subscribe(Events.ToggleMinimize, OnToggleMinimize);
			EventBeacon.Subscribe(Events.Maximize, OnMaximize);
			EventBeacon.Subscribe(Events.Minimize, OnMinimize);
			EventBeacon.Subscribe(Events.UpdateLayoutList, OnUpdateLayoutList);
			EventBeacon.Subscribe(Events.UpdateLayoutList, UpdateHotkeys);
			EventBeacon.Subscribe(Events.ChangeLayout, OnUpdateLayoutList);
			EventBeacon.Subscribe(Events.DockingChanged, OnDockingChanged);
			EventBeacon.Subscribe(Events.LayoutChanged, OnLayoutChanged);

			_keyboardHook = new KeyboardHook();
			_keyboardHook.KeyPressed += OnHotkeyTriggered;

			UpdateHotkeys();
		}


		private void OnSizeChanged(object sender, EventArgs eventArgs)
		{
			UiFactory.CreateUi(AppState.CurrentLayout, this);
		}


		private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (AppState.CurrentLayout != null)
			{
				UiFactory.CreateUi(AppState.CurrentLayout, this);
			}
		}


		private double _maxOpacity;
		public double MaxOpacity
		{
			get => _maxOpacity;
			set
			{
				_maxOpacity = value;
				OnPropertyChanged(nameof(MaxOpacity));
			}
		}


		private double _minOpacity;
		public double MinOpacity
		{
			get => _minOpacity;
			set
			{
				_minOpacity = value;
				OnPropertyChanged(nameof(MinOpacity));
			}
		}


		private void OnUpdateLayoutList(object[] obj = null)
		{
			// Secondary quick access context menu.
			Application.Current.Dispatcher.Invoke(
				() =>
				{
					ContextMenu.Items.Clear();
					DockingMenuFactory.CreateDockingMenu(ContextMenu);

					ContextMenu.Items.Add(new Separator());
					var items = _layoutList.GetClonedItems();
					foreach (var item in items)
					{
						ContextMenu.Items.Add(item);
					}
				}
			);
		}

		private void OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (
				e.ChangedButton == MouseButton.Left
				&& AppState.Settings.DockingMode == DockingMode.None
			)
			{
				DragMove();
			}
		}

		private void OnDockingChanged(params object[] args)
		{
			var side = (DockingMode)args[0];

			if (side != DockingMode.None && side != AppState.Settings.DockingMode)
			{
				AppBarFunctions.SetAppBar(this, DockingMode.None);
			}
			
			AppState.Settings.DockingMode = side;

			UiFactory.CreateUi(AppState.CurrentLayout, this);
			
			if (Visibility == Visibility.Visible)
			{
				AppBarFunctions.SetAppBar(this, side, _layout.LastLoadResult == LayoutLoadResult.RequiresRedock);
			}

			if (side != DockingMode.None)
			{
				MinOpacity = AppState.CurrentLayout.MaxOpacity;
				MaxOpacity = AppState.CurrentLayout.MaxOpacity;
				BeginAnimation(OpacityProperty, null);
				Opacity = AppState.CurrentLayout.MaxOpacity;
			}
			else
			{
				MinOpacity = AppState.CurrentLayout.MinOpacity;
				MaxOpacity = AppState.CurrentLayout.MaxOpacity;
				BeginAnimation(OpacityProperty, null);
				Opacity = AppState.CurrentLayout.MaxOpacity;
				BeginAnimation(OpacityProperty, FadeOut);
			}


			EventBeacon.SendEvent(Events.UpdateSettings);
		}


		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// Makes it so the window doesn't hold focus.
			var helper = new WindowInteropHelper(this);
			SetWindowLong(
				helper.Handle,
				GWL_EXSTYLE,
				GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE
			);

			EventBeacon.SendEvent(Events.DockingChanged, AppState.Settings.DockingMode);
		}

		private static bool _firstToggle = true;

		private void OnToggleMinimize(object[] obj)
		{
			// Regular minimize doesn't work without the taskbar icon.
			// The window just derps out and stays at the bottom left corner.
			// There are workarounds, but they make an icon flash in the taskbar
			// for a split second. This is the best solution I found.
			if (Visibility == Visibility.Collapsed || Visibility == Visibility.Hidden)
			{
				Visibility = Visibility.Visible;
				if (!_firstToggle)
				{				
					AppBarFunctions.SetAppBar(this, AppState.Settings.DockingMode);
				}
				else
				{
					// On first launch the toolbar spergs out. This seems to fix it. Maybe. Mostly. :(
					Thread.Sleep(500);
					AppBarFunctions.SetAppBar(this, AppState.Settings.DockingMode);
					Thread.Sleep(100);
					AppBarFunctions.SetAppBar(this, DockingMode.None);
					Thread.Sleep(100);
					AppBarFunctions.SetAppBar(this, AppState.Settings.DockingMode);
					_firstToggle = false;
				}
			}
			else
			{
				AppBarFunctions.SetAppBar(this, DockingMode.None);
				Visibility = Visibility.Hidden;
			}
		}

		private void OnMinimize(object[] obj)
		{
			if (Visibility == Visibility.Visible)
			{
				AppBarFunctions.SetAppBar(this, DockingMode.None);
				Visibility = Visibility.Hidden;
			}
		}

		private void OnMaximize(object[] obj)
		{
			if (Visibility == Visibility.Collapsed || Visibility == Visibility.Hidden)
			{
				Visibility = Visibility.Visible;
				AppBarFunctions.SetAppBar(this, AppState.Settings.DockingMode);
			}
		}


		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_NOACTIVATE = 0x08000000;

		[DllImport("user32.dll")]
		public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		private void OnLayoutChanged(params object[] args)
		{
			if (args != null && args.Length > 0)
			{
				var currentDockingMode = AppState.Settings.DockingMode;
				UiFactory.CreateUi(AppState.CurrentLayout, this);
				
				if (currentDockingMode != DockingMode.None && Visibility == Visibility.Visible)
				{
					AppBarFunctions.SetAppBar(this, currentDockingMode, true);
				}
			}
		}

		private void OnHotkeyTriggered(object sender, string layoutName)
		{
			Debug.WriteLine($"[MainWindow] OnHotkeyTriggered: {layoutName}");
			if (layoutName.Equals(AppState.CurrentLayoutName, StringComparison.OrdinalIgnoreCase))
			{
				Debug.WriteLine($"[MainWindow] Toggling minimize for active layout: {layoutName}");
				EventBeacon.SendEvent(Events.ToggleMinimize);
			}
			else
			{
				Debug.WriteLine($"[MainWindow] Switching layout to: {layoutName}");
				EventBeacon.SendEvent(Events.ChangeLayout, layoutName);
				if (Visibility == Visibility.Collapsed || Visibility == Visibility.Hidden)
				{
					EventBeacon.SendEvent(Events.Maximize);
				}
			}
		}

		private void UpdateHotkeys(object[] obj = null)
		{
			Application.Current.Dispatcher.Invoke(
				delegate
				{
					_keyboardHook.UnregisterAll();
					if (AppState.Layouts == null) return;

					var registeredCombos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					var duplicates = new List<string>();

					foreach (var pair in AppState.Layouts)
					{
						var layoutName = pair.Key;
						var layout = pair.Value;

						if (string.IsNullOrWhiteSpace(layout.ToggleHotkey)) continue;

						var hotkeyStr = layout.ToggleHotkey.Trim();
						if (registeredCombos.TryGetValue(hotkeyStr, out var existingLayout))
						{
							duplicates.Add($"Hotkey '{hotkeyStr}' is defined in both '{existingLayout}' and '{layoutName}'.");
							continue;
						}

						try
						{
							if (ParseHotkey(hotkeyStr, out var modifier, out var key))
							{
								_keyboardHook.RegisterHotKey(modifier, key, layoutName);
								registeredCombos[hotkeyStr] = layoutName;
							}
						}
						catch (Exception ex)
						{
							MessageBox.Show(
								$"Failed to register hotkey '{hotkeyStr}' for layout '{layoutName}': {ex.Message}",
								"Hotkey Registration Error",
								MessageBoxButton.OK,
								MessageBoxImage.Warning
							);
						}
					}

					if (duplicates.Count > 0)
					{
						MessageBox.Show(
							"Duplicate hotkeys detected:\n\n" + string.Join("\n", duplicates) + "\n\nOnly the first registration was kept.",
							"Hotkey Conflict",
							MessageBoxButton.OK,
							MessageBoxImage.Warning
						);
					}
				}
			);
		}

		private bool ParseHotkey(string hotkeyStr, out ModifierKeys modifier, out System.Windows.Forms.Keys key)
		{
			modifier = ModifierKeys.None;
			key = System.Windows.Forms.Keys.None;

			var parts = hotkeyStr.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0) return false;

			for (int i = 0; i < parts.Length; i++)
			{
				var part = parts[i].Trim().ToLowerInvariant();
				if (part == "ctrl" || part == "control")
				{
					modifier |= ModifierKeys.Control;
				}
				else if (part == "alt" || part == "menu")
				{
					modifier |= ModifierKeys.Alt;
				}
				else if (part == "shift")
				{
					modifier |= ModifierKeys.Shift;
				}
				else if (part == "win" || part == "windows")
				{
					modifier |= ModifierKeys.Win;
				}
				else
				{
					var keyToken = TranslateKey(part);
					if (Enum.TryParse<System.Windows.Forms.Keys>(keyToken, true, out var parsedKey))
					{
						key = parsedKey;
					}
					else
					{
						throw new ArgumentException($"Unknown key code: '{parts[i].Trim()}'");
					}
				}
			}

			if (key == System.Windows.Forms.Keys.None)
			{
				throw new ArgumentException("No primary key specified in hotkey combination.");
			}

			return true;
		}

		private static string TranslateKey(string key)
		{
			switch (key.ToLowerInvariant())
			{
				case "0": return "D0";
				case "1": return "D1";
				case "2": return "D2";
				case "3": return "D3";
				case "4": return "D4";
				case "5": return "D5";
				case "6": return "D6";
				case "7": return "D7";
				case "8": return "D8";
				case "9": return "D9";
				default: return key;
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			_keyboardHook?.Dispose();
			base.OnClosed(e);
		}

		private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (AppState.Settings.DockingMode == DockingMode.Top || AppState.Settings.DockingMode == DockingMode.Bottom)
			{
				MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset - e.Delta);
				e.Handled = true;
			}
			else
			{
				MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - e.Delta);
				e.Handled = true;
			}
		}
	}
}
