using System;
using System.Windows.Forms;

namespace BoogieBoardSync
{
    internal static class ScreenUtil
    {
        public static int DisplayNumber(Screen screen)
        {
            string name = screen.DeviceName ?? "";
            int index = name.LastIndexOf("DISPLAY", StringComparison.OrdinalIgnoreCase);
            int number;
            if (index >= 0 && int.TryParse(name.Substring(index + 7), out number))
            {
                return number;
            }
            return 0;
        }

        public static string ShortLabel(Screen screen)
        {
            int number = DisplayNumber(screen);
            return number > 0 ? "显示器 " + number : "显示器";
        }

        public static string Describe(Screen screen)
        {
            string label = ShortLabel(screen) + (screen.Primary ? "（主屏·" : "（");
            bool portrait = screen.Bounds.Height > screen.Bounds.Width;
            label += portrait ? "竖屏）" : "横屏）";
            return string.Format("{0}  {1}×{2}", label, screen.Bounds.Width, screen.Bounds.Height);
        }

        public static int IndexOfDisplayNumber(int number)
        {
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (DisplayNumber(screens[i]) == number)
                {
                    return i;
                }
            }
            return -1;
        }

        public static string LabelForIndex(int index)
        {
            Screen[] screens = Screen.AllScreens;
            if (index >= 0 && index < screens.Length)
            {
                return ShortLabel(screens[index]);
            }
            return "全部屏幕";
        }

        public static int[] IndicesSortedByDisplayNumber()
        {
            Screen[] screens = Screen.AllScreens;
            int[] indices = new int[screens.Length];
            for (int i = 0; i < screens.Length; i++)
            {
                indices[i] = i;
            }
            Array.Sort(indices, delegate(int a, int b)
            {
                int numberA = DisplayNumber(screens[a]);
                int numberB = DisplayNumber(screens[b]);
                if (numberA != numberB)
                {
                    return numberA.CompareTo(numberB);
                }
                return a.CompareTo(b);
            });
            return indices;
        }
    }
}
