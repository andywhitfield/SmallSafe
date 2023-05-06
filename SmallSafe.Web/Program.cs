using Microsoft.AspNetCore;
using SmallSafe.Secure.Dictionary;

namespace SmallSafe.Web;

public class Program
{
    public async static Task Main(string[] args)
    {
        var host = WebHost.CreateDefaultBuilder(args)
            .UseIISIntegration()
            .UseStartup<Startup>()
            .Build();
        
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

        logger.LogInformation("Loaded word dictionary, running app");
        await host.RunAsync();
    }
}
