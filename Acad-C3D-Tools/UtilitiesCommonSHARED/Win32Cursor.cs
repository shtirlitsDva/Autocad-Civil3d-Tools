using System;
using System.Runtime.InteropServices;

namespace IntersectUtilities.UtilsCommon
{
    public static class Win32Cursor
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        public static (int X, int Y) GetPosition()
        {
            if (!GetCursorPos(out var pt))
            {
                throw new InvalidOperationException("GetCursorPos failed.");
            }
            return (pt.X, pt.Y);
        }

        public static void SetPosition(int x, int y)
        {
            if (!SetCursorPos(x, y))
            {
                throw new InvalidOperationException("SetCursorPos failed.");
            }
        }
    }
}


