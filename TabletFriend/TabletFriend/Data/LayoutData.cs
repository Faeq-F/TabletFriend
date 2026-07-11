// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.

using System.Collections.Generic;

namespace TabletFriend.Data
{
	public class LayoutData
	{
		public int LayoutWidth;

		public int? ButtonSize;
		public int? Margin;
		public string MinOpacity;
		public string MaxOpacity;

		public string App;

		public string ToggleHotkey;

		public Dictionary<string, ButtonData> Buttons;
	}
}
