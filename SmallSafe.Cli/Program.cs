using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmallSafe.Cli;
using SmallSafe.Secure;
using SmallSafe.Secure.Services;

try
{
    var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices(services => services
            .AddTransient<CliService>()
            .AddTransient<IEncryptDecrypt, EncryptDecrypt>()
            .AddTransient<ISafeDbService, SafeDbService>())
        .ConfigureLogging(builder => builder.AddSimpleConsole(opt =>
        {
            opt.SingleLine = true;
            opt.TimestampFormat = "[HH:mm:ss] ";
        }))
        .Build();

    await host.Services.GetRequiredService<CliService>().ExecuteAsync(args.Length == 0 ? null : args[0]);
}
catch (Exception ex)
{
    Console.WriteLine($"Application error, exiting! {ex}");
    Environment.Exit(1);
}
