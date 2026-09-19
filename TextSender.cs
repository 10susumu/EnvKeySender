using System;
using System.Runtime.InteropServices;

namespace EnvKeySender
{
    public static class TextSender
    {
        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (var ch in text)
            {
                var inputs = new INPUT[2];
                inputs[0].type = 1; // INPUT_KEYBOARD
                inputs[0].ki.wScan = ch;
                inputs[0].ki.dwFlags = KEYEVENTF.UNICODE;

                inputs[1].type = 1;
                inputs[1].ki.wScan = ch;
                inputs[1].ki.dwFlags = KEYEVENTF.UNICODE | KEYEVENTF.KEYUP;

                if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == 0)
                {
                    // swallow errors silently to avoid leaking sensitive data
                }
            }
        }

        #region Native
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public char wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        static class KEYEVENTF
        {
            public const uint EXTENDEDKEY = 0x0001;
            public const uint KEYUP = 0x0002;
            public const uint UNICODE = 0x0004;
            public const uint SCANCODE = 0x0008;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        #endregion
    }
}
