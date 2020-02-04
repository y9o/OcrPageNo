namespace OcrPageNo
{
    public enum Position
    {
        None = 0,
        Top = 1,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Center = 1 << 4
    }

    class PageNumber
    {
        public int No;
        public Position Pos;
        public PageNumber(int no, Position pos)
        {
            No = no;
            Pos = pos;
        }
        public override int GetHashCode()
        {
            return (No << 5) | (int)Pos;
        }
        public override bool Equals(object obj)
        {
            var o = obj as PageNumber;
            if (o == null) return false;
            return this.No == o.No && this.Pos == o.Pos;
        }
    }
}
