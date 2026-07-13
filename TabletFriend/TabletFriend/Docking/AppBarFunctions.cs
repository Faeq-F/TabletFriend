// Copyright (c) 2026 Faeq-F. Licensed under GPL version 3.
// Modified from original code by Martenfur, licensed under the MIT License.
// Original AppBar docking code by https://github.com/beavis28/AppBar.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WpfAppBar
{
    public enum DockingMode : int
    {
        Left = 0,
        Top,
        Right,
        Bottom,
        None
    }

    public static class AppBarFunctions
    {
        private class RegisterInfo
        {
            public int CallbackId { get; set; }
            public bool IsRegistered { get; set; }
            public Window Window { get; set; }
            public DockingMode Edge { get; set; }
            public WindowStyle OriginalStyle { get; set; }
            public Point OriginalPosition { get; set; }
            public Size OriginalSize { get; set; }
            public ResizeMode OriginalResizeMode { get; set; }


            public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam,
                                    IntPtr lParam, ref bool handled)
            {
                if (msg == CallbackId)
                {
                    if (wParam.ToInt32() == (int)Interop.ABNotify.ABN_POSCHANGED)
                    {
                        ABSetPos(Edge, Window);
                        handled = true;
                    }
                }
                return IntPtr.Zero;
            }

        }
        private static readonly Dictionary<Window, RegisterInfo> RegisteredWindowInfo
            = new Dictionary<Window, RegisterInfo>();
        private static RegisterInfo GetRegisterInfo(Window appbarWindow)
        {
            RegisterInfo reg;
            if (RegisteredWindowInfo.ContainsKey(appbarWindow))
            {
                reg = RegisteredWindowInfo[appbarWindow];
            }
            else
            {
                reg = new RegisterInfo()
                {
                    CallbackId = 0,
                    Window = appbarWindow,
                    IsRegistered = false,
                    Edge = DockingMode.Top,
                    OriginalStyle = appbarWindow.WindowStyle,
                    OriginalPosition = new Point(appbarWindow.Left, appbarWindow.Top),
                    OriginalSize =
                        new Size(appbarWindow.ActualWidth, appbarWindow.ActualHeight),
                    OriginalResizeMode = appbarWindow.ResizeMode,
                };
                RegisteredWindowInfo.Add(appbarWindow, reg);
            }
            return reg;
        }

        private static void RestoreWindow(Window appbarWindow)
        {
            var info = GetRegisterInfo(appbarWindow);

            appbarWindow.WindowStyle = info.OriginalStyle;
            appbarWindow.ResizeMode = info.OriginalResizeMode;
            //appbarWindow.Topmost = false;

            var rect = new Rect(info.OriginalPosition.X, info.OriginalPosition.Y,
                info.OriginalSize.Width, info.OriginalSize.Height);
            appbarWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new ResizeDelegate(DoResize), appbarWindow, rect);

        }

        private static DockingMode _currentEdge = DockingMode.None;

        public static void SetAppBar(Window appbarWindow, DockingMode edge, bool forceRedock = false)
        {
            if (_currentEdge == edge && !forceRedock)
            {
                return;
            }

            var info = GetRegisterInfo(appbarWindow);
            info.Edge = edge;
            _currentEdge = edge;

            var abd = new Interop.APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            abd.hWnd = new WindowInteropHelper(appbarWindow).Handle;

            if (abd.hWnd.ToInt32() == 0)
            {
                return;
            }

            int renderPolicy;

            if (edge == DockingMode.None)
            {
                if (info.IsRegistered)
                {
                    Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_REMOVE, ref abd);
                    info.IsRegistered = false;
                }
                //RestoreWindow(appbarWindow);

                // Restore normal desktop window manager attributes
                renderPolicy = (int)Interop.DWMNCRenderingPolicy.UseWindowStyle;

                Interop.DwmSetWindowAttribute(abd.hWnd, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_EXCLUDED_FROM_PEEK, ref renderPolicy, sizeof(int));
                Interop.DwmSetWindowAttribute(abd.hWnd, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_DISALLOW_PEEK, ref renderPolicy, sizeof(int));

                return;
            }

            if (!info.IsRegistered)
            {
                info.IsRegistered = true;
                info.CallbackId = Interop.RegisterWindowMessage("AppBarMessage");
                abd.uCallbackMessage = info.CallbackId;

                var ret = Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_NEW, ref abd);

                var source = HwndSource.FromHwnd(abd.hWnd);
                source.AddHook(info.WndProc);
            }

            appbarWindow.WindowStyle = WindowStyle.None;
            appbarWindow.ResizeMode = ResizeMode.NoResize;
            appbarWindow.Topmost = true;

            // Set desktop window manager attributes to prevent window
            // from being hidden when peeking at the desktop or when
            // the 'show desktop' button is pressed
            renderPolicy = (int)Interop.DWMNCRenderingPolicy.UseWindowStyle;
            //renderPolicy = (int)Interop.DWMNCRenderingPolicy.Enabled; // From vanilla code, but works worse. No idea why. :D

            Interop.DwmSetWindowAttribute(abd.hWnd, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_EXCLUDED_FROM_PEEK, ref renderPolicy, sizeof(int));
            Interop.DwmSetWindowAttribute(abd.hWnd, (int)Interop.DWMWINDOWATTRIBUTE.DWMA_DISALLOW_PEEK, ref renderPolicy, sizeof(int));

            ABSetPos(info.Edge, appbarWindow);
        }

        private delegate void ResizeDelegate(Window appbarWindow, Rect rect);
        private static void DoResize(Window appbarWindow, Rect rect)
        {
            appbarWindow.Width = rect.Width;
            appbarWindow.Height = rect.Height;
            appbarWindow.Top = rect.Top;
            appbarWindow.Left = rect.Left;

            var hwnd = new WindowInteropHelper(appbarWindow).Handle;
            var hwndTaskbar = Interop.FindWindow("Shell_TrayWnd", null);
            if (hwndTaskbar != IntPtr.Zero)
            {
                Interop.SetWindowPos(hwnd, hwndTaskbar, 0, 0, 0, 0, Interop.SWP_NOMOVE | Interop.SWP_NOSIZE | Interop.SWP_NOACTIVATE);
            }
        }



        private static Rect GetTaskbarRect()
        {
            var hwnd = Interop.FindWindow("Shell_TrayWnd", null);
            if (hwnd != IntPtr.Zero && Interop.GetWindowRect(hwnd, out var rect))
            {
                return new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            }
            return Rect.Empty;
        }

        private static Rect GetBaseBounds(System.Windows.Forms.Screen screen)
        {
            var rect = new Rect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);

            var taskbarRect = GetTaskbarRect();
            if (taskbarRect != Rect.Empty)
            {
                if (rect.IntersectsWith(taskbarRect))
                {
                    if (taskbarRect.Left == rect.Left && taskbarRect.Width < rect.Width)
                    { // Taskbar is on the left
                        rect.X += taskbarRect.Width;
                        rect.Width -= taskbarRect.Width;
                    }
                    else if (taskbarRect.Right == rect.Right && taskbarRect.Width < rect.Width)
                    { // Taskbar is on the right
                        rect.Width -= taskbarRect.Width;
                    }
                    else if (taskbarRect.Top == rect.Top && taskbarRect.Height < rect.Height)
                    { // Taskbar is at the top
                        rect.Y += taskbarRect.Height;
                        rect.Height -= taskbarRect.Height;
                    }
                    else if (taskbarRect.Bottom == rect.Bottom && taskbarRect.Height < rect.Height)
                    { // Taskbar is at the bottom
                        rect.Height -= taskbarRect.Height;
                    }
                }
            }
            return rect;
        }

        private static void ABSetPos(DockingMode edge, Window appbarWindow)
        {
            var barData = new Interop.APPBARDATA();
            barData.cbSize = Marshal.SizeOf(barData);
            barData.hWnd = new WindowInteropHelper(appbarWindow).Handle;
            barData.uEdge = (int)edge;

            var screen = System.Windows.Forms.Screen.FromHandle(barData.hWnd);
            var baseBounds = GetBaseBounds(screen);

            // Transforms a coordinate from WPF space to Screen space
            var toPixel = PresentationSource.FromVisual(appbarWindow).CompositionTarget.TransformToDevice;
            // Transforms a coordinate from Screen space to WPF space
            var toWpfUnit = PresentationSource.FromVisual(appbarWindow).CompositionTarget.TransformFromDevice;

            var mainWindow = (TabletFriend.MainWindow)appbarWindow;
            // Transform window size from wpf units (1/96 ") to real pixels, for win32 usage
            var sizeInPixels = toPixel.Transform(new Vector(mainWindow.LayoutWidth, mainWindow.LayoutHeight));

            if (barData.uEdge == (int)DockingMode.Left || barData.uEdge == (int)DockingMode.Right)
            {
                barData.rc.top = screen.Bounds.Top;
                barData.rc.bottom = screen.Bounds.Bottom;
                if (barData.uEdge == (int)DockingMode.Left)
                {
                    barData.rc.left = (int)baseBounds.Left;
                    barData.rc.right = (int)baseBounds.Left + (int)Math.Round(sizeInPixels.X);
                }
                else
                {
                    barData.rc.right = (int)baseBounds.Right;
                    barData.rc.left = (int)baseBounds.Right - (int)Math.Round(sizeInPixels.X);
                }
            }
            else
            {
                barData.rc.left = (int)baseBounds.Left;
                barData.rc.right = (int)baseBounds.Right;
                if (barData.uEdge == (int)DockingMode.Top)
                {
                    barData.rc.top = screen.Bounds.Top;
                    barData.rc.bottom = (int)baseBounds.Top + (int)Math.Round(sizeInPixels.Y);
                }
                else
                {
                    barData.rc.bottom = screen.Bounds.Bottom;
                    barData.rc.top = (int)baseBounds.Bottom - (int)Math.Round(sizeInPixels.Y);
                }
            }

            Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_QUERYPOS, ref barData);
            Interop.SHAppBarMessage((int)Interop.ABMsg.ABM_SETPOS, ref barData);

            // transform back to wpf units, for wpf window resizing in DoResize. 
            var location = toWpfUnit.Transform(new Point(barData.rc.left, barData.rc.top));
            var dimension = toWpfUnit.Transform(new Vector(barData.rc.right - barData.rc.left,
                barData.rc.bottom - barData.rc.top));

            if (edge == DockingMode.Left || edge == DockingMode.Right)
            {
                location.Y = toWpfUnit.Transform(new Point(0, screen.Bounds.Top)).Y;
                dimension.Y = toWpfUnit.Transform(new Vector(0, screen.Bounds.Height)).Y;
            }
            else if (edge == DockingMode.Bottom)
            {
                dimension.Y = toWpfUnit.Transform(new Vector(0, screen.Bounds.Bottom - barData.rc.top)).Y;
            }
            else if (edge == DockingMode.Top)
            {
                location.Y = toWpfUnit.Transform(new Point(0, screen.Bounds.Top)).Y;
                dimension.Y = toWpfUnit.Transform(new Vector(0, barData.rc.bottom - screen.Bounds.Top)).Y;
            }

            var rect = new Rect(location, new Size(dimension.X, dimension.Y));

            //This is done async, because WPF will send a resize after a new appbar is added.  
            //if we size right away, WPFs resize comes last and overrides us.
            appbarWindow.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new ResizeDelegate(DoResize), appbarWindow, rect);
        }
    }
}
