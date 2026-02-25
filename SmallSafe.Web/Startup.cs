using System.Collections.Immutable;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SmallSafe.Secure;
using SmallSafe.Secure.Dictionary;
using SmallSafe.Secure.Services;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web;

public class Startup
{
    private static readonly TimeSpan _loginSessionTimeout = new(0, 10, 0);

    public Startup(IWebHostEnvironment env)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();
        Configuration = builder.Build();
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);

        services
            .AddAuthentication(o => o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/signin";
                o.LogoutPath = "/signout";
                o.AccessDeniedPath = "/twofactor";
                o.Cookie.HttpOnly = true;
                o.Cookie.MaxAge = _loginSessionTimeout;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.IsEssential = true;
                o.ExpireTimeSpan = _loginSessionTimeout;
                o.SlidingExpiration = true;
            });

        services
            .AddAuthorization(options => options.AddPolicy(TwoFactorRequirement.PolicyName, policy => policy.AddRequirements(new TwoFactorRequirement())))
            .AddFido2(options =>
            {
                options.ServerName = "Small:Safe";
                options.ServerDomain = Configuration.GetValue<string>("FidoDomain");
                options.Origins = ImmutableHashSet.Create(Configuration.GetValue<string>("FidoOrigins"));
            });
        services.AddHttpContextAccessor();
        services
            .AddScoped<IAuthorizationHandler, TwoFactorHandler>()
            .AddScoped<IAuthorizationSession, AuthorizationSession>();

        services
            .AddDataProtection()
            .SetApplicationName(typeof(Startup).Namespace ?? "")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.ContentRootPath, ".keys")));

        services.AddLogging(logging =>
        {
            logging.AddSimpleConsole(opt =>
            {
                opt.UseUtcTimestamp = true;
                opt.TimestampFormat = "[HH:mm:ss.fff] ";
                opt.SingleLine = true;
            });
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Trace);
        });

        services.Configure<DropboxConfig>(Configuration.GetSection("Dropbox"));
        services.Configure<CookiePolicyOptions>(o =>
        {
            o.CheckConsentNeeded = context => false;
            o.MinimumSameSitePolicy = SameSiteMode.None;
        });

        services.AddMvc().AddSessionStateTempDataProvider();
        var builder = services.AddRazorPages();
#if DEBUG
        if (Environment.IsDevelopment())
            builder.AddRazorRuntimeCompilation();
#endif
        services.AddCors();
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = _loginSessionTimeout;
            options.Cookie.IsEssential = true;
        });

        services
            .AddDbContext<SqliteDataContext>((serviceProvider, options) =>
            {
                var sqliteConnectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("SmallSafe");
                serviceProvider.GetRequiredService<ILogger<SqliteDataContext>>().LogInformation("Using connection string: {SqliteConnectionString}", sqliteConnectionString);
                options.UseSqlite(sqliteConnectionString);
            })
            .AddScoped(sp => (ISqliteDataContext)sp.GetRequiredService<SqliteDataContext>())
            .AddScoped<IUserService, UserService>()
            .AddTransient<ISafeDbReadWriteService, SafeDbReadWriteService>()
            .AddTransient<ISafeDbService, SafeDbService>()
            .AddTransient<IEncryptDecrypt, EncryptDecrypt>()
            .AddTransient<ITwoFactor, TwoFactor>()
            .AddScoped<IRandomPasswordGenerator, RandomPasswordGenerator>()
            .AddSingleton<IWordDictionary, WordDictionary>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Home/Error");

        app.UseStaticFiles();
        app.UseCookiePolicy();
        app.UseSession();
        app.UseAuthentication();
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(options => options.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}"));

        using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
        scope.ServiceProvider.GetRequiredService<ISqliteDataContext>().Migrate();
    }
}
