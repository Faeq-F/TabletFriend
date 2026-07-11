// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TabletFriend.Actions
{
	public class CmdAction : ButtonAction
	{
		private readonly string _cmd;
		private readonly bool _hide;

		public CmdAction(string cmd)
		{
			if (cmd.StartsWith("/h ", StringComparison.OrdinalIgnoreCase))
			{
				_cmd = cmd.Substring(3).Trim();
				_hide = true;
			}
			else if (cmd.StartsWith("/hide ", StringComparison.OrdinalIgnoreCase))
			{
				_cmd = cmd.Substring(6).Trim();
				_hide = true;
			}
			else
			{
				_cmd = cmd;
				_hide = false;
			}
		}

		public override Task Invoke()
		{
			var process = new Process();
			var startInfo = new ProcessStartInfo();
			if (_hide)
			{
				startInfo.CreateNoWindow = true;
				startInfo.UseShellExecute = false;
				startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			}
			startInfo.FileName = "cmd.exe";
			startInfo.Arguments = "/C " + _cmd;
			process.StartInfo = startInfo;
			process.Start();
			return Task.CompletedTask;
		}
	}
}
