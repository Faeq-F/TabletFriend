// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Printing;
using System.Windows;
using TabletFriend.Actions;
using TabletFriend.Models;
using WindowsInput.Events;
using WpfAppBar;

namespace TabletFriend
{
	public class LayoutManager
	{
		private static LayoutModel _fallbackLayout = new LayoutModel()
		{
			Buttons = {
				new ButtonModel()
				{
					Text = "ERROR",
					Action = new KeyAction(KeyCode.N),
					Size = new Vector2(4, 2)
				}
			}
		};


		public LayoutLoadResult LastLoadResult;

		public LayoutManager()
		{
			EventBeacon.Subscribe(Events.FilesChanged, OnFilesChanged);
			EventBeacon.Subscribe(Events.ChangeLayout, OnChangeLayout);
		}


		private void OnFilesChanged(object[] args)
		{
			Application.Current.Dispatcher.Invoke(
				delegate
				{
					// Full reload every time.
					LoadLayout(AppState.CurrentLayoutName);
					LastLoadResult = LayoutLoadResult.RequiresRedock;
				}
			);
		}

		private void OnChangeLayout(object[] obj)
		{
			var firstLoad = AppState.CurrentLayout == null;
			var path = (string)obj[0];

			var isManual = true;
			if (obj.Length > 1)
			{
				var method = (LayoutChangeMethod)obj[1];
				if (method == LayoutChangeMethod.Automatic)
				{
					isManual = false;
				}
			}

			if (isManual)
			{
				AppState.LastManuallySetLayout = path;
			}

			LoadLayout(path);
			if (firstLoad)
			{
				LastLoadResult = LayoutLoadResult.RequiresRedock;
			}

			if (!firstLoad)
			{
				EventBeacon.SendEvent(Events.UpdateSettings);
			}
		}


		public void LoadLayout(string path)
		{
			Debug.WriteLine("Loading " + path);

			LastLoadResult = LayoutLoadResult.Default;

			if (AppState.Layouts.Count == 0)
			{
				MessageBox.Show(
					"No layouts found!",
					"Load failure!",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
				return;
			}
			if (!AppState.Layouts.TryGetValue(path, out var layout)) // TODO: fix the check lul.
			{
				if (AppState.Layouts.ContainsKey("default"))
				{
					MessageBox.Show(
						"Cannot load '" + path +"'! Trying to fall back to default layout.",
						"Load failure!",
						MessageBoxButton.OK,
						MessageBoxImage.Error
					);
					layout = AppState.Layouts["default"];
				}
				else
				{
					MessageBox.Show(
						"No default layout found! Make sure you have a valid layout named 'default.yaml'",
						"Man you really screwed up",
						MessageBoxButton.OK,
						MessageBoxImage.Error
					);
					// Nothing to fall back on, we're fucked.
					layout = _fallbackLayout;
				}
			}

			if (AppState.CurrentLayout != null)
			{
				AppState.CurrentLayout.Dispose();
			}

			if (layout == null)// || layout == AppState.CurrentLayout)
			{
				return;
			}

			if (AppState.CurrentLayout != null && !AppState.CurrentLayout.IsSameWidth(layout))
			{
				LastLoadResult = LayoutLoadResult.RequiresRedock;
			}
			AppState.CurrentLayout = layout;
			//UiFactory.CreateUi(AppState.CurrentLayout, _window);
			AppState.CurrentLayoutName = Path.GetFileNameWithoutExtension(path);
			AppState.CurrentLayoutPath = Path.Combine(AppState.LayoutsRoot, AppState.CurrentLayoutName + ".yaml");
			EventBeacon.SendEvent(Events.DockingChanged, AppState.Settings.DockingMode);
		}

		public static void UpdateClickActionCoordinatesInCurrentLayoutFile(
			string keyNameInLayoutFile,
			System.Drawing.Point newCoordinates)
		{
			if (AppState.CurrentLayout != null)
			{
				var layout = AppState.CurrentLayoutPath;
				var currentLayoutFileContent = File.ReadAllText(layout);
				var pattern = @$"({keyNameInLayoutFile}:.*?action:.*?click)\s+(?<x_coordinate>\d+)\s*,\s*(?<y_coordinate>\d+)";
				if (Regex.IsMatch(currentLayoutFileContent, pattern, RegexOptions.Singleline))
				{
					var updatedLayoutFileContent = Regex.Replace(
						currentLayoutFileContent,
						pattern,
						@$"$1 {newCoordinates.X},{newCoordinates.Y}",
						RegexOptions.Singleline);
					File.WriteAllText(layout, updatedLayoutFileContent);
				}
			}
		}

	}

	public enum LayoutLoadResult
	{
		Default,
		RequiresRedock,
	}
}
