using ShellProgressBar;

namespace BoothDownloader.misc;

class ChildProgressBarProgress(ChildProgressBar childProgressBar) : IProgress<double>
{
    public void Report(double value)
    {
        childProgressBar.Tick((int)(childProgressBar.MaxTicks * value));
    }
}