using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;

namespace TwitchPlaysBot
{
    [Serializable]
    public class Joypad
    {
        /// <summary>
        /// The name of the joypad.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// A collection of commands and their associated keyboard key combinations.
        /// </summary>
        public Dictionary<string, Keys[]> CommandKeyPairs { get; set; }
        /// <summary>
        /// The delay in milliseconds between key presses when pressing an array of keys.
        /// </summary>
        public int Delay { get; set; }
        /// <summary>
        /// The delay in milliseconds between key down and key up events, to help the target register input.
        /// </summary>
        private const int KeyDownUpDelay = 50;

        /// <summary>
        /// Create a new Joypad with no command to key mappings.
        /// </summary>
        public Joypad()
        {
            Name = "";
            // default to a delay of half a second
            Delay = 500;
            // initialise with a case-insensitive comparer to ensure commands get matched
            CommandKeyPairs = new Dictionary<string, Keys[]>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Create a new Joypad with a collection of command to key mappings.
        /// Uses the WinForms key enumeration type.
        /// </summary>
        /// <param name="commandKeys">Maps string commands to an array of WinForms key enumerations.</param>
        public Joypad(Dictionary<string, Keys[]> commandKeys) : this()
        {
            foreach (KeyValuePair<string, Keys[]> command in commandKeys)
            {
                CommandKeyPairs.Add(command.Key, command.Value);
            }
        }

        /// <summary>
        /// Create a new Joypad with a collection of command to key mappings,
        /// converting the WPF key enumeration type to WinForms.
        /// </summary>
        /// <param name="commandKeys">Maps string commands to an array of WPF key enumerations.</param>
        public Joypad(Dictionary<string, Key[]> commandKeys) : this()
        {
            foreach (KeyValuePair<string, Key[]> command in commandKeys)
            {
                Keys[] keys = new Keys[command.Value.Length];

                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = (Keys)KeyInterop.VirtualKeyFromKey(command.Value[i]);
                }

                CommandKeyPairs.Add(command.Key, keys);
            }
        }

        /// <summary>
        /// Creates a new Joypad with a collection of command to key mappings.
        /// Parses strings representing keys into WinForms key enumerations.
        /// </summary>
        /// <param name="commandStrings">Maps string commands to an array of strings representing keys.</param>
        public Joypad(Dictionary<string, string[]> commandStrings) : this()
        {
            foreach (KeyValuePair<string, string[]> command in commandStrings)
            {
                List<Keys> keys = new List<Keys>();

                foreach (string key in command.Value)
                {
                    Keys parsedKey;
                    if (TryParseEnum<Keys>(key, out parsedKey))
                    {
                        keys.Add(parsedKey);
                    }
                }

                CommandKeyPairs.Add(command.Key, keys.ToArray());
            }
        }

        /// <summary>
        /// Attempts to parse a string into an equivalent enumeration using generics.
        /// </summary>
        /// <typeparam name="T">The type of enumeration.</typeparam>
        /// <param name="value">The value of the enumeration.</param>
        /// <param name="result">The enumeration that has been parsed.</param>
        /// <returns>true if the parse was successful, false otherwise.</returns>
        public static bool TryParseEnum<T>(string value, out T result)
        {
            result = default(T);

            if (!string.IsNullOrEmpty(value))
            {
                Type type = typeof(T);

                if (type.IsEnum)
                {
                    try
                    {
                        result = (T)Enum.Parse(type, value);
                        return true;
                    }
                    catch (Exception)
                    {
                        // thrown because there wasn't an equivalent enum
                    }
                }
            }

            return false;
        }

        public void PressKey(Keys key)
        {
            Keyboard.KeyPress(key, KeyDownUpDelay);
        }

        public void PressKey(Key key)
        {
            Keys wpfToVirtualKey = (Keys)KeyInterop.VirtualKeyFromKey(key);
            PressKey(wpfToVirtualKey);
        }

        public void PressKey(string key)
        {
            Keys stringToKey;
            if (TryParseEnum<Keys>(key, out stringToKey))
            {
                PressKey(stringToKey);
            }
        }

        public void PressManyKeys(Keys[] keys)
        {
            foreach (Keys key in keys)
            {
                PressKey(key);
                Thread.Sleep(Delay);
            }
        }

        public void PressManyKeys(Key[] keys)
        {
            foreach (Key key in keys)
            {
                PressKey(key);
                Thread.Sleep(Delay);
            }
        }

        public void PressManyKeys(string[] keys)
        {
            foreach (string key in keys)
            {
                PressKey(key);
                Thread.Sleep(Delay);
            }
        }

        /// <summary>
        /// Inner class that provides methods to send keyboard input that also works in DirectX games.
        /// </summary>
        /// <author>http://www.codeproject.com/Articles/117657/InputManager-library-Track-user-input-and-simulate?msg=4004697#xx4004697xx</author>
        private class Keyboard
        {
            #region API Declaring
            #region SendInput
            [DllImport("user32.dll", CharSet = CharSet.Auto,
                    CallingConvention = CallingConvention.StdCall, SetLastError = true)]
            private static extern int SendInput(
                int cInputs,
                ref INPUT pInputs,
                int cbSize);

            private struct INPUT
            {
                public uint dwType;
                public MOUSEKEYBDHARDWAREINPUT mkhi;
            }

            private struct KEYBDINPUT
            {
                public short wVk;
                public short wScan;
                public uint dwFlags;
                public int time;
                public IntPtr dwExtraInfo;
            }

            private struct HARDWAREINPUT
            {
                public int uMsg;
                public short wParamL;
                public short wParamH;
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct MOUSEKEYBDHARDWAREINPUT
            {
                [FieldOffset(0)]
                public MOUSEINPUT mi;
                [FieldOffset(0)]
                public KEYBDINPUT ki;
                [FieldOffset(0)]
                public HARDWAREINPUT hi;
            }

            private struct MOUSEINPUT
            {
                public int dx;
                public int dy;
                public int mouseData;
                public uint dwFlags;
                public int time;
                public IntPtr dwExtraInfo;
            }

            const UInt32 INPUT_MOUSE = 0;
            const int INPUT_KEYBOARD = 1;
            const int INPUT_HARDWARE = 2;
            const UInt32 KEYEVENTF_EXTENDEDKEY = 0x01;
            const UInt32 KEYEVENTF_KEYUP = 0x02;
            const UInt32 KEYEVENTF_UNICODE = 0x04;
            const UInt32 KEYEVENTF_SCANCODE = 0x08;
            const UInt32 XBUTTON1 = 0x01;
            const UInt32 XBUTTON2 = 0x02;
            const UInt32 MOUSEEVENTF_MOVE = 0x01;
            const UInt32 MOUSEEVENTF_LEFTDOWN = 0x02;
            const UInt32 MOUSEEVENTF_LEFTUP = 0x04;
            const UInt32 MOUSEEVENTF_RIGHTDOWN = 0x08;
            const UInt32 MOUSEEVENTF_RIGHTUP = 0x10;
            const UInt32 MOUSEEVENTF_MIDDLEDOWN = 0x20;
            const UInt32 MOUSEEVENTF_MIDDLEUP = 0x40;
            const UInt32 MOUSEEVENTF_XDOWN = 0x80;
            const UInt32 MOUSEEVENTF_XUP = 0x100;
            const UInt32 MOUSEEVENTF_WHEEL = 0x800;
            const UInt32 MOUSEEVENTF_VIRTUALDESK = 0x4000;
            const UInt32 MOUSEEVENTF_ABSOLUTE = 0x8000;
            #endregion SendInput

            [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall, SetLastError = true)]
            private static extern UInt32 MapVirtualKey(UInt32 uCode, MapVirtualKeyMapTypes uMapType);

            [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall, SetLastError = true)]
            private static extern UInt32 MapVirtualKeyEx(UInt32 uCode, MapVirtualKeyMapTypes uMapType, IntPtr dwhkl);

            [DllImport("user32.dll", CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall, SetLastError = true)]
            private static extern IntPtr GetKeyboardLayout(uint idThread);

            /// <summary>The set of valid MapTypes used in MapVirtualKey
            /// </summary>
            /// <remarks></remarks>
            public enum MapVirtualKeyMapTypes : uint
            {
                /// <summary>uCode is a virtual-key code and is translated into a scan code.
                /// If it is a virtual-key code that does not distinguish between left- and
                /// right-hand keys, the left-hand scan code is returned.
                /// If there is no translation, the function returns 0.
                /// </summary>
                /// <remarks></remarks>
                MAPVK_VK_TO_VSC = 0x00,

                /// <summary>uCode is a scan code and is translated into a virtual-key code that
                /// does not distinguish between left- and right-hand keys. If there is no
                /// translation, the function returns 0.
                /// </summary>
                /// <remarks></remarks>
                MAPVK_VSC_TO_VK = 0x01,

                /// <summary>uCode is a virtual-key code and is translated into an unshifted
                /// character value in the low-order word of the return value. Dead keys (diacritics)
                /// are indicated by setting the top bit of the return value. If there is no
                /// translation, the function returns 0.
                /// </summary>
                /// <remarks></remarks>
                MAPVK_VK_TO_CHAR = 0x02,

                /// <summary>Windows NT/2000/XP: uCode is a scan code and is translated into a
                /// virtual-key code that distinguishes between left- and right-hand keys. If
                /// there is no translation, the function returns 0.
                /// </summary>
                /// <remarks></remarks>
                MAPVK_VSC_TO_VK_EX = 0x03,

                /// <summary>Not currently documented
                /// </summary>
                /// <remarks></remarks>
                MAPVK_VK_TO_VSC_EX = 0x04,
            } ///Enum
            #endregion API Declaring

            private static ScanKey GetScanKey(Keys VKey)
            {
                uint ScanCode = MapVirtualKey((uint)VKey, MapVirtualKeyMapTypes.MAPVK_VK_TO_VSC);
                bool Extended = (VKey == Keys.RMenu) || (VKey == Keys.RControlKey) || (VKey == Keys.Left) || (VKey == Keys.Right) || (VKey == Keys.Up) || (VKey == Keys.Down) || (VKey == Keys.Home) || (VKey == Keys.Delete) || (VKey == Keys.PageUp) || (VKey == Keys.PageDown) || (VKey == Keys.End) || (VKey == Keys.Insert) || (VKey == Keys.NumLock) || (VKey == Keys.PrintScreen) || (VKey == Keys.Divide);
                return new ScanKey(ScanCode, Extended);
            }

            private struct ScanKey
            {
                public uint ScanCode;
                public bool Extended;
                public ScanKey(uint sCode, Boolean ex/* = false*/)
                {
                    ScanCode = sCode;
                    Extended = ex;
                }
            }

            /// <summary>
            /// Sends shortcut keys (key down and up) signals.
            /// </summary>
            /// <param name="kCode">The array of keys to send as a shortcut.</param>
            /// <param name="Delay">The delay in milliseconds between the key down and up events.</param>
            /// <remarks></remarks>
            public static void ShortcutKeys(Keys[] kCode, int Delay /*= 0*/)
            {
                KeyPressStruct KeysPress = new KeyPressStruct(kCode, Delay);
                Thread t = new Thread(new ParameterizedThreadStart(KeyPressThread));
                t.Start(KeysPress);
            }

            /// <summary>
            /// Sends a key down signal.
            /// </summary>
            /// <param name="kCode">The virtual keycode to send.</param>
            /// <remarks></remarks>
            public static void KeyDown(Keys kCode)
            {
                ScanKey sKey = GetScanKey(kCode);
                INPUT input = new INPUT();
                input.dwType = INPUT_KEYBOARD;
                input.mkhi.ki = new KEYBDINPUT();
                input.mkhi.ki.wScan = (short)sKey.ScanCode;
                input.mkhi.ki.dwExtraInfo = IntPtr.Zero;
                input.mkhi.ki.dwFlags = KEYEVENTF_SCANCODE | (sKey.Extended ? KEYEVENTF_EXTENDEDKEY : 0);
                int cbSize = Marshal.SizeOf(typeof(INPUT));
                SendInput(1, ref input, cbSize);
            }

            /// <summary>
            /// Sends a key up signal.
            /// </summary>
            /// <param name="kCode">The virtual keycode to send.</param>
            /// <remarks></remarks>
            public static void KeyUp(Keys kCode)
            {
                ScanKey sKey = GetScanKey(kCode);
                INPUT input = new INPUT();
                input.dwType = INPUT_KEYBOARD;
                input.mkhi.ki = new KEYBDINPUT();
                input.mkhi.ki.wScan = (short)sKey.ScanCode;
                input.mkhi.ki.dwExtraInfo = IntPtr.Zero;
                input.mkhi.ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP | (sKey.Extended ? KEYEVENTF_EXTENDEDKEY : 0);
                int cbSize = Marshal.SizeOf(typeof(INPUT));
                SendInput(1, ref input, cbSize);
            }

            /// <summary>
            /// Sends a key press signal (key down and up).
            /// </summary>
            /// <param name="kCode">The virtual keycode to send.</param>
            /// <param name="Delay">The delay to set between the key down and up commands.</param>
            /// <remarks></remarks>
            public static void KeyPress(Keys kCode, int Delay /*= 0*/)
            {
                Keys[] SendKeys = new Keys[] { kCode };
                KeyPressStruct KeysPress = new KeyPressStruct(SendKeys, Delay);
                Thread t = new Thread(new ParameterizedThreadStart(KeyPressThread));
                t.Start(KeysPress);
            }

            private static void KeyPressThread(object obj)
            {
                KeyPressStruct KeysP = (KeyPressStruct)obj;
                foreach (Keys k in KeysP.Keys)
                {
                    KeyDown(k);
                }
                if (KeysP.Delay > 0) { Thread.Sleep(KeysP.Delay); }
                foreach (Keys k in KeysP.Keys)
                {
                    KeyUp(k);
                }
            }

            private struct KeyPressStruct
            {
                public Keys[] Keys;
                public int Delay;
                public KeyPressStruct(Keys[] KeysToPress, int DelayTime /*= 0*/)
                {
                    Keys = KeysToPress;
                    Delay = DelayTime;
                }
            }
        }
    }
}
