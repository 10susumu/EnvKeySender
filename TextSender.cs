using System;
using System.Runtime.InteropServices;

namespace EnvKeySender
{
    public static class TextSender
    {
        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                ErrorLogger.Write("TextSender: empty text requested.");
                return;
            }

            var inputSize = Marshal.SizeOf<INPUT>();
            var keybdInputSize = Marshal.SizeOf<KEYBDINPUT>();
            ErrorLogger.Write($"TextSender: text length={text.Length}, IntPtr.Size={IntPtr.Size}, Marshal.SizeOf<INPUT>={inputSize}, Marshal.SizeOf<KEYBDINPUT>={keybdInputSize}");

            for (int i = 0; i < text.Length; i++)
            {
                var inputs = new INPUT[2];
                inputs[0].type = (uint)InputType.Keyboard;
                inputs[0].union.ki.wVk = 0;
                inputs[0].union.ki.wScan = (ushort)text[i];
                inputs[0].union.ki.dwFlags = KEYEVENTF.UNICODE;

                inputs[1].type = (uint)InputType.Keyboard;
                inputs[1].union.ki.wVk = 0;
                inputs[1].union.ki.wScan = (ushort)text[i];
                inputs[1].union.ki.dwFlags = KEYEVENTF.UNICODE | KEYEVENTF.KEYUP;

                ErrorLogger.Write($"TextSender: SendInput call index={i}, nInputs={inputs.Length}, cbSize={inputSize}");

                uint result = SendInput((uint)inputs.Length, inputs, inputSize);
                int lastError = Marshal.GetLastWin32Error();
                if (result != inputs.Length)
                {
                    ErrorLogger.Write($"TextSender: SendInput failed index={i} result={result} lastError={lastError}");
                }
                else
                {
                    ErrorLogger.Write($"TextSender: SendInput succeeded index={i}");
                }
            }
        }

        #region Native
        // Native Windows definition on x64:
        // typedef struct tagINPUT {
        //   DWORD type;
        //   union {
        //     MOUSEINPUT mi;
        //     KEYBDINPUT ki;
        //     HARDWAREINPUT hi;
        //   } DUMMYUNIONNAME;
        // } INPUT;
        //
        // With x64 alignment, MOUSEINPUT is 32 bytes, KEYBDINPUT is 24 bytes, and the union is 32 bytes.
        // The native struct layout is therefore: 4-byte type + 8-byte alignment padding + 32-byte union = 40 bytes.
        // This is the correct native layout for x64 Windows.

        [StructLayout(LayoutKind.Explicit)]
        struct INPUT
        {
            [FieldOffset(0)]
            public uint type;

            [FieldOffset(8)]
            public InputUnion union;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        enum InputType : uint
        {
            Mouse = 0,
            Keyboard = 1,
            Hardware = 2
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
