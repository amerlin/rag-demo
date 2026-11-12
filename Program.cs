using RagDemo;
using RagDemo.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRagDemo(builder.Configuration);

var app = builder.Build();
app.LogRagDemoStartup(builder.Configuration);
app.MapRagDemoEndpoints();
app.Run();

