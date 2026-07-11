// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Christian Liensberger.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TabletFriend
{
    /// <summary>
    /// Obtained from https://web.archive.org/web/20141017230556/http://www.liensberger.it:80/web/blog/?p=207
    /// By Christian Liensberger
    /// </summary>
    public sealed class KeyboardHook : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private class Window : NativeWindow, IDisposable
        {
            private const int WM_HOTKEY = 0x0312;

            public Window()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_HOTKEY)
                {
                    var id = m.WParam.ToInt32();
                    Debug.WriteLine($"[KeyboardHook] WM_HOTKEY received with ID {id}");
                    KeyPressed?.Invoke(this, id);
                }
            }

            public event Action<object, int> KeyPressed;

            public void Dispose()
            {
                DestroyHandle();
            }
        }

        private readonly Window _window = new Window();
        private readonly Dictionary<int, string> _idToLayout = new Dictionary<int, string>();
        private int _currentId;

        public KeyboardHook()
        {
            _window.KeyPressed += (sender, id) =>
            {
                if (_idToLayout.TryGetValue(id, out var layoutName))
                {
                    KeyPressed?.Invoke(this, layoutName);
                }
            };
        }

        public void RegisterHotKey(ModifierKeys modifier, Keys key, string layoutName)
        {
            _currentId += 1;
            Debug.WriteLine($"[KeyboardHook] Registering {modifier} + {key} for {layoutName} with ID {_currentId}");
            if (!RegisterHotKey(_window.Handle, _currentId, (uint)modifier, (uint)key))
            {
                throw new InvalidOperationException("Couldn't register the hotkey.");
            }
            _idToLayout[_currentId] = layoutName;
        }

        public void UnregisterAll()
        {
            foreach (var id in _idToLayout.Keys)
            {
                UnregisterHotKey(_window.Handle, id);
            }
            _idToLayout.Clear();
            _currentId = 0;
        }

        public event Action<object, string> KeyPressed;

        public void Dispose()
        {
            UnregisterAll();
            _window.Dispose();
        }
    }

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }
}
