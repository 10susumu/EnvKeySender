using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EnvKeySender
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_ALT = 0x12;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;

        public int MonitoredVk { get; set; }
        public bool RequiresCtrl { get; set; }
        public bool RequiresShift { get; set; }
        public bool RequiresAlt { get; set; }

        // Track key pressed state to avoid repeats
        private readonly HashSet<int> _pressed = new();

        public event EventHandler? Triggered;

        public KeyboardHook(int vk, bool ctrl = false, bool shift = false, bool alt = false)
        {
            MonitoredVk = vk;
            RequiresCtrl = ctrl;
            RequiresShift = shift;
            RequiresAlt = alt;
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                var kb = Marshal.PtrToStructure<KBLLHOOKSTRUCT>(lParam);
                int vk = kb.vkCode;

                if (vk == MonitoredVk)
                {
                    bool ctrlPressed = IsModifierPressed(VK_CONTROL);
                    bool shiftPressed = IsModifierPressed(VK_SHIFT);
                    bool altPressed = IsModifierPressed(VK_ALT);
                    bool modifiersMatch = ctrlPressed == RequiresCtrl && shiftPressed == RequiresShift && altPressed == RequiresAlt;

                    if (modifiersMatch)
                    {
                        if (msg == WM_KEYDOWN)
                        {
                            if (!_pressed.Contains(vk))
                            {
                                _pressed.Add(vk);
                                try { Triggered?.Invoke(this, EventArgs.Empty); } catch { }
                                return (IntPtr)1;
                            }
                            else
                            {
                                return (IntPtr)1;
                            }
                        }
                        else if (msg == WM_KEYUP)
                        {
                            _pressed.Remove(vk);
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsModifierPressed(int vk)
        {
            return (GetKeyState(vk) & 0x8000) != 0;
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        #region Native
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}
