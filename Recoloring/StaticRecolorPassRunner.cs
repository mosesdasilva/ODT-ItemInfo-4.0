using System.Threading;

namespace ItemInfo.Recoloring;

public sealed class StaticRecolorPassRunner
{
    private int _hasRun;

    public bool RunOnce(Action pass)
    {
        if (Interlocked.Exchange(ref _hasRun, 1) != 0)
            return false;

        pass();
        return true;
    }
}
