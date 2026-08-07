namespace Plugins.WhizToys.Models
{
    public struct WhizToysLayout
    {
        public int Row;
        public int Column;

        public WhizToysLayout(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Compare(WhizToysLayout layout)
        {
            if (Row == layout.Row && Column == layout.Column)
                return true;

            return false;
        }
    }
}