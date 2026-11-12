using RagDemo;
using RagDemo.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRagDemo(builder.Configuration);

var app = builder.Build();
app.LogRagDemoStartup(builder.Configuration);

//File esterno per la mappatura delle api
app.MapRagDemoEndpoints();

app.Run();

