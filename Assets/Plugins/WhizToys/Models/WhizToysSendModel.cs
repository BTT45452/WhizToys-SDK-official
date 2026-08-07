namespace Plugins.WhizToys.Models
{
    public class WhizToysSendModel
    {
        public WhizToysLayout Layout;
        public int ColorIndex;
        public LightModeType LightMode = LightModeType.All;
        public CommandModeType CommandMode = CommandModeType.Immediately;
        public FeedBackModeType FeedBackMode = FeedBackModeType.None;
        public ShowTimeModeType ShowTimeMode = ShowTimeModeType.Short;
    }

    public enum LightModeType
    {
        All = 0,
        LeftUp = 1,
        LeftRight = 2,
        RightUp = 3,
        RightDown = 4,
        Only
    }

    public enum CommandModeType
    {
        Immediately = 0,
        AfterClick = 1
    }

    public enum FeedBackModeType
    {
        None = 0,
        ClickNone = 1,
        Basic = 2,
        Flash = 3,
        Marquee = 4,
        Breathe = 5,
        Neon = 6
    }

    public enum ShowTimeModeType
    {
        Short = 0,
        Long = 1
    }
}