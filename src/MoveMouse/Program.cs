using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MoveMouse;

/// <summary>
/// Native entry point for the unpackaged (non-MSIX) deployment model. Unlike
/// packaged WinUI 3 apps, an unpackaged app has no project-generated
/// <c>Main</c>, so it must be provided explicitly: initialize WinRT's
/// ComWrappers support, then hand control to the WinUI 3 application model.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
