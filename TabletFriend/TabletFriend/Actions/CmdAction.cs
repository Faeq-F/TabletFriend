// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace TabletFriend.Actions
{
	public class CmdAction : ButtonAction
	{
		private readonly string _cmd;
		private readonly bool _hide;
		private readonly string _workingDirectory;

		public CmdAction(string cmd)
		{
			var commandString = cmd.Trim();
			var hide = false;
			string workingDir = null;

			while (true)
			{
				var spaceIndex = commandString.IndexOf(' ');
				if (spaceIndex == -1) break;

				var firstToken = commandString.Substring(0, spaceIndex).ToLowerInvariant();
				switch (firstToken)
				{
					case "/h":
					case "/hide":
						hide = true;
						commandString = commandString.Substring(spaceIndex + 1).Trim();
						continue;

					case "/d":
					case "/dir":
						commandString = commandString.Substring(spaceIndex + 1).Trim();
						if (commandString.StartsWith("\""))
						{
							var nextQuoteIndex = commandString.IndexOf('\"', 1);
							if (nextQuoteIndex != -1)
							{
								workingDir = commandString.Substring(1, nextQuoteIndex - 1);
								commandString = commandString.Substring(nextQuoteIndex + 1).Trim();
							}
							else
							{
								workingDir = commandString.Substring(1);
								commandString = "";
							}
						}
						else
						{
							var nextSpaceIndex = commandString.IndexOf(' ');
							if (nextSpaceIndex != -1)
							{
								workingDir = commandString.Substring(0, nextSpaceIndex);
								commandString = commandString.Substring(nextSpaceIndex + 1).Trim();
							}
							else
							{
								workingDir = commandString;
								commandString = "";
							}
						}
						continue;
				}
				break;
			}

			_cmd = commandString;
			_hide = hide;
			_workingDirectory = workingDir;
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

			if (!string.IsNullOrWhiteSpace(_workingDirectory))
			{
				if (Path.IsPathRooted(_workingDirectory))
				{
					startInfo.WorkingDirectory = _workingDirectory;
				}
				else if (!string.IsNullOrWhiteSpace(AppState.CurrentLayoutPath))
				{
					var layoutDirectory = Path.GetDirectoryName(AppState.CurrentLayoutPath);
					if (Directory.Exists(layoutDirectory))
					{
						startInfo.WorkingDirectory = Path.GetFullPath(Path.Combine(layoutDirectory, _workingDirectory));
					}
				}
				else
				{
					startInfo.WorkingDirectory = Path.GetFullPath(Path.Combine(AppState.CurrentDirectory, _workingDirectory));
				}
			}
			else if (!string.IsNullOrWhiteSpace(AppState.CurrentLayoutPath))
			{
				var layoutDirectory = Path.GetDirectoryName(AppState.CurrentLayoutPath);
				if (Directory.Exists(layoutDirectory))
				{
					startInfo.WorkingDirectory = layoutDirectory;
				}
			}
			else
			{
				startInfo.WorkingDirectory = AppState.CurrentDirectory;
			}

			startInfo.FileName = "cmd.exe";
			startInfo.Arguments = "/C " + _cmd;
			process.StartInfo = startInfo;
			process.Start();
			return Task.CompletedTask;
		}
	}
}
