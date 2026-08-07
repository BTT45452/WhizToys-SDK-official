using System;
using System.Drawing;

namespace Plugins.WhizToys
{
    public class ColorTable
    {
        public int FindClosestColorIndex(Color targetColor)
        {
            int closestIndex = -1;
            double closestDistance = double.MaxValue;

            for (int i = 0; i < _colors.Length; i++)
            {
                Color testColor = _colors[i];

                double distance = Math.Sqrt(
                    Math.Pow(targetColor.R - testColor.R, 2) +
                    Math.Pow(targetColor.G - testColor.G, 2) +
                    Math.Pow(targetColor.B - testColor.B, 2));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private Color[] _colors =
        {
            // 0
            Color.FromArgb(0, 0, 0),
            // 1 ~ 10
            Color.FromArgb(255, 0, 0),
            Color.FromArgb(255, 25, 0),
            Color.FromArgb(255, 50, 0),
            Color.FromArgb(255, 75, 0),
            Color.FromArgb(255, 100, 0),
            Color.FromArgb(255, 125, 0),
            Color.FromArgb(255, 150, 0),
            Color.FromArgb(255, 175, 0),
            Color.FromArgb(255, 200, 0),
            Color.FromArgb(255, 225, 0),
            // 11 ~ 20
            Color.FromArgb(255, 255, 0),
            Color.FromArgb(225, 255, 0),
            Color.FromArgb(200, 255, 0),
            Color.FromArgb(175, 255, 0),
            Color.FromArgb(150, 255, 0),
            Color.FromArgb(125, 255, 0),
            Color.FromArgb(100, 255, 0),
            Color.FromArgb(75, 255, 0),
            Color.FromArgb(50, 255, 0),
            Color.FromArgb(25, 255, 0),
            // 21 ~ 30
            Color.FromArgb(0, 255, 0),
            Color.FromArgb(0, 255, 25),
            Color.FromArgb(0, 255, 50),
            Color.FromArgb(0, 255, 75),
            Color.FromArgb(0, 255, 100),
            Color.FromArgb(0, 255, 125),
            Color.FromArgb(0, 255, 150),
            Color.FromArgb(0, 255, 175),
            Color.FromArgb(0, 255, 200),
            Color.FromArgb(0, 255, 225),
            // 31 ~ 40
            Color.FromArgb(0, 255, 255),
            Color.FromArgb(0, 225, 255),
            Color.FromArgb(0, 200, 255),
            Color.FromArgb(0, 175, 255),
            Color.FromArgb(0, 150, 255),
            Color.FromArgb(0, 125, 255),
            Color.FromArgb(0, 100, 255),
            Color.FromArgb(0, 75, 255),
            Color.FromArgb(0, 50, 255),
            Color.FromArgb(0, 25, 255),
            // 41 ~ 50
            Color.FromArgb(0, 0, 255),
            Color.FromArgb(25, 0, 255),
            Color.FromArgb(50, 0, 255),
            Color.FromArgb(75, 0, 255),
            Color.FromArgb(100, 0, 255),
            Color.FromArgb(125, 0, 255),
            Color.FromArgb(150, 0, 255),
            Color.FromArgb(175, 0, 255),
            Color.FromArgb(200, 0, 255),
            Color.FromArgb(225, 0, 255),
            // 51 ~ 60
            Color.FromArgb(255, 0, 255),
            Color.FromArgb(255, 0, 225),
            Color.FromArgb(255, 0, 200),
            Color.FromArgb(255, 0, 175),
            Color.FromArgb(255, 0, 150),
            Color.FromArgb(255, 0, 125),
            Color.FromArgb(255, 0, 100),
            Color.FromArgb(255, 0, 75),
            Color.FromArgb(255, 0, 50),
            Color.FromArgb(255, 0, 25),
            // 61
            Color.FromArgb(255, 255, 255),
        };
    }
}