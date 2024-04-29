using ShellProgressBar;

namespace BoothDownloader.misc;

class ChildProgressBarProgress : IProgress<double>
{
    private readonly ChildProgressBar _childProgressBar;

    public ChildProgressBarProgress(ChildProgressBar childProgressBar)
    {
        _childProgressBar = childProgressBar;
    }

    public void Report(double value)
    {
        _childProgressBar.Tick((int)(_childProgressBar.MaxTicks * value));
    }
}