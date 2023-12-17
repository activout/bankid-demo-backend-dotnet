using System.Security.Cryptography.X509Certificates;
using BankIdDemo.Backend;
using BankIdDemo.Backend.Gateways;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BankIdSettings>(builder.Configuration.GetSection(BankIdSettings.Key));
builder.Services.AddRestClient();
builder.Services.AddTransient<BankIdClientFactory>();
builder.Services.AddTransient<BankIdApiHandler>();
builder.Services.AddTransient<IBankIdGateway, BankIdGateway>();

builder.Services.AddHttpClient();
builder.Services
    .AddHttpClient<BankIdClientFactory>()
    .ConfigurePrimaryHttpMessageHandler(services =>
    {
        var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true
        };

        var settings = services.GetRequiredService<IOptions<BankIdSettings>>().Value;
        handler.ClientCertificates.Add(new X509Certificate2(
            settings.SslCertificatePath,
            settings.SslCertificatePassword));
        return handler;
    })
    .AddHttpMessageHandler<BankIdApiHandler>();

builder.Services.AddSingleton<IBankIdClient>(provider => provider.GetRequiredService<BankIdClientFactory>().Create());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();