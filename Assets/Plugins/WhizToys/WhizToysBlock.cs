 namespace Plugins.WhizToys
{
    public class WhizToysBlock
    {
        private readonly int _row;
        private readonly int _column;
        public readonly bool Active;

        private readonly WhizToys _whizToys;

        public WhizToysBlock(WhizToys whizToys, int row, int column, bool active)
        {
            _whizToys = whizToys;

            _row = row;
            _column = column;
            Active = active;
        }

        public bool IsPressure
        {
            get
            {
                for (int i = 0; i < Pressures.Length; i++)
                {
                    if (Pressures[i] > 0)
                        return true;
                }

                return false;
            }
        }

        public bool AllPressure
        {
            get
            {
                for (int i = 0; i < Pressures.Length; i++)
                {
                    if (Pressures[i] < 1)
                        return false;
                }

                return true;
            }
        }

        public bool IsLeft
        {
            get
            {
                if (Pressures[0] > 0 && Pressures[1] > 0)
                    return true;
                else
                    return false;
            }
        }

        public bool IsRight
        {
            get
            {
                if (Pressures[2] > 0 && Pressures[3] > 0)
                    return true;
                else
                    return false;
            }
        }
        
        public bool IsUp
        {
            get
            {
                if (Pressures[0] > 0 && Pressures[3] > 0)
                    return true;
                else
                    return false;
            }
        }
        
        public bool IsDown
        {
            get
            {
                if (Pressures[1] > 0 && Pressures[2] > 0)
                    return true;
                else
                    return false;
            }
        }

        private int[] Pressures => _whizToys.AllPressures[_row, _column];
    }
}