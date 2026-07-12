using ArkaCallCenter.Infrastructure;
using ArkaCallCenter.Realtime;
using ArkaCallCenter.Realtime.Audio;
using ArkaCallCenter.Realtime.Call;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// دسترسی به DbContext/RAG/Settings از لایه‌ی Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection("Realtime"));
builder.Services.AddSingleton<CallHandler>();
builder.Services.AddHostedService<AudioSocketServer>();

var host = builder.Build();
host.Run();
