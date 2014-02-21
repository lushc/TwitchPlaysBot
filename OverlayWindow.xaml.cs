using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TwitchPlaysBot
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private Process process;
        private IntPtr processHandle;
        private Queue<Keys[]> keyPressQueue;
        private Thread keyPressQueueThread;

        private string gameTitle;
        public string GameTitle
        {
            get { return this.gameTitle; }
            set
            {
                this.gameTitle = "Twitch Plays \n" + value;
                GameTitleBlock.Text = this.gameTitle;
            }
        }
        public int ProcessId { get; set; }
        public bool RemoveWindowsStyle { get; set; }
        public bool CloseProcessOnExit { get; set; }
        public Joypad Joypad { get; set; }

        // constants to remove styling
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;

        public OverlayWindow()
        {
            this.DataContext = this;
            InitializeComponent();

            keyPressQueue = new Queue<Keys[]>();
            Joypad = new Joypad();
            GameTitle = "";
        }

        public void Hook()
        {
            try
            {
                process = Process.GetProcessById(ProcessId);

                if (process != null)
                {
                    // wait for the process to become available before getting the window handle
                    process.WaitForInputIdle();
                    processHandle = process.MainWindowHandle;

                    // close the overlay if the process exits
                    process.Exited += delegate { Dispatcher.BeginInvoke(new Action(() => { this.Close(); })); };

                    System.Diagnostics.Debug.WriteLine("Hooked process ID: " + process.Id);
                    System.Diagnostics.Debug.WriteLine("Hooked process Handle: " + processHandle);

                    // focus process
                    FocusProcessWindow();

                    if (RemoveWindowsStyle)
                    {
                        // remove caption and borders
                        int style = GetWindowLong(processHandle, GWL_STYLE);
                        style = style & ~WS_CAPTION & ~WS_THICKFRAME;
                        SetWindowLong(processHandle, GWL_STYLE, style);
                    }

                    // position the overlay window to the right of the application
                    Rect processDimensions = new Rect();
                    GetWindowRect(processHandle, ref processDimensions);
                    if (processDimensions.Top > 0 && processDimensions.Right > 0)
                    {
                        this.Top = processDimensions.Top;
                        this.Left = processDimensions.Right;
                    }

                    // start working through the command queue
                    keyPressQueueThread = new Thread(() =>
                    {
                        while (!process.HasExited)
                        {
                            SendKeyPress();
                        }
                    }) { IsBackground = true };
                    keyPressQueueThread.Start();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error when hooking process: " + e.Message, e.InnerException);
            }
        }

        public void OnMessagedRecieved(string username, string message)
        {
            message = message.ToLower();

            Keys[] keys;
            if (Joypad.CommandKeyPairs.TryGetValue(message, out keys))
            {
                keyPressQueue.Enqueue(keys);
                PrintToActionLog(String.Format("{0}: {1}", username, message));
            }
        }

        private void SendKeyPress()
        {
            if (keyPressQueue.Count > 0)
            {
                if (!ProcessHasFocus())
                {
                    FocusProcessWindow();
                }

                Keys[] keys = keyPressQueue.Dequeue();
                Joypad.PressManyKeys(keys);

                System.Diagnostics.Debug.WriteLine("Using joypad layout: " + Joypad.Name);
                System.Diagnostics.Debug.WriteLine("Key press: " + String.Join(" ", keys.Select(k=>k.ToString()).ToArray()));
            }
        }

        private void FocusProcessWindow()
        {
            // show windows that have been hidden
            ShowWindow(processHandle, WindowShowStyle.Show);
            // show windows that have been minimized
            ShowWindow(processHandle, WindowShowStyle.Restore);
            // finally focus the window
            SetForegroundWindow(processHandle);
        }

        private bool ProcessHasFocus()
        {
            if (process == null)
            {
                // process has not been created
                return false;
            }

            // handle for the window that currently has focus
            var activatedHandle = GetForegroundWindow();

            if (activatedHandle == IntPtr.Zero)
            {
                // no window is currently activated
                return false;
            }

            // get the window's process ID
            int activeProcessId;
            GetWindowThreadProcessId(activatedHandle, out activeProcessId);

            // compare it to the embedded process' ID
            return activeProcessId == process.Id;
        }

        private void PrintToActionLog(string line)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // add a new line
                if (ActionLog.Text.Length > 0)
                {
                    ActionLog.Text += System.Environment.NewLine;
                }

                // append to the textbox
                ActionLog.Text += line;

                // remove overflowing lines
                while (ActionLog.LineCount > 15)
                {
                    ActionLog.Text = ActionLog.Text.Remove(0, ActionLog.GetLineLength(0));
                }

                // scroll down to the end
                ActionLog.SelectionStart = ActionLog.Text.Length;
                ActionLog.ScrollToEnd();

                // write to debug console also
                System.Diagnostics.Debug.WriteLine(line);
            }));
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (keyPressQueueThread != null)
            {
                // stop processing the queue
                keyPressQueueThread.Abort();
            }

            if (CloseProcessOnExit && process != null)
            {
                // close the process
                process.CloseMainWindow();
                process.Close();
            }

            base.OnClosing(e);
        }

        [DllImport("user32")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32")]
        private static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, WindowShowStyle nCmdShow);
        /// <summary>Enumeration of the different ways of showing a window using
        /// ShowWindow</summary>
        private enum WindowShowStyle : uint
        {
            /// <summary>Hides the window and activates another window.</summary>
            /// <remarks>See SW_HIDE</remarks>
            Hide = 0,
            /// <summary>Activates and displays a window. If the window is minimized
            /// or maximized, the system restores it to its original size and
            /// position. An application should specify this flag when displaying
            /// the window for the first time.</summary>
            /// <remarks>See SW_SHOWNORMAL</remarks>
            ShowNormal = 1,
            /// <summary>Activates the window and displays it as a minimized window.</summary>
            /// <remarks>See SW_SHOWMINIMIZED</remarks>
            ShowMinimized = 2,
            /// <summary>Activates the window and displays it as a maximized window.</summary>
            /// <remarks>See SW_SHOWMAXIMIZED</remarks>
            ShowMaximized = 3,
            /// <summary>Maximizes the specified window.</summary>
            /// <remarks>See SW_MAXIMIZE</remarks>
            Maximize = 3,
            /// <summary>Displays a window in its most recent size and position.
            /// This value is similar to "ShowNormal", except the window is not
            /// actived.</summary>
            /// <remarks>See SW_SHOWNOACTIVATE</remarks>
            ShowNormalNoActivate = 4,
            /// <summary>Activates the window and displays it in its current size
            /// and position.</summary>
            /// <remarks>See SW_SHOW</remarks>
            Show = 5,
            /// <summary>Minimizes the specified window and activates the next
            /// top-level window in the Z order.</summary>
            /// <remarks>See SW_MINIMIZE</remarks>
            Minimize = 6,
            /// <summary>Displays the window as a minimized window. This value is
            /// similar to "ShowMinimized", except the window is not activated.</summary>
            /// <remarks>See SW_SHOWMINNOACTIVE</remarks>
            ShowMinNoActivate = 7,
            /// <summary>Displays the window in its current size and position. This
            /// value is similar to "Show", except the window is not activated.</summary>
            /// <remarks>See SW_SHOWNA</remarks>
            ShowNoActivate = 8,
            /// <summary>Activates and displays the window. If the window is
            /// minimized or maximized, the system restores it to its original size
            /// and position. An application should specify this flag when restoring
            /// a minimized window.</summary>
            /// <remarks>See SW_RESTORE</remarks>
            Restore = 9,
            /// <summary>Sets the show state based on the SW_ value specified in the
            /// STARTUPINFO structure passed to the CreateProcess function by the
            /// program that started the application.</summary>
            /// <remarks>See SW_SHOWDEFAULT</remarks>
            ShowDefault = 10,
            /// <summary>Windows 2000/XP: Minimizes a window, even if the thread
            /// that owns the window is hung. This flag should only be used when
            /// minimizing windows from a different thread.</summary>
            /// <remarks>See SW_FORCEMINIMIZE</remarks>
            ForceMinimized = 11
        }

        [DllImport("user32")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref Rect rect);
        private struct Rect
        {
            // the x-coordinate of the upper-left corner of the rectangle
            public int Left { get; set; }
            // the y-coordinate of the upper-left corner of the rectangle
            public int Top { get; set; }
            // the x-coordinate of the lower-right corner of the rectangle
            public int Right { get; set; }
            // the y-coordinate of the lower-right corner of the rectangle
            public int Bottom { get; set; }
        }

        [DllImport("user32")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32")]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    }
}
