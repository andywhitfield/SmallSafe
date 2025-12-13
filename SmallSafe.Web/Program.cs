using SmallSafe.Secure.Dictionary;
using SmallSafe.Web;

var host = new HostBuilder()
    .ConfigureWebHost(webHostBuilder =>
    {
        webHostBuilder
#if DEBUG
            .UseKestrel()
#else
            .UseIIS()
#endif
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseStartup<Startup>();
    }).Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var wordDictionary = host.Services.GetService<IWordDictionary>();
if (wordDictionary != null)
{
    logger.LogInformation("Loading word dictionary");
    await wordDictionary.LoadAsync();
}
else
{
    logger.LogInformation("Word dictionary not available");
}

host.Run();
